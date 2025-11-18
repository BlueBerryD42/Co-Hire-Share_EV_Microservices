using System;
using System.Collections.Generic;
using System.Linq;
using CoOwnershipVehicle.Booking.Api.Configuration;
using CoOwnershipVehicle.Booking.Api.Contracts;
using CoOwnershipVehicle.Booking.Api.DTOs;
using CoOwnershipVehicle.Booking.Api.Repositories;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoOwnershipVehicle.Booking.Api.Services;

public class BookingService : IBookingService
{
    private const int MaxEmergencyBookingsPerMonth = 2; // Define the limit for emergency bookings
    private const int DefaultHistoryLimit = 20;
    private const int MaxHistoryLimit = 100;

    private readonly IBookingRepository _bookingRepository;
    private readonly ILogger<BookingService> _logger;
    private readonly TripPricingOptions _tripPricingOptions;

    public BookingService(
        IBookingRepository bookingRepository,
        ILogger<BookingService> logger,
        IOptions<TripPricingOptions> tripPricingOptions)
    {
        _bookingRepository = bookingRepository ?? throw new ArgumentNullException(nameof(bookingRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tripPricingOptions = tripPricingOptions?.Value ?? throw new ArgumentNullException(nameof(tripPricingOptions));
    }

    public async Task<BookingDto> CreateBookingAsync(CreateBookingDto createDto, Guid userId, bool isEmergency = false, string? emergencyReason = null)
    {
        if (createDto.VehicleId == Guid.Empty)
        {
            throw new InvalidOperationException("VehicleId must be provided when creating a booking.");
        }

        if (createDto.GroupId == Guid.Empty)
        {
            throw new InvalidOperationException("GroupId must be provided when creating a booking.");
        }

        IReadOnlyList<Domain.Entities.Booking> conflictingBookings = Array.Empty<Domain.Entities.Booking>();
        EmergencyConflictResolutionResult? emergencyConflictResult = null;

        if (isEmergency)
        {
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
                userId,
                emergencyReason);

            await NotifyGroupOfEmergencyBookingAsync(createDto, userId, emergencyReason);
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
            UpdatedAt = DateTime.UtcNow,
            VehicleStatus = VehicleStatus.Available,
            DistanceKm = null,
            TripFeeAmount = 0m
        };

        await _bookingRepository.AddAsync(booking);
        await _bookingRepository.SaveChangesAsync();

        _logger.LogInformation("Booking {BookingId} created for vehicle {VehicleId} by user {UserId}. IsEmergency: {IsEmergency}", booking.Id, booking.VehicleId, userId, isEmergency);

        if (isEmergency)
        {
            _logger.LogInformation("Emergency booking {BookingId} recorded with reason {Reason}", booking.Id, emergencyReason);
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

    public async Task<IReadOnlyList<BookingHistoryEntryDto>> GetUserBookingHistoryAsync(Guid userId, int limit = DefaultHistoryLimit)
    {
        var normalizedLimit = NormalizeHistoryLimit(limit);
        var bookings = await _bookingRepository.GetUserBookingHistoryAsync(userId, DateTime.UtcNow, normalizedLimit);

        return bookings
            .Select(booking => new BookingHistoryEntryDto
            {
                Booking = MapBookingToDto(booking),
                CheckIns = booking.CheckIns
                    .OrderBy(ci => ci.CheckInTime)
                    .Select(MapCheckInToDto)
                    .ToList()
            })
            .ToList();
    }

    public async Task<BookingConflictSummaryDto> CheckBookingConflictsAsync(Guid vehicleId, DateTime startAt, DateTime endAt, Guid? excludeBookingId = null)
    {
        var conflicts = await _bookingRepository.GetConflictingBookingsAsync(vehicleId, startAt, endAt, excludeBookingId);
        var conflictDtos = conflicts.Select(MapBookingToDto).ToList();

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
            UserName = b.UserId.ToString(),
            VehicleId = b.VehicleId,
            StartAt = b.StartAt,
            EndAt = b.EndAt,
            Status = (BookingStatus)b.Status,
            Priority = (int)b.Priority,
            IsEmergency = b.IsEmergency,
            PriorityScore = CalculatePriorityScore(b),
            OwnershipPercentage = 0m
        })
        .OrderByDescending(p => p.PriorityScore)
        .ThenBy(p => p.StartAt)
        .ToList();
    }

    public async Task<BookingDto> ApproveBookingAsync(Guid bookingId, Guid approverId)
    {
        var booking = await _bookingRepository.GetBookingWithDetailsAsync(bookingId)
                      ?? throw new ArgumentException("Booking not found");

        booking.Status = Domain.Entities.BookingStatus.Confirmed;
        booking.UpdatedAt = DateTime.UtcNow;

        await _bookingRepository.SaveChangesAsync();

        _logger.LogInformation("Booking {BookingId} approved by {ApproverId}", bookingId, approverId);

        return await GetBookingByIdAsync(booking.Id);
    }

    public async Task<BookingDto> CancelBookingAsync(Guid bookingId, Guid userId, string? reason = null)
    {
        var booking = await _bookingRepository.GetBookingWithDetailsAsync(bookingId)
                      ?? throw new ArgumentException("Booking not found");

        booking.Status = Domain.Entities.BookingStatus.Cancelled;
        booking.UpdatedAt = DateTime.UtcNow;
        booking.Notes = !string.IsNullOrEmpty(reason)
            ? $"{booking.Notes}\n[CANCELLED] {reason}"
            : $"{booking.Notes}\n[CANCELLED]";

        await _bookingRepository.SaveChangesAsync();

        _logger.LogInformation("Booking {BookingId} cancelled by {UserId}", bookingId, userId);

        return await GetBookingByIdAsync(booking.Id);
    }

    public async Task<List<BookingDto>> GetPendingApprovalsAsync(Guid userId)
    {
        var userBookings = await _bookingRepository.GetUserBookingsAsync(userId);
        var pending = userBookings
            .Where(b => b.Status == Domain.Entities.BookingStatus.PendingApproval)
            .ToList();

        return pending.Select(MapBookingToDto).ToList();
    }

    public async Task<BookingDto> UpdateVehicleStatusAsync(Guid bookingId, UpdateVehicleStatusDto request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var booking = await _bookingRepository.GetBookingWithDetailsAsync(bookingId)
                      ?? throw new ArgumentException("Booking not found", nameof(bookingId));

        booking.VehicleStatus = request.Status;
        booking.UpdatedAt = DateTime.UtcNow;

        await _bookingRepository.SaveChangesAsync();
        return MapBookingToDto(booking);
    }

    public async Task<BookingDto> UpdateTripSummaryAsync(Guid bookingId, UpdateTripSummaryDto request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (request.DistanceKm < 0)
        {
            throw new InvalidOperationException("Distance must not be negative.");
        }

        var booking = await _bookingRepository.GetBookingWithDetailsAsync(bookingId)
                      ?? throw new ArgumentException("Booking not found", nameof(bookingId));

        booking.DistanceKm = request.DistanceKm;
        booking.TripFeeAmount = CalculateTripFee(request.DistanceKm);
        booking.UpdatedAt = DateTime.UtcNow;

        await _bookingRepository.SaveChangesAsync();
        return MapBookingToDto(booking);
    }

    private decimal CalculateTripFee(decimal distanceKm)
    {
        var baseCost = distanceKm * _tripPricingOptions.CostPerKm;
        var normalized = Math.Round(baseCost, 2, MidpointRounding.AwayFromZero);

        if (_tripPricingOptions.MinimumFee.HasValue && normalized < _tripPricingOptions.MinimumFee.Value)
        {
            return _tripPricingOptions.MinimumFee.Value;
        }

        return normalized;
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
            UpdatedAt = DateTime.UtcNow,
            VehicleStatus = VehicleStatus.Available,
            DistanceKm = null,
            TripFeeAmount = 0m
        };

        await _bookingRepository.AddAsync(booking);
        await _bookingRepository.SaveChangesAsync();

        return await GetBookingByIdAsync(booking.Id);
    }

    private Task<int> GetUserPriorityAsync(Guid userId, Guid vehicleId)
    {
        // Without ownership metadata locally we fall back to a deterministic score so conflicting
        // requests remain stable. Upstream services enforce real prioritisation rules.
        var hash = Math.Abs(HashCode.Combine(userId, vehicleId));
        var normalizedScore = 25 + (hash % 75); // Keep score within a reasonable range (25-99)
        return Task.FromResult(normalizedScore);
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
            CreatedAt = booking.CreatedAt,
            VehicleStatus = booking.VehicleStatus,
            DistanceKm = booking.DistanceKm,
            TripFeeAmount = booking.TripFeeAmount
        };
    }

    private static CheckInDto MapCheckInToDto(CheckIn entity)
    {
        return new CheckInDto
        {
            Id = entity.Id,
            BookingId = entity.BookingId,
            UserId = entity.UserId,
            VehicleId = entity.VehicleId,
            Type = entity.Type,
            Odometer = entity.Odometer,
            Notes = entity.Notes,
            SignatureReference = entity.SignatureReference,
            CheckInTime = entity.CheckInTime,
            IsLateReturn = entity.IsLateReturn,
            LateReturnMinutes = entity.LateReturnMinutes,
            LateFeeAmount = entity.LateFeeAmount,
            Photos = entity.Photos.Select(p => new CheckInPhotoDto
            {
                Id = p.Id,
                CheckInId = entity.Id,
                PhotoUrl = p.PhotoUrl,
                ThumbnailUrl = p.ThumbnailUrl,
                Description = p.Description,
                Type = p.Type,
                ContentType = p.ContentType,
                CapturedAt = p.CapturedAt,
                Latitude = p.Latitude,
                Longitude = p.Longitude,
                IsDeleted = p.IsDeleted
            }).ToList(),
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    private static int NormalizeHistoryLimit(int limit)
    {
        if (limit <= 0)
        {
            return DefaultHistoryLimit;
        }

        return Math.Clamp(limit, 1, MaxHistoryLimit);
    }

    private Task NotifyGroupOfEmergencyBookingAsync(CreateBookingDto createDto, Guid createdByUserId, string emergencyReason)
    {
        // Group level notifications are now handled by upstream services that own membership data.
        _logger.LogInformation(
            "Emergency booking {StartAt} - {EndAt} created for vehicle {VehicleId} by user {UserId}. Reason: {Reason}",
            createDto.StartAt,
            createDto.EndAt,
            createDto.VehicleId,
            createdByUserId,
            emergencyReason);

        return Task.CompletedTask;
    }

    private async Task<EmergencyConflictResolutionResult> HandleEmergencyConflictsAsync(
        IReadOnlyList<Domain.Entities.Booking> conflictingBookings,
        CreateBookingDto createDto,
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

                _logger.LogInformation("Rescheduled booking {BookingId} to {Start} - {End} due to emergency override.", conflict.Id, rescheduleInfo.NewStartAt, rescheduleInfo.NewEndAt);
                continue;
            }

            if (createDto.EmergencyAutoCancelConflicts)
            {
                conflict.Status = Domain.Entities.BookingStatus.Cancelled;
                conflict.UpdatedAt = DateTime.UtcNow;
                conflict.Notes = AppendNote(conflict.Notes, $"[AUTO-CANCELLED BY EMERGENCY {createDto.StartAt:u}] {emergencyReason}");
                result.AutoCancelled.Add(conflict.Id);

                _logger.LogInformation("Auto-cancelled booking {BookingId} due to emergency override by user {UserId}.", conflict.Id, emergencyCreatorId);
            }
            else
            {
                conflict.Status = Domain.Entities.BookingStatus.PendingApproval;
                conflict.UpdatedAt = DateTime.UtcNow;
                conflict.Notes = AppendNote(conflict.Notes, $"[PENDING RESCHEDULE DUE TO EMERGENCY {createDto.StartAt:u}] {emergencyReason}");
                result.PendingResolution.Add(conflict.Id);

                _logger.LogInformation("Marked booking {BookingId} as pending resolution due to emergency override by user {UserId}.", conflict.Id, emergencyCreatorId);
            }
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
