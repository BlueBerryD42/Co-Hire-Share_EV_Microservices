using System;
using System.Collections.Generic;
using System.Linq;
using CoOwnershipVehicle.Booking.Api.Contracts;
using CoOwnershipVehicle.Booking.Api.DTOs;
using CoOwnershipVehicle.Booking.Api.Repositories;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Shared.Contracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace CoOwnershipVehicle.Booking.Api.Services;

public class BookingService : IBookingService
{
    private const int MaxEmergencyBookingsPerMonth = 2; // Define the limit for emergency bookings

    private readonly IBookingRepository _bookingRepository;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<BookingService> _logger;

    public BookingService(
        IBookingRepository bookingRepository,
        IPublishEndpoint publishEndpoint,
        ILogger<BookingService> logger)
    {
        _bookingRepository = bookingRepository ?? throw new ArgumentNullException(nameof(bookingRepository));
        _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BookingDto> CreateBookingAsync(CreateBookingDto createDto, Guid userId, bool isEmergency = false, string? emergencyReason = null)
    {
        // Ensure user has access to the vehicle
        if (!await _bookingRepository.UserHasVehicleAccessAsync(createDto.VehicleId, userId))
        {
            throw new UnauthorizedAccessException("Access denied to this vehicle");
        }

        var vehicle = await _bookingRepository.GetVehicleByIdAsync(createDto.VehicleId)
                      ?? throw new InvalidOperationException("Vehicle not found.");

        if (vehicle.GroupId == null)
        {
            throw new InvalidOperationException("Vehicle is not associated with a group.");
        }

        createDto.GroupId = vehicle.GroupId.Value; // Set GroupId from vehicle

        IReadOnlyList<Domain.Entities.Booking> conflictingBookings = Array.Empty<Domain.Entities.Booking>();
        EmergencyConflictResolutionResult? emergencyConflictResult = null;

        if (isEmergency)
        {
            // Validate user is group admin
            if (!await _bookingRepository.IsGroupAdminAsync(userId, createDto.GroupId))
            {
                _logger.LogWarning("Non-admin user {UserId} attempted to create an emergency booking for group {GroupId}.", userId, createDto.GroupId);
                throw new UnauthorizedAccessException("Only group admins can create emergency bookings.");
            }

            // Validate emergency reason is provided
            if (string.IsNullOrWhiteSpace(emergencyReason))
            {
                throw new InvalidOperationException("Emergency reason is required for emergency bookings.");
            }
            createDto.EmergencyReason = emergencyReason; // Ensure DTO has the reason

            // Enforce emergency booking limits
            var currentMonth = DateTime.UtcNow.Date; // Use current UTC date for month calculation
            var emergencyBookingCount = await _bookingRepository.GetEmergencyBookingCountForUserInMonthAsync(userId, currentMonth);
            if (emergencyBookingCount >= MaxEmergencyBookingsPerMonth)
            {
                _logger.LogWarning("User {UserId} exceeded emergency booking limit for month {Month}. Count: {Count}", userId, currentMonth.ToString("yyyy-MM"), emergencyBookingCount);
                throw new InvalidOperationException($"Emergency booking limit of {MaxEmergencyBookingsPerMonth} per month exceeded.");
            }
            
            conflictingBookings = await _bookingRepository.GetBookingsInPeriodAsync(createDto.VehicleId, createDto.StartAt, createDto.EndAt);
            emergencyConflictResult = await HandleEmergencyConflictsAsync(
                conflictingBookings,
                createDto,
                vehicle,
                userId,
                emergencyReason);

            await NotifyGroupOfEmergencyBookingAsync(createDto, vehicle, userId, emergencyReason);
        }
        else // Not an emergency booking, proceed with normal conflict checks
        {
            var conflicts = await CheckBookingConflictsAsync(createDto.VehicleId, createDto.StartAt, createDto.EndAt);

            if (conflicts.HasConflicts)
            {
                var conflictUserPriorityScore = await GetUserPriorityAsync(userId, createDto.VehicleId);
                var conflictUserPriority = MapPriorityScoreToEnum(conflictUserPriorityScore);
                var requiresApproval = conflicts.ConflictingBookings.Any(cb => (int)cb.Priority >= (int)conflictUserPriority);

                if (requiresApproval)
                {
                    return await CreatePendingBookingAsync(createDto, userId, conflicts);
                }
                else
                {
                    throw new InvalidOperationException($"Booking conflicts detected with {conflicts.ConflictingBookings.Count} existing bookings");
                }
            }
        }

        var userPriorityScore = await GetUserPriorityAsync(userId, createDto.VehicleId);
        var userPriority = isEmergency ? BookingPriority.Emergency : MapPriorityScoreToEnum(userPriorityScore);

        var booking = new Domain.Entities.Booking
        {
            Id = Guid.NewGuid(),
            VehicleId = createDto.VehicleId,
            GroupId = createDto.GroupId,
            UserId = userId,
            StartAt = createDto.StartAt,
            EndAt = createDto.EndAt,
            Purpose = createDto.Purpose,
            Notes = createDto.Notes,
            IsEmergency = isEmergency,
            EmergencyReason = emergencyReason,
            Priority = userPriority,
            PriorityScore = userPriorityScore,
            Status = Domain.Entities.BookingStatus.Confirmed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _bookingRepository.AddAsync(booking);
        await _bookingRepository.SaveChangesAsync();

        await _publishEndpoint.Publish(new BookingCreatedEvent
        {
            BookingId = booking.Id,
            VehicleId = booking.VehicleId,
            UserId = booking.UserId,
            StartAt = booking.StartAt,
            EndAt = booking.EndAt,
            Status = BookingStatus.Confirmed,
            IsEmergency = booking.IsEmergency,
            Priority = booking.Priority
        });

        _logger.LogInformation("Booking {BookingId} created for vehicle {VehicleId} by user {UserId}. IsEmergency: {IsEmergency}", booking.Id, booking.VehicleId, userId, isEmergency);

        if (isEmergency)
        {
            await PublishEmergencySummaryEventsAsync(
                booking,
                emergencyReason!,
                emergencyConflictResult ?? new EmergencyConflictResolutionResult(),
                conflictingBookings,
                createDto.EmergencyAutoCancelConflicts);
        }

        return await GetBookingByIdAsync(booking.Id);
    }

    public async Task<List<BookingDto>> GetUserBookingsAsync(Guid userId, DateTime? from = null, DateTime? to = null)
    {
        var bookings = await _bookingRepository.GetUserBookingsAsync(userId, from, to);
        return bookings.Select(MapBookingToDto).ToList();
    }

    public async Task<List<BookingDto>> GetVehicleBookingsAsync(Guid vehicleId, DateTime? from = null, DateTime? to = null)
    {
        var bookings = await _bookingRepository.GetVehicleBookingsAsync(vehicleId, from, to);
        return bookings.Select(MapBookingToDto).ToList();
    }

    public async Task<BookingConflictSummaryDto> CheckBookingConflictsAsync(Guid vehicleId, DateTime startAt, DateTime endAt, Guid? excludeBookingId = null)
    {
        var conflicts = await _bookingRepository.GetConflictingBookingsAsync(vehicleId, startAt, endAt, excludeBookingId);
        var conflictDtos = conflicts.Select(b => new BookingDto
        {
            Id = b.Id,
            VehicleId = b.VehicleId,
            VehicleModel = b.Vehicle?.Model ?? string.Empty,
            VehiclePlateNumber = b.Vehicle?.PlateNumber ?? string.Empty,
            GroupId = b.GroupId,
            GroupName = b.Group?.Name ?? string.Empty,
            UserId = b.UserId,
            UserFirstName = b.User?.FirstName ?? string.Empty,
            UserLastName = b.User?.LastName ?? string.Empty,
            StartAt = b.StartAt,
            EndAt = b.EndAt,
            Purpose = b.Purpose,
            Notes = b.Notes,
            Status = (BookingStatus)b.Status,
            Priority = b.Priority,
            PriorityScore = b.PriorityScore,
            IsEmergency = b.IsEmergency,
            RequiresDamageReview = b.RequiresDamageReview,
            RecurringBookingId = b.RecurringBookingId,
            CreatedAt = b.CreatedAt
        }).ToList();

        return new BookingConflictSummaryDto
        {
            VehicleId = vehicleId,
            RequestedStartAt = startAt,
            RequestedEndAt = endAt,
            HasConflicts = conflictDtos.Any(),
            ConflictingBookings = conflictDtos
        };
    }

    public async Task<List<BookingPriorityDto>> GetBookingPriorityQueueAsync(Guid vehicleId, DateTime startAt, DateTime endAt)
    {
        var bookings = await _bookingRepository.GetBookingsForPriorityQueueAsync(vehicleId, startAt, endAt);

        return bookings.Select(b => new BookingPriorityDto
        {
            BookingId = b.Id,
            UserId = b.UserId,
            UserName = $"{b.User.FirstName} {b.User.LastName}",
            VehicleId = b.VehicleId,
            StartAt = b.StartAt,
            EndAt = b.EndAt,
            Status = (BookingStatus)b.Status,
            Priority = (int)b.Priority,
            IsEmergency = b.IsEmergency,
            PriorityScore = CalculatePriorityScore(b),
            OwnershipPercentage = GetUserOwnershipPercentage(b.UserId, b.Vehicle!.Group!.Members)
        })
        .OrderByDescending(p => p.PriorityScore)
        .ThenByDescending(p => p.OwnershipPercentage)
        .ThenBy(p => p.StartAt)
        .ToList();
    }

    public async Task<BookingDto> ApproveBookingAsync(Guid bookingId, Guid approverId)
    {
        var booking = await _bookingRepository.GetBookingWithDetailsAsync(bookingId)
                      ?? throw new ArgumentException("Booking not found");

        var isAdmin = booking.Vehicle!.Group!.Members
            .Any(m => m.UserId == approverId && m.RoleInGroup == GroupRole.Admin);

        if (!isAdmin)
        {
            throw new UnauthorizedAccessException("Only group admins can approve bookings");
        }

        booking.Status = Domain.Entities.BookingStatus.Confirmed;
        booking.UpdatedAt = DateTime.UtcNow;

        await _bookingRepository.SaveChangesAsync();

        await _publishEndpoint.Publish(new BookingApprovedEvent
        {
            BookingId = booking.Id,
            VehicleId = booking.VehicleId,
            UserId = booking.UserId,
            ApprovedBy = approverId,
            StartAt = booking.StartAt,
            EndAt = booking.EndAt
        });

        _logger.LogInformation("Booking {BookingId} approved by {ApproverId}", bookingId, approverId);

        return await GetBookingByIdAsync(booking.Id);
    }

    public async Task<BookingDto> CancelBookingAsync(Guid bookingId, Guid userId, string? reason = null)
    {
        var booking = await _bookingRepository.GetBookingWithDetailsAsync(bookingId)
                      ?? throw new ArgumentException("Booking not found");

        var isOwner = booking.UserId == userId;
        var isAdmin = booking.Vehicle!.Group!.Members
            .Any(m => m.UserId == userId && m.RoleInGroup == GroupRole.Admin);

        if (!isOwner && !isAdmin)
        {
            throw new UnauthorizedAccessException("Cannot cancel this booking");
        }

        booking.Status = Domain.Entities.BookingStatus.Cancelled;
        booking.UpdatedAt = DateTime.UtcNow;
        booking.Notes = !string.IsNullOrEmpty(reason)
            ? $"{booking.Notes}\n[CANCELLED] {reason}"
            : $"{booking.Notes}\n[CANCELLED]";

        await _bookingRepository.SaveChangesAsync();

        await _publishEndpoint.Publish(new BookingCancelledEvent
        {
            BookingId = booking.Id,
            VehicleId = booking.VehicleId,
            UserId = booking.UserId,
            CancelledBy = userId,
            Reason = reason,
            StartAt = booking.StartAt,
            EndAt = booking.EndAt
        });

        _logger.LogInformation("Booking {BookingId} cancelled by {UserId}", bookingId, userId);

        return await GetBookingByIdAsync(booking.Id);
    }

    public async Task<List<BookingDto>> GetPendingApprovalsAsync(Guid userId)
    {
        var adminVehicleIds = await _bookingRepository.GetAdminVehicleIdsAsync(userId);
        var approvals = await _bookingRepository.GetPendingApprovalsAsync(adminVehicleIds);
        return approvals.Select(MapBookingToDto).ToList();
    }

    private async Task<BookingDto> CreatePendingBookingAsync(CreateBookingDto createDto, Guid userId, BookingConflictSummaryDto conflicts)
    {
        var userPriorityScore = await GetUserPriorityAsync(userId, createDto.VehicleId);
        var userPriority = MapPriorityScoreToEnum(userPriorityScore);

        var booking = new Domain.Entities.Booking
        {
            Id = Guid.NewGuid(),
            VehicleId = createDto.VehicleId,
            GroupId = createDto.GroupId,
            UserId = userId,
            StartAt = createDto.StartAt,
            EndAt = createDto.EndAt,
            Purpose = createDto.Purpose,
            Notes = createDto.Notes,
            IsEmergency = createDto.IsEmergency,
            EmergencyReason = createDto.EmergencyReason,
            Priority = userPriority,
            PriorityScore = userPriorityScore,
            Status = Domain.Entities.BookingStatus.PendingApproval,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _bookingRepository.AddAsync(booking);
        await _bookingRepository.SaveChangesAsync();

        await _publishEndpoint.Publish(new BookingPendingApprovalEvent
        {
            BookingId = booking.Id,
            VehicleId = booking.VehicleId,
            UserId = booking.UserId,
            StartAt = booking.StartAt,
            EndAt = booking.EndAt,
            ConflictCount = conflicts.ConflictingBookings.Count
        });

        return await GetBookingByIdAsync(booking.Id);
    }

    private async Task<int> GetUserPriorityAsync(Guid userId, Guid vehicleId)
    {
        var member = await _bookingRepository.GetMemberForVehicleAsync(userId, vehicleId);

        if (member == null)
        {
            return 0;
        }

        var basePriority = (int)(member.SharePercentage * 100);
        var rolePriority = member.RoleInGroup == GroupRole.Admin ? 50 : 0;

        return basePriority + rolePriority;
    }

    private int CalculatePriorityScore(Domain.Entities.Booking booking)
    {
        var score = (int)booking.Priority;

        if (booking.IsEmergency)
        {
            score += 1000;
        }

        if (booking.Status == Domain.Entities.BookingStatus.Confirmed)
        {
            score += 100;
        }

        var daysSinceCreated = (DateTime.UtcNow - booking.CreatedAt).Days;
        score += Math.Max(0, 30 - daysSinceCreated);

        return score;
    }

    private decimal GetUserOwnershipPercentage(Guid userId, ICollection<GroupMember> members)
    {
        return members.FirstOrDefault(m => m.UserId == userId)?.SharePercentage ?? 0;
    }

    private static BookingPriority MapPriorityScoreToEnum(int priorityScore)
    {
        // Map priority score (0-150) to BookingPriority enum
        // 0-30: Low, 31-70: Normal, 71-120: High, 121+: Emergency (but Emergency is set separately)
        if (priorityScore <= 30)
            return BookingPriority.Low;
        if (priorityScore <= 70)
            return BookingPriority.Normal;
        return BookingPriority.High;
    }

    private async Task<BookingDto> GetBookingByIdAsync(Guid bookingId)
    {
        var booking = await _bookingRepository.GetBookingWithVehicleAndUserAsync(bookingId)
                      ?? throw new InvalidOperationException("Failed to load booking");

        return MapBookingToDto(booking);
    }

    private static BookingDto MapBookingToDto(Domain.Entities.Booking booking)
    {
        return new BookingDto
        {
            Id = booking.Id,
            VehicleId = booking.VehicleId,
            VehicleModel = booking.Vehicle?.Model ?? string.Empty,
            VehiclePlateNumber = booking.Vehicle?.PlateNumber ?? string.Empty,
            GroupId = booking.GroupId,
            GroupName = booking.Group?.Name ?? string.Empty,
            UserId = booking.UserId,
            UserFirstName = booking.User?.FirstName ?? string.Empty,
            UserLastName = booking.User?.LastName ?? string.Empty,
            StartAt = booking.StartAt,
            EndAt = booking.EndAt,
            Purpose = booking.Purpose,
            Notes = booking.Notes,
            Status = (BookingStatus)booking.Status,
            Priority = booking.Priority,
            PriorityScore = booking.PriorityScore,
            IsEmergency = booking.IsEmergency,
            RequiresDamageReview = booking.RequiresDamageReview,
            RecurringBookingId = booking.RecurringBookingId,
            CreatedAt = booking.CreatedAt
        };
    }

    private async Task NotifyGroupOfEmergencyBookingAsync(CreateBookingDto createDto, Vehicle vehicle, Guid createdByUserId, string emergencyReason)
    {
        var groupMembers = await _bookingRepository.GetGroupMembersAsync(createDto.GroupId);
        var groupMemberUserIds = groupMembers.Select(gm => gm.UserId).Distinct().ToList();
        if (!groupMemberUserIds.Any())
        {
            return;
        }

        await _publishEndpoint.Publish(new BulkNotificationEvent
        {
            UserIds = groupMemberUserIds,
            GroupId = createDto.GroupId,
            Title = "New Emergency Vehicle Booking",
            Message = $"An emergency booking for vehicle {vehicle.Model} ({vehicle.PlateNumber}) has been created by user {createdByUserId} from {createDto.StartAt:F} to {createDto.EndAt:F}. Reason: {emergencyReason}",
            Type = "EmergencyBookingCreation",
            Priority = "High",
            CreatedAt = DateTime.UtcNow,
            ActionUrl = $"/bookings/vehicle/{createDto.VehicleId}",
            ActionText = "View vehicle calendar"
        });

        _logger.LogInformation("Notified {Count} group members about emergency booking {StartAt} - {EndAt}.", groupMemberUserIds.Count, createDto.StartAt, createDto.EndAt);
    }

    private async Task<EmergencyConflictResolutionResult> HandleEmergencyConflictsAsync(
        IReadOnlyList<Domain.Entities.Booking> conflictingBookings,
        CreateBookingDto createDto,
        Vehicle vehicle,
        Guid emergencyCreatorId,
        string emergencyReason)
    {
        var result = new EmergencyConflictResolutionResult();

        if (!conflictingBookings.Any())
        {
            return result;
        }

        foreach (var conflict in conflictingBookings)
        {
            if (conflict.IsEmergency)
            {
                _logger.LogInformation(
                    "Emergency booking {StartAt} - {EndAt} by {UserId} for vehicle {VehicleId} cannot override existing emergency booking {ExistingBookingId}.",
                    createDto.StartAt,
                    createDto.EndAt,
                    emergencyCreatorId,
                    createDto.VehicleId,
                    conflict.Id);
                throw new InvalidOperationException("Cannot override an existing emergency booking with another emergency booking.");
            }

            var rescheduleInfo = await TryRescheduleBookingAsync(conflict, createDto.StartAt, createDto.EndAt);
            if (rescheduleInfo != null)
            {
                result.Rescheduled.Add(rescheduleInfo);

                await _publishEndpoint.Publish(new BookingRescheduledEvent
                {
                    BookingId = conflict.Id,
                    VehicleId = conflict.VehicleId,
                    GroupId = conflict.GroupId,
                    UserId = conflict.UserId,
                    OriginalStartAt = rescheduleInfo.OriginalStartAt,
                    OriginalEndAt = rescheduleInfo.OriginalEndAt,
                    NewStartAt = rescheduleInfo.NewStartAt,
                    NewEndAt = rescheduleInfo.NewEndAt,
                    Reason = $"Rescheduled due to emergency booking {createDto.StartAt:u} - {createDto.EndAt:u}"
                });

                await _publishEndpoint.Publish(new BulkNotificationEvent
                {
                    UserIds = new List<Guid> { conflict.UserId },
                    GroupId = conflict.GroupId,
                    Title = "Your booking has been rescheduled",
                    Message = $"Your booking for vehicle {vehicle.Model} ({vehicle.PlateNumber}) was moved to {rescheduleInfo.NewStartAt:F} - {rescheduleInfo.NewEndAt:F} due to an emergency booking. Please review the new time.",
                    Type = "EmergencyBookingRescheduled",
                    Priority = "High",
                    CreatedAt = DateTime.UtcNow,
                    ActionUrl = $"/bookings/{conflict.Id}",
                    ActionText = "Review booking"
                });

                _logger.LogInformation("Rescheduled booking {BookingId} to {Start} - {End} due to emergency override.", conflict.Id, rescheduleInfo.NewStartAt, rescheduleInfo.NewEndAt);
                continue;
            }

            if (createDto.EmergencyAutoCancelConflicts)
            {
                conflict.Status = Domain.Entities.BookingStatus.Cancelled;
                conflict.UpdatedAt = DateTime.UtcNow;
                conflict.Notes = AppendNote(conflict.Notes, $"[AUTO-CANCELLED BY EMERGENCY {createDto.StartAt:u}] {emergencyReason}");
                result.AutoCancelled.Add(conflict.Id);

                await _publishEndpoint.Publish(new BookingCancelledEvent
                {
                    BookingId = conflict.Id,
                    VehicleId = conflict.VehicleId,
                    UserId = conflict.UserId,
                    CancelledBy = emergencyCreatorId,
                    Reason = $"Cancelled by emergency booking. Reason: {emergencyReason}",
                    CancellationReason = emergencyReason,
                    StartAt = conflict.StartAt,
                    EndAt = conflict.EndAt
                });

                await _publishEndpoint.Publish(new BulkNotificationEvent
                {
                    UserIds = new List<Guid> { conflict.UserId },
                    GroupId = conflict.GroupId,
                    Title = "Booking cancelled due to emergency",
                    Message = $"Your booking for vehicle {vehicle.Model} ({vehicle.PlateNumber}) from {conflict.StartAt:F} to {conflict.EndAt:F} was cancelled due to an emergency booking. Reason: {emergencyReason}",
                    Type = "EmergencyBookingCancellation",
                    Priority = "High",
                    CreatedAt = DateTime.UtcNow,
                    ActionUrl = $"/bookings/{conflict.Id}",
                    ActionText = "Review booking"
                });

                _logger.LogInformation("Auto-cancelled booking {BookingId} due to emergency override by user {UserId}.", conflict.Id, emergencyCreatorId);
            }
            else
            {
                conflict.Status = Domain.Entities.BookingStatus.PendingApproval;
                conflict.UpdatedAt = DateTime.UtcNow;
                conflict.Notes = AppendNote(conflict.Notes, $"[PENDING RESCHEDULE DUE TO EMERGENCY {createDto.StartAt:u}] {emergencyReason}");
                result.PendingResolution.Add(conflict.Id);

                await _publishEndpoint.Publish(new BulkNotificationEvent
                {
                    UserIds = new List<Guid> { conflict.UserId },
                    GroupId = conflict.GroupId,
                    Title = "Action required: booking affected by emergency",
                    Message = $"Your booking for vehicle {vehicle.Model} ({vehicle.PlateNumber}) from {conflict.StartAt:F} to {conflict.EndAt:F} was affected by an emergency booking. Please reschedule or contact your group admin.",
                    Type = "EmergencyBookingPendingResolution",
                    Priority = "High",
                    CreatedAt = DateTime.UtcNow,
                    ActionUrl = $"/bookings/{conflict.Id}",
                    ActionText = "Reschedule booking"
                });

                _logger.LogInformation("Marked booking {BookingId} as pending resolution due to emergency override by user {UserId}.", conflict.Id, emergencyCreatorId);
            }
        }

        if (!createDto.EmergencyAutoCancelConflicts && result.PendingResolution.Count > 0)
        {
            await _publishEndpoint.Publish(new BulkNotificationEvent
            {
                UserIds = new List<Guid> { emergencyCreatorId },
                GroupId = createDto.GroupId,
                Title = "Emergency booking requires follow-up",
                Message = $"Emergency booking {createDto.StartAt:F} - {createDto.EndAt:F} could not automatically resolve {result.PendingResolution.Count} conflicting bookings. Please review them.",
                Type = "EmergencyBookingManualResolution",
                Priority = "High",
                CreatedAt = DateTime.UtcNow,
                ActionUrl = $"/bookings/vehicle/{createDto.VehicleId}",
                ActionText = "Review conflicts"
            });
        }

        return result;
    }

    private async Task<RescheduledBookingInfo?> TryRescheduleBookingAsync(Domain.Entities.Booking booking, DateTime emergencyStart, DateTime emergencyEnd)
    {
        var duration = booking.EndAt - booking.StartAt;
        if (duration <= TimeSpan.Zero)
        {
            return null;
        }

        var originalStart = booking.StartAt;
        var originalEnd = booking.EndAt;

        var candidateStart = emergencyEnd > DateTime.UtcNow ? emergencyEnd : DateTime.UtcNow;
        const int maxAttempts = 12;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var candidateEnd = candidateStart.Add(duration);
            var conflicts = await _bookingRepository.GetConflictingBookingsAsync(
                booking.VehicleId,
                candidateStart,
                candidateEnd,
                booking.Id,
                booking.RecurringBookingId);

            if (!conflicts.Any())
            {
                booking.StartAt = candidateStart;
                booking.EndAt = candidateEnd;
                booking.UpdatedAt = DateTime.UtcNow;
                booking.Notes = AppendNote(booking.Notes, $"[RESCHEDULED due to emergency {emergencyStart:u} - {emergencyEnd:u}]");

                return new RescheduledBookingInfo(
                    booking.Id,
                    booking.UserId,
                    originalStart,
                    originalEnd,
                    candidateStart,
                    candidateEnd);
            }

            candidateStart = candidateStart.AddMinutes(30);
        }

        return null;
    }

    private async Task PublishEmergencySummaryEventsAsync(
        Domain.Entities.Booking booking,
        string emergencyReason,
        EmergencyConflictResolutionResult conflictResult,
        IReadOnlyList<Domain.Entities.Booking> initialConflicts,
        bool autoCancelApplied)
    {
        await _publishEndpoint.Publish(new EmergencyBookingUsageEvent
        {
            BookingId = booking.Id,
            VehicleId = booking.VehicleId,
            GroupId = booking.GroupId,
            UserId = booking.UserId,
            OccurredAt = booking.CreatedAt
        });

        await _publishEndpoint.Publish(new EmergencyBookingAuditEvent
        {
            BookingId = booking.Id,
            VehicleId = booking.VehicleId,
            GroupId = booking.GroupId,
            CreatedBy = booking.UserId,
            CreatedAt = booking.CreatedAt,
            Reason = emergencyReason,
            AutoCancelApplied = autoCancelApplied,
            ConflictingBookingIds = initialConflicts.Select(b => b.Id).ToList(),
            AutoCancelledBookingIds = conflictResult.AutoCancelled.ToList(),
            RescheduledBookingIds = conflictResult.Rescheduled.Select(r => r.BookingId).ToList(),
            PendingResolutionBookingIds = conflictResult.PendingResolution.ToList()
        });
    }

    private static string AppendNote(string? existing, string note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return existing ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(existing))
        {
            return note;
        }

        return $"{existing}\n{note}";
    }

    private sealed class EmergencyConflictResolutionResult
    {
        public List<RescheduledBookingInfo> Rescheduled { get; } = new();
        public List<Guid> AutoCancelled { get; } = new();
        public List<Guid> PendingResolution { get; } = new();
    }

    private sealed record RescheduledBookingInfo(
        Guid BookingId,
        Guid UserId,
        DateTime OriginalStartAt,
        DateTime OriginalEndAt,
        DateTime NewStartAt,
        DateTime NewEndAt);
}
