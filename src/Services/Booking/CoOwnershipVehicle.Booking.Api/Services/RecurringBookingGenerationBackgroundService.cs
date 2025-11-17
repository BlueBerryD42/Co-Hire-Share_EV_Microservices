using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CoOwnershipVehicle.Booking.Api.Contracts;
using CoOwnershipVehicle.Booking.Api.Repositories;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Shared.Contracts.Events;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CoOwnershipVehicle.Booking.Api.Services;

public class RecurringBookingGenerationBackgroundService : BackgroundService
{
    private static readonly TimeSpan ExecutionInterval = TimeSpan.FromHours(24); // Run daily
    private static readonly TimeSpan GenerationHorizon = TimeSpan.FromDays(28); // Generate for next 4 weeks
    private static readonly TimeSpan LookBackWindow = TimeSpan.FromDays(7); // To catch any missed generations

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RecurringBookingGenerationBackgroundService> _logger;

    public RecurringBookingGenerationBackgroundService(IServiceScopeFactory scopeFactory, ILogger<RecurringBookingGenerationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Recurring booking generation background service starting with interval {Interval}", ExecutionInterval);

        using var timer = new PeriodicTimer(ExecutionInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessRecurringBookingsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing recurring bookings for generation");
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Recurring booking generation background service stopping");
    }

    private async Task ProcessRecurringBookingsAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var recurringBookingRepository = scope.ServiceProvider.GetRequiredService<IRecurringBookingRepository>();
        var bookingRepository = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var nowUtc = DateTime.UtcNow;
        var generationCutoff = nowUtc.Add(GenerationHorizon);
        var lookBackCutoff = nowUtc.Subtract(LookBackWindow);

        // Get active recurring bookings that need new bookings generated
        var recurringBookings = await recurringBookingRepository.GetRecurringBookingsToGenerateAsync(
            nowUtc,
            generationCutoff,
            lookBackCutoff,
            stoppingToken);

        if (!recurringBookings.Any())
        {
            _logger.LogDebug("No recurring bookings qualified for generation at {Timestamp:O}", nowUtc);
            return;
        }

        _logger.LogInformation("Processing {Count} recurring booking candidates for generation", recurringBookings.Count);

        foreach (var recurringBooking in recurringBookings)
        {
            stoppingToken.ThrowIfCancellationRequested();

            try
            {
                await GenerateBookingsForRecurringAsync(
                    recurringBooking,
                    bookingRepository,
                    recurringBookingRepository,
                    publishEndpoint,
                    nowUtc,
                    stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate bookings for recurring booking {RecurringBookingId}", recurringBooking.Id);
            }
        }

        await recurringBookingRepository.SaveChangesAsync(stoppingToken);
    }

    private async Task GenerateBookingsForRecurringAsync(
        RecurringBooking recurringBooking,
        IBookingRepository bookingRepository,
        IRecurringBookingRepository recurringBookingRepository,
        IPublishEndpoint publishEndpoint,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        // Determine the period for which bookings need to be generated
        // Start generating from the day after the last generated booking, or from recurrence start date if none generated yet
        var lastGeneratedUntil = recurringBooking.LastGeneratedUntilUtc ?? recurringBooking.RecurrenceStartDate.ToDateTime(TimeOnly.FromTimeSpan(recurringBooking.StartTime));
        var generationWindowStart = lastGeneratedUntil.Date < nowUtc.Date ? nowUtc.Date : lastGeneratedUntil.Date.AddDays(1);

        // The horizon is 4 weeks from nowUtc, capped by the recurrence end date
        var generationThrough = nowUtc.Add(GenerationHorizon);
        if (recurringBooking.RecurrenceEndDate.HasValue)
        {
            generationThrough = MinDateTime(generationThrough, recurringBooking.RecurrenceEndDate.Value.ToDateTime(TimeOnly.FromTimeSpan(recurringBooking.EndTime)));
        }

        // If generationThrough is before generationWindowStart, nothing to generate for this cycle
        if (generationThrough <= generationWindowStart)
        {
            _logger.LogDebug("No new bookings to generate for recurring booking {RecurringBookingId} in this cycle.", recurringBooking.Id);
            return;
        }

        var generateRequest = new CreateRecurringBookingDto
        {
            VehicleId = recurringBooking.VehicleId,
            Pattern = recurringBooking.Pattern,
            Interval = recurringBooking.Interval,
            DaysOfWeek = FromMask(recurringBooking.DaysOfWeekMask ?? 0).ToList(),
            StartTime = recurringBooking.StartTime,
            EndTime = recurringBooking.EndTime,
            RecurrenceStartDate = generationWindowStart,
            RecurrenceEndDate = generationThrough,
            Notes = recurringBooking.Notes,
            Purpose = recurringBooking.Purpose,
            TimeZoneId = recurringBooking.TimeZoneId
        };

        // Reuse the occurrence generation logic, but for a specific range
        var (occurrences, actualGenerationThrough) = await GenerateOccurrencesForBackgroundServiceAsync(
            generateRequest,
            generationWindowStart,
            generationThrough,
            bookingRepository,
            cancellationToken,
            recurringBooking.Id);

        var newBookings = new List<CoOwnershipVehicle.Domain.Entities.Booking>();
        foreach (var occurrence in occurrences)
        {
            if (occurrence.Conflicts.Any())
            {
                _logger.LogWarning("Skipping generation of booking for recurring booking {RecurringBookingId} due to conflict at {StartAt:O}",
                    recurringBooking.Id, occurrence.StartAt);
                // TODO: Notify user about skipped booking due to conflict
                continue; // Skip this conflicting occurrence
            }

            var booking = new CoOwnershipVehicle.Domain.Entities.Booking
            {
                Id = Guid.NewGuid(),
                VehicleId = recurringBooking.VehicleId,
                GroupId = recurringBooking.GroupId,
                UserId = recurringBooking.UserId,
                StartAt = occurrence.StartAt,
                EndAt = occurrence.EndAt,
                Notes = recurringBooking.Notes,
                Purpose = recurringBooking.Purpose,
                IsEmergency = false,
                Priority = await CalculateUserPriorityAsync(recurringBooking.UserId, recurringBooking.VehicleId, cancellationToken),
                Status = BookingStatus.Confirmed,
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc,
                RecurringBookingId = recurringBooking.Id
            };

            newBookings.Add(booking);
        }

        if (newBookings.Any())
        {
            await recurringBookingRepository.AddGeneratedBookingsAsync(newBookings, cancellationToken);
            recurringBooking.LastGeneratedUntilUtc = newBookings.Max(b => b.EndAt);
            recurringBooking.LastGenerationRunAtUtc = nowUtc;
            recurringBooking.UpdatedAt = nowUtc;
            _logger.LogInformation("Generated {Count} new bookings for recurring booking {RecurringBookingId}.", newBookings.Count, recurringBooking.Id);

            foreach (var booking in newBookings)
            {
                await publishEndpoint.Publish(new BookingCreatedEvent
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
        else
        {
            recurringBooking.LastGenerationRunAtUtc = nowUtc; // Update generation run timestamp even if no bookings generated
            recurringBooking.UpdatedAt = nowUtc;
        }
    }

    // Helper methods (copied and adapted from RecurringBookingService for self-containment)
    private async Task<(List<Occurrence> Occurrences, DateTime GenerationThrough)> GenerateOccurrencesForBackgroundServiceAsync(
        CreateRecurringBookingDto request,
        DateTime generationWindowStart,
        DateTime generationThrough,
        IBookingRepository bookingRepository,
        CancellationToken cancellationToken,
        Guid? excludeRecurringBookingId = null)
    {
        var occurrences = new List<Occurrence>();

        switch (request.Pattern)
        {
            case RecurrencePattern.Daily:
                GenerateDailyOccurrences(request, generationWindowStart, generationThrough, occurrences);
                break;
            case RecurrencePattern.Weekly:
                GenerateWeeklyOccurrences(request, generationWindowStart, generationThrough, occurrences);
                break;
            case RecurrencePattern.Monthly:
                GenerateMonthlyOccurrences(request, generationWindowStart, generationThrough, occurrences);
                break;
            default:
                throw new NotSupportedException($"Recurrence pattern {request.Pattern} is not supported.");
        }

        foreach (var occurrence in occurrences)
        {
            var conflicts = await bookingRepository.GetConflictingBookingsAsync(
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

    private Task<BookingPriority> CalculateUserPriorityAsync(Guid userId, Guid vehicleId, CancellationToken cancellationToken)
    {
        var hash = Math.Abs(HashCode.Combine(userId, vehicleId));
        var normalized = 25 + (hash % 75);

        var priority = normalized switch
        {
            >= 90 => BookingPriority.High,
            >= 60 => BookingPriority.Normal,
            _ => BookingPriority.Low
        };

        return Task.FromResult(priority);
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
            // This case should ideally be caught by initial validation for recurring booking creation
            return;
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

        // Adjust startDate if it's before the first day of the current month
        if (startDate.Day > dayOfMonth && startDate.Month == currentDate.Month && startDate.Year == currentDate.Year)
        {
            currentDate = currentDate.AddMonths(1); // Start from next month
        }

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
            // This should ideally be caught by initial validation for recurring booking creation
            return;
        }

        var startAt = DateTime.SpecifyKind(date.Date.Add(startTime), DateTimeKind.Utc);
        var endAt = DateTime.SpecifyKind(date.Date.Add(endTime), DateTimeKind.Utc);

        occurrences.Add(new Occurrence
        {
            StartAt = startAt,
            EndAt = endAt
        });
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

    private static IReadOnlyList<DayOfWeek> FromMask(int mask)
    {
        var days = new List<DayOfWeek>();
        for (var i = 0; i < 7; i++)
        {
            if ((mask & (1 << i)) != 0)
            {
                days.Add((DayOfWeek)i);
            }
        }

        return days;
    }

    private static DateTime MinDateTime(DateTime first, DateTime second) => first <= second ? first : second;

    private static DateTime MaxDateTime(DateTime first, DateTime second) => first >= second ? first : second;

    private sealed class Occurrence
    {
        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }
        public IReadOnlyList<CoOwnershipVehicle.Domain.Entities.Booking> Conflicts { get; set; } = Array.Empty<CoOwnershipVehicle.Domain.Entities.Booking>();
    }
}

// Note: This extension method should ideally be in a shared utility or framework project
internal static class DateTimeExtensions
{
    public static DateTime StartOfWeek(this DateTime date, DayOfWeek startOfWeek)
    {
        int diff = (7 + (date.DayOfWeek - startOfWeek)) % 7;
        return date.AddDays(-1 * diff).Date;
    }
}
