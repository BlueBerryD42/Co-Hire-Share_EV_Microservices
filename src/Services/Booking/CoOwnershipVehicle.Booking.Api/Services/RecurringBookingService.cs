using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CoOwnershipVehicle.Booking.Api.Contracts;
using CoOwnershipVehicle.Booking.Api.Repositories;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Shared.Contracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace CoOwnershipVehicle.Booking.Api.Services;

public class RecurringBookingService : IRecurringBookingService
{
    private static readonly TimeSpan GenerationHorizon = TimeSpan.FromDays(28);

    private readonly IRecurringBookingRepository _recurringBookingRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<RecurringBookingService> _logger;

    public RecurringBookingService(
        IRecurringBookingRepository recurringBookingRepository,
        IBookingRepository bookingRepository,
        IPublishEndpoint publishEndpoint,
        ILogger<RecurringBookingService> logger)
    {
        _recurringBookingRepository = recurringBookingRepository ?? throw new ArgumentNullException(nameof(recurringBookingRepository));
        _bookingRepository = bookingRepository ?? throw new ArgumentNullException(nameof(bookingRepository));
        _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RecurringBookingDto> CreateAsync(CreateRecurringBookingDto request, Guid userId, CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        ValidateRequest(request);

        if (!await _bookingRepository.UserHasVehicleAccessAsync(request.VehicleId, userId, cancellationToken))
        {
            throw new UnauthorizedAccessException("Access denied to this vehicle.");
        }

        var vehicle = await _bookingRepository.GetVehicleByIdAsync(request.VehicleId, cancellationToken)
                      ?? throw new InvalidOperationException("Vehicle not found.");

        if (vehicle.GroupId == null)
        {
            throw new InvalidOperationException("Vehicle is not associated with a group.");
        }

        var nowUtc = DateTime.UtcNow;
        var (occurrences, _) = await GenerateOccurrencesAsync(
            request,
            nowUtc,
            cancellationToken);

        if (occurrences.Any(o => o.Conflicts.Any()))
        {
            var conflict = occurrences.First(o => o.Conflicts.Any());
            throw new InvalidOperationException($"Recurring booking conflicts with existing booking starting at {conflict.StartAt:u}.");
        }

        var recurringBooking = new RecurringBooking
        {
            Id = Guid.NewGuid(),
            VehicleId = request.VehicleId,
            GroupId = vehicle.GroupId.Value,
            UserId = userId,
            Pattern = request.Pattern,
            Interval = request.Interval,
            DaysOfWeekMask = ToDaysOfWeekMask(request.DaysOfWeek),
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            RecurrenceStartDate = DateOnly.FromDateTime(request.RecurrenceStartDate),
            RecurrenceEndDate = request.RecurrenceEndDate.HasValue ? DateOnly.FromDateTime(request.RecurrenceEndDate.Value) : (DateOnly?)null,
            Status = RecurringBookingStatus.Active,
            Notes = request.Notes,
            Purpose = request.Purpose,
            TimeZoneId = string.IsNullOrWhiteSpace(request.TimeZoneId) ? null : request.TimeZoneId,
            LastGeneratedUntilUtc = occurrences.Any() ? occurrences.Max(o => o.EndAt) : (DateTime?)null,
            LastGenerationRunAtUtc = nowUtc,
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc
        };

        var bookings = new List<CoOwnershipVehicle.Domain.Entities.Booking>();

        foreach (var occurrence in occurrences)
        {
            var booking = new CoOwnershipVehicle.Domain.Entities.Booking
            {
                Id = Guid.NewGuid(),
                VehicleId = recurringBooking.VehicleId,
                GroupId = recurringBooking.GroupId,
                UserId = userId,
                StartAt = occurrence.StartAt,
                EndAt = occurrence.EndAt,
                Notes = request.Notes,
                Purpose = request.Purpose,
                IsEmergency = false,
                Priority = await CalculateUserPriorityAsync(userId, recurringBooking.VehicleId, cancellationToken),
                Status = BookingStatus.Confirmed,
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc,
                RecurringBookingId = recurringBooking.Id
            };

            bookings.Add(booking);
        }

        await _recurringBookingRepository.AddAsync(recurringBooking, cancellationToken);

        if (bookings.Count > 0)
        {
            await _recurringBookingRepository.AddGeneratedBookingsAsync(bookings, cancellationToken);
        }

        await _recurringBookingRepository.SaveChangesAsync(cancellationToken);

        if (bookings.Count > 0)
        {
            foreach (var booking in bookings)
            {
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
                }, cancellationToken);
            }
        }

        await _publishEndpoint.Publish(new RecurringBookingCreatedEvent
        {
            RecurringBookingId = recurringBooking.Id,
            VehicleId = recurringBooking.VehicleId,
            GroupId = recurringBooking.GroupId,
            UserId = recurringBooking.UserId,
            Pattern = recurringBooking.Pattern,
            Interval = recurringBooking.Interval,
            DaysOfWeek = FromMask(recurringBooking.DaysOfWeekMask),
            StartTime = recurringBooking.StartTime,
            EndTime = recurringBooking.EndTime,
            RecurrenceStartDate = recurringBooking.RecurrenceStartDate.ToDateTime(TimeOnly.MinValue),
            RecurrenceEndDate = recurringBooking.RecurrenceEndDate.HasValue ? recurringBooking.RecurrenceEndDate.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
            Status = recurringBooking.Status,
            GeneratedBookingIds = bookings.Select(b => b.Id).ToList()
        }, cancellationToken);

        _logger.LogInformation("Created recurring booking {RecurringBookingId} for user {UserId} with {OccurrenceCount} initial bookings.",
            recurringBooking.Id, userId, bookings.Count);

        return MapToDto(recurringBooking, bookings);
    }

    public async Task<RecurringBookingDto> UpdateAsync(Guid recurringBookingId, UpdateRecurringBookingDto request, Guid userId, CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var recurring = await _recurringBookingRepository.GetByIdAsync(recurringBookingId, includeGeneratedBookings: true, cancellationToken)
                        ?? throw new KeyNotFoundException("Recurring booking not found.");

        EnsureUserCanModify(recurring, userId);

        MergeUpdates(recurring, request);
        ValidateRecurring(recurring);

        var nowUtc = DateTime.UtcNow;
        var generationWindow = nowUtc.Add(GenerationHorizon);
        var endBoundary = recurring.RecurrenceEndDate.HasValue
            ? MinDateTime(generationWindow, recurring.RecurrenceEndDate.Value.ToDateTime(TimeOnly.MinValue).AddDays(1).AddTicks(-1))
            : generationWindow;

        var (occurrences, _) = await GenerateOccurrencesAsync(new CreateRecurringBookingDto
        {
            VehicleId = recurring.VehicleId,
            Pattern = recurring.Pattern,
            Interval = recurring.Interval,
            DaysOfWeek = FromMask(recurring.DaysOfWeekMask).ToList(),
            StartTime = recurring.StartTime,
            EndTime = recurring.EndTime,
            RecurrenceStartDate = MaxDateTime(nowUtc.Date, recurring.RecurrenceStartDate.ToDateTime(TimeOnly.MinValue)),
            RecurrenceEndDate = recurring.RecurrenceEndDate.HasValue ? recurring.RecurrenceEndDate.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
            Notes = recurring.Notes,
            Purpose = recurring.Purpose,
            TimeZoneId = recurring.TimeZoneId
        }, nowUtc, cancellationToken, recurring.Id);

        if (occurrences.Any(o => o.Conflicts.Any()))
        {
            var conflict = occurrences.First(o => o.Conflicts.Any());
            throw new InvalidOperationException($"Recurring booking update conflicts with booking starting at {conflict.StartAt:u}.");
        }

        // Cancel future generated bookings and replace with new set
        var futureBookings = recurring.GeneratedBookings
                .Where(b => b.StartAt >= nowUtc)
                .ToList();

        if (futureBookings.Count > 0)
        {
            foreach (var booking in futureBookings)
            {
                booking.Status = BookingStatus.Cancelled;
                booking.UpdatedAt = nowUtc;
            }
        }

        var newBookings = new List<CoOwnershipVehicle.Domain.Entities.Booking>();
        foreach (var occurrence in occurrences.Where(o => o.StartAt >= nowUtc && o.StartAt <= endBoundary))
        {
            if (recurring.GeneratedBookings.Any(b => b.StartAt == occurrence.StartAt && b.EndAt == occurrence.EndAt))
            {
                continue;
            }

            var booking = new CoOwnershipVehicle.Domain.Entities.Booking
            {
                Id = Guid.NewGuid(),
                VehicleId = recurring.VehicleId,
                GroupId = recurring.GroupId,
                UserId = recurring.UserId,
                StartAt = occurrence.StartAt,
                EndAt = occurrence.EndAt,
                Notes = recurring.Notes,
                Purpose = recurring.Purpose,
                IsEmergency = false,
                Priority = await CalculateUserPriorityAsync(recurring.UserId, recurring.VehicleId, cancellationToken),
                Status = BookingStatus.Confirmed,
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc,
                RecurringBookingId = recurring.Id
            };

            newBookings.Add(booking);
        }

        recurring.LastGeneratedUntilUtc = occurrences.Any()
            ? occurrences.Max(o => o.EndAt)
            : recurring.LastGeneratedUntilUtc;
        recurring.LastGenerationRunAtUtc = nowUtc;
        recurring.UpdatedAt = nowUtc;

        if (newBookings.Count > 0)
        {
            await _recurringBookingRepository.AddGeneratedBookingsAsync(newBookings, cancellationToken);
        }

        await _recurringBookingRepository.SaveChangesAsync(cancellationToken);

        if (newBookings.Count > 0)
        {
            foreach (var booking in newBookings)
            {
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
                }, cancellationToken);
            }
        }

        await _publishEndpoint.Publish(new RecurringBookingUpdatedEvent
        {
            RecurringBookingId = recurring.Id,
            Pattern = recurring.Pattern,
            Interval = recurring.Interval,
            DaysOfWeek = FromMask(recurring.DaysOfWeekMask),
            StartTime = recurring.StartTime,
            EndTime = recurring.EndTime,
            RecurrenceStartDate = recurring.RecurrenceStartDate.ToDateTime(TimeOnly.MinValue),
            RecurrenceEndDate = recurring.RecurrenceEndDate.HasValue ? recurring.RecurrenceEndDate.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
            Status = recurring.Status,
            UpdatedAt = recurring.UpdatedAt
        }, cancellationToken);

        _logger.LogInformation("Updated recurring booking {RecurringBookingId} by user {UserId}. Generated {Count} new bookings.", recurring.Id, userId, newBookings.Count);

        var refreshed = await _recurringBookingRepository.GetByIdAsync(recurringBookingId, includeGeneratedBookings: true, cancellationToken);
        return MapToDto(refreshed!, refreshed!.GeneratedBookings);
    }

    public async Task CancelAsync(Guid recurringBookingId, Guid userId, string? reason = null, CancellationToken cancellationToken = default)
    {
        var recurring = await _recurringBookingRepository.GetByIdAsync(recurringBookingId, includeGeneratedBookings: true, cancellationToken)
                        ?? throw new KeyNotFoundException("Recurring booking not found.");

        EnsureUserCanModify(recurring, userId);

        if (recurring.Status == RecurringBookingStatus.Ended)
        {
            return;
        }

        var nowUtc = DateTime.UtcNow;

        // Cancel future bookings
        var futureBookings = recurring.GeneratedBookings.Where(b => b.StartAt >= nowUtc).ToList();
        foreach (var booking in futureBookings)
        {
            booking.Status = BookingStatus.Cancelled;
            booking.UpdatedAt = nowUtc;
        }

        recurring.Status = RecurringBookingStatus.Ended;
        recurring.CancellationReason = reason;
        recurring.CancelledAtUtc = nowUtc;
        recurring.UpdatedAt = nowUtc;

        await _recurringBookingRepository.SaveChangesAsync(cancellationToken);

        if (futureBookings.Count > 0)
        {
            foreach (var booking in futureBookings)
            {
                await _publishEndpoint.Publish(new BookingCancelledEvent
                {
                    BookingId = booking.Id,
                    VehicleId = booking.VehicleId,
                    UserId = booking.UserId,
                    StartAt = booking.StartAt,
                    EndAt = booking.EndAt,
                    CancelledBy = userId,
                    Reason = "Cancelled with recurring series",
                    CancellationReason = reason
                }, cancellationToken);
            }
        }

        await _publishEndpoint.Publish(new RecurringBookingStatusChangedEvent
        {
            RecurringBookingId = recurring.Id,
            Status = recurring.Status,
            ChangedBy = userId,
            ChangedAt = nowUtc,
            Reason = reason
        }, cancellationToken);

        _logger.LogInformation("Cancelled recurring booking {RecurringBookingId} by user {UserId}.", recurringBookingId, userId);
    }

    public async Task<RecurringBookingDto> GetByIdAsync(Guid recurringBookingId, Guid userId, CancellationToken cancellationToken = default)
    {
        var recurring = await _recurringBookingRepository.GetByIdAsync(recurringBookingId, includeGeneratedBookings: true, cancellationToken)
                        ?? throw new KeyNotFoundException("Recurring booking not found.");

        EnsureUserHasVisibility(recurring, userId);

        return MapToDto(recurring, recurring.GeneratedBookings);
    }

    public async Task<IReadOnlyList<RecurringBookingDto>> GetUserRecurringBookingsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var recurringBookings = await _recurringBookingRepository.GetByUserAsync(userId, cancellationToken);
        return recurringBookings.Select(rb => MapToDto(rb, Array.Empty<CoOwnershipVehicle.Domain.Entities.Booking>())).ToList();
    }

    private async Task<(List<Occurrence> Occurrences, DateTime GenerationThrough)> GenerateOccurrencesAsync(
        CreateRecurringBookingDto request,
        DateTime nowUtc,
        CancellationToken cancellationToken,
        Guid? excludeRecurringBookingId = null)
    {
        var occurrences = new List<Occurrence>();
        var startDate = request.RecurrenceStartDate.Date;
        if (startDate < nowUtc.Date)
        {
            startDate = nowUtc.Date;
        }

        var endDateLimit = request.RecurrenceEndDate?.Date ?? DateTime.MaxValue.Date;
        var generationThrough = MinDateTime(startDate.Add(GenerationHorizon), endDateLimit);

        switch (request.Pattern)
        {
            case RecurrencePattern.Daily:
                GenerateDailyOccurrences(request, startDate, generationThrough, occurrences);
                break;
            case RecurrencePattern.Weekly:
                GenerateWeeklyOccurrences(request, startDate, generationThrough, occurrences);
                break;
            case RecurrencePattern.Monthly:
                GenerateMonthlyOccurrences(request, startDate, generationThrough, occurrences);
                break;
            default:
                throw new NotSupportedException($"Recurrence pattern {request.Pattern} is not supported.");
        }

        foreach (var occurrence in occurrences)
        {
            var conflicts = await _bookingRepository.GetConflictingBookingsAsync(
                request.VehicleId,
                occurrence.StartAt,
                occurrence.EndAt,
                null,
                excludeRecurringBookingId,
                cancellationToken);

            occurrence.Conflicts = conflicts;
        }

        return (occurrences, generationThrough);
    }

    private async Task<BookingPriority> CalculateUserPriorityAsync(Guid userId, Guid vehicleId, CancellationToken cancellationToken)
    {
        var member = await _bookingRepository.GetMemberForVehicleAsync(userId, vehicleId, cancellationToken);
        if (member == null)
        {
            return BookingPriority.Normal;
        }

        var basePriority = (int)(member.SharePercentage * 100);
        var rolePriority = member.RoleInGroup == GroupRole.Admin ? 50 : 0;
        var combined = basePriority + rolePriority;

        return combined switch
        {
            >= 200 => BookingPriority.Emergency,
            >= 150 => BookingPriority.High,
            >= 50 => BookingPriority.Normal,
            _ => BookingPriority.Low
        };
    }

    private static void GenerateDailyOccurrences(CreateRecurringBookingDto request, DateTime startDate, DateTime generationThrough, List<Occurrence> output)
    {
        var current = startDate;
        while (current <= generationThrough)
        {
            AddOccurrence(output, current, request.StartTime, request.EndTime);
            current = current.AddDays(request.Interval);
        }
    }

    private static void GenerateWeeklyOccurrences(CreateRecurringBookingDto request, DateTime startDate, DateTime generationThrough, List<Occurrence> output)
    {
        var days = request.DaysOfWeek?.Distinct().OrderBy(d => d).ToList() ?? new List<DayOfWeek>();
        if (!days.Any())
        {
            throw new InvalidOperationException("DaysOfWeek is required for weekly recurring bookings.");
        }

        var currentWeekStart = DateTimeExtensions.StartOfWeek(startDate, DayOfWeek.Sunday);
        while (currentWeekStart <= generationThrough)
        {
            foreach (var day in days)
            {
                var date = currentWeekStart.AddDays((int)day);
                if (date < startDate || date > generationThrough)
                {
                    continue;
                }

                AddOccurrence(output, date, request.StartTime, request.EndTime);
            }

            currentWeekStart = currentWeekStart.AddDays(7 * request.Interval);
        }
    }

    private static void GenerateMonthlyOccurrences(CreateRecurringBookingDto request, DateTime startDate, DateTime generationThrough, List<Occurrence> output)
    {
        var dayOfMonth = startDate.Day;
        var currentDate = new DateTime(startDate.Year, startDate.Month, 1);

        while (currentDate <= generationThrough)
        {
            var targetDay = Math.Min(dayOfMonth, DateTime.DaysInMonth(currentDate.Year, currentDate.Month));
            var date = new DateTime(currentDate.Year, currentDate.Month, targetDay);
            if (date >= startDate && date <= generationThrough)
            {
                AddOccurrence(output, date, request.StartTime, request.EndTime);
            }

            currentDate = currentDate.AddMonths(request.Interval);
        }
    }

    private static void AddOccurrence(List<Occurrence> occurrences, DateTime date, TimeSpan startTime, TimeSpan endTime)
    {
        if (endTime <= startTime)
        {
            throw new InvalidOperationException("EndTime must be after StartTime for recurring bookings.");
        }

        var startAt = DateTime.SpecifyKind(date.Date.Add(startTime), DateTimeKind.Utc);
        var endAt = DateTime.SpecifyKind(date.Date.Add(endTime), DateTimeKind.Utc);

        occurrences.Add(new Occurrence
        {
            StartAt = startAt,
            EndAt = endAt
        });
    }

    private static void MergeUpdates(RecurringBooking recurring, UpdateRecurringBookingDto request)
    {
        if (request.Pattern.HasValue)
        {
            recurring.Pattern = request.Pattern.Value;
        }

        if (request.Interval.HasValue)
        {
            recurring.Interval = request.Interval.Value;
        }

        if (request.DaysOfWeek != null)
        {
            recurring.DaysOfWeekMask = ToDaysOfWeekMask(request.DaysOfWeek);
        }

        if (request.StartTime.HasValue)
        {
            recurring.StartTime = request.StartTime.Value;
        }

        if (request.EndTime.HasValue)
        {
            recurring.EndTime = request.EndTime.Value;
        }

        if (request.RecurrenceStartDate.HasValue)
        {
            recurring.RecurrenceStartDate = DateOnly.FromDateTime(request.RecurrenceStartDate.Value);
        }

        if (request.RecurrenceEndDate.HasValue)
        {
            recurring.RecurrenceEndDate = DateOnly.FromDateTime(request.RecurrenceEndDate.Value);
        }

        if (request.Status.HasValue)
        {
            recurring.Status = request.Status.Value;
        }

        if (request.PausedUntilUtc.HasValue)
        {
            recurring.PausedUntilUtc = request.PausedUntilUtc;
        }

        if (request.Notes != null)
        {
            recurring.Notes = request.Notes;
        }

        if (request.Purpose != null)
        {
            recurring.Purpose = request.Purpose;
        }

        if (request.TimeZoneId != null)
        {
            recurring.TimeZoneId = string.IsNullOrWhiteSpace(request.TimeZoneId) ? null : request.TimeZoneId;
        }
    }

    private static void ValidateRequest(CreateRecurringBookingDto request)
    {
        if (request.EndTime <= request.StartTime)
        {
            throw new InvalidOperationException("EndTime must be after StartTime.");
        }

        if (request.RecurrenceEndDate.HasValue && request.RecurrenceEndDate.Value.Date < request.RecurrenceStartDate.Date)
        {
            throw new InvalidOperationException("RecurrenceEndDate must be on or after RecurrenceStartDate.");
        }

        if (request.Pattern == RecurrencePattern.Weekly && (request.DaysOfWeek == null || !request.DaysOfWeek.Any()))
        {
            throw new InvalidOperationException("DaysOfWeek must be provided for weekly recurring bookings.");
        }
    }

    private static void ValidateRecurring(RecurringBooking recurring)
    {
        if (recurring.EndTime <= recurring.StartTime)
        {
            throw new InvalidOperationException("EndTime must be after StartTime.");
        }

        if (recurring.RecurrenceEndDate.HasValue && recurring.RecurrenceEndDate.Value < recurring.RecurrenceStartDate)
        {
            throw new InvalidOperationException("RecurrenceEndDate must be on or after RecurrenceStartDate.");
        }

        if (recurring.Pattern == RecurrencePattern.Weekly && recurring.DaysOfWeekMask == 0)
        {
            throw new InvalidOperationException("DaysOfWeek must be provided for weekly recurring bookings.");
        }
    }

    private static int ToDaysOfWeekMask(IEnumerable<DayOfWeek>? days)
    {
        if (days == null)
        {
            return 0;
        }

        var mask = 0;
        foreach (var day in days.Distinct())
        {
            mask |= 1 << (int)day;
        }

        return mask;
    }

    private static IReadOnlyList<DayOfWeek> FromMask(int? mask)
    {
        var days = new List<DayOfWeek>();
        if (!mask.HasValue)
        {
            return days;
        }

        var m = mask.Value;
        for (var i = 0; i < 7; i++)
        {
            if ((m & (1 << i)) != 0)
            {
                days.Add((DayOfWeek)i);
            }
        }

        return days;
    }

    private static DateTime MinDateTime(DateTime first, DateTime second) => first <= second ? first : second;

    private static DateTime MaxDateTime(DateTime first, DateTime second) => first >= second ? first : second;

    private static void EnsureUserCanModify(RecurringBooking recurring, Guid userId)
    {
        if (recurring.UserId != userId)
        {
            throw new UnauthorizedAccessException("You do not have permission to modify this recurring booking.");
        }
    }

    private static void EnsureUserHasVisibility(RecurringBooking recurring, Guid userId)
    {
        if (recurring.UserId != userId)
        {
            throw new UnauthorizedAccessException("You do not have permission to view this recurring booking.");
        }
    }

    private static RecurringBookingDto MapToDto(RecurringBooking recurring, IEnumerable<CoOwnershipVehicle.Domain.Entities.Booking> generatedBookings)
    {
        return new RecurringBookingDto
        {
            Id = recurring.Id,
            VehicleId = recurring.VehicleId,
            GroupId = recurring.GroupId,
            UserId = recurring.UserId,
            Pattern = recurring.Pattern,
            Interval = recurring.Interval,
            DaysOfWeek = FromMask(recurring.DaysOfWeekMask),
            StartTime = recurring.StartTime,
            EndTime = recurring.EndTime,
            RecurrenceStartDate = recurring.RecurrenceStartDate.ToDateTime(TimeOnly.MinValue),
            RecurrenceEndDate = recurring.RecurrenceEndDate.HasValue ? recurring.RecurrenceEndDate.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
            Status = recurring.Status,
            PausedUntilUtc = recurring.PausedUntilUtc,
            LastGeneratedUntilUtc = recurring.LastGeneratedUntilUtc,
            LastGenerationRunAtUtc = recurring.LastGenerationRunAtUtc,
            Notes = recurring.Notes,
            Purpose = recurring.Purpose,
            TimeZoneId = recurring.TimeZoneId,
            CancellationReason = recurring.CancellationReason,
            CreatedAt = recurring.CreatedAt,
            UpdatedAt = recurring.UpdatedAt
        };
    }

    private sealed class Occurrence
    {
        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }
        public IReadOnlyList<CoOwnershipVehicle.Domain.Entities.Booking> Conflicts { get; set; } = Array.Empty<CoOwnershipVehicle.Domain.Entities.Booking>();
    }
}
