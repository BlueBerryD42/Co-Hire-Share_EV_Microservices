using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CoOwnershipVehicle.Booking.Api.Contracts;
using CoOwnershipVehicle.Booking.Api.Repositories;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Shared.Contracts.Events;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CoOwnershipVehicle.Booking.Api.Services;

public class RecurringBookingGenerationService : BackgroundService
{
    private static readonly TimeSpan ExecutionInterval = TimeSpan.FromHours(24);
    private static readonly TimeSpan GenerationHorizon = TimeSpan.FromDays(28);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RecurringBookingGenerationService> _logger;

    public RecurringBookingGenerationService(IServiceScopeFactory scopeFactory, ILogger<RecurringBookingGenerationService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Recurring booking generation service starting. Interval {Interval}", ExecutionInterval);

        await ProcessAsync(stoppingToken);

        using var timer = new PeriodicTimer(ExecutionInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await ProcessAsync(stoppingToken);
        }

        _logger.LogInformation("Recurring booking generation service stopping.");
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var recurringRepository = scope.ServiceProvider.GetRequiredService<IRecurringBookingRepository>();
        var bookingRepository = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var nowUtc = DateTime.UtcNow;
        var targetThrough = nowUtc.Date.Add(GenerationHorizon);

        var recurringBookings = await recurringRepository.GetActiveAsync(targetThrough, cancellationToken);
        if (!recurringBookings.Any())
        {
            _logger.LogDebug("No recurring bookings require generation at {Timestamp:O}", nowUtc);
            return;
        }

        _logger.LogInformation("Processing {Count} recurring booking series for future generation.", recurringBookings.Count);

        foreach (var recurring in recurringBookings)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await GenerateForRecurringAsync(recurring, nowUtc, targetThrough, recurringRepository, bookingRepository, publishEndpoint, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating bookings for recurring booking {RecurringBookingId}", recurring.Id);
            }
        }

        await recurringRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task GenerateForRecurringAsync(
        RecurringBooking recurring,
        DateTime nowUtc,
        DateTime targetThrough,
        IRecurringBookingRepository recurringRepository,
        IBookingRepository bookingRepository,
        IPublishEndpoint publishEndpoint,
        CancellationToken cancellationToken)
    {
        if (recurring.Status == RecurringBookingStatus.Paused && recurring.PausedUntilUtc.HasValue && recurring.PausedUntilUtc.Value > nowUtc)
        {
            _logger.LogDebug("Skipping recurring booking {RecurringBookingId} because it is paused until {PausedUntil:O}.", recurring.Id, recurring.PausedUntilUtc);
            return;
        }

        var generationStartUtc = recurring.LastGeneratedUntilUtc.HasValue
            ? recurring.LastGeneratedUntilUtc.Value.AddMinutes(1)
            : DateTime.SpecifyKind(recurring.RecurrenceStartDate.ToDateTime(TimeOnly.FromTimeSpan(recurring.StartTime)), DateTimeKind.Utc);

        if (generationStartUtc > targetThrough)
        {
            return;
        }

        if (recurring.RecurrenceEndDate.HasValue && generationStartUtc > recurring.RecurrenceEndDate.Value.ToDateTime(TimeOnly.FromTimeSpan(recurring.StartTime)))
        {
            return;
        }

        var endBoundary = recurring.RecurrenceEndDate.HasValue
            ? MinDateTime(targetThrough, recurring.RecurrenceEndDate.Value.ToDateTime(TimeOnly.FromTimeSpan(recurring.EndTime)))
            : targetThrough;

        var occurrences = BuildOccurrences(
            recurring.Pattern,
            recurring.Interval,
            FromMask(recurring.DaysOfWeekMask ?? 0),
            recurring.StartTime,
            recurring.EndTime,
            generationStartUtc.Date,
            endBoundary);

        var newBookings = new List<CoOwnershipVehicle.Domain.Entities.Booking>();
        var skippedDueToConflicts = new List<(DateTime StartAt, IReadOnlyList<CoOwnershipVehicle.Domain.Entities.Booking> Conflicts)>();

        foreach (var occurrence in occurrences)
        {
            if (occurrence.StartAt <= (recurring.LastGeneratedUntilUtc ?? DateTime.MinValue))
            {
                continue;
            }

            if (occurrence.StartAt < nowUtc)
            {
                continue;
            }

            var conflicts = await bookingRepository.GetConflictingBookingsAsync(
                recurring.VehicleId,
                occurrence.StartAt,
                occurrence.EndAt,
                null,
                recurring.Id,
                cancellationToken);

            if (conflicts.Any())
            {
                skippedDueToConflicts.Add((occurrence.StartAt, conflicts));
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
                Priority = BookingPriority.Normal,
                Status = BookingStatus.Confirmed,
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc,
                RecurringBookingId = recurring.Id
            };

            newBookings.Add(booking);
        }

        if (newBookings.Count == 0 && skippedDueToConflicts.Count == 0)
        {
            return;
        }

        foreach (var booking in newBookings)
        {
            booking.Priority = await CalculateUserPriorityAsync(recurring.UserId, recurring.VehicleId, bookingRepository, cancellationToken);
        }

        if (newBookings.Count > 0)
        {
            await recurringRepository.AddGeneratedBookingsAsync(newBookings, cancellationToken);
            recurring.LastGeneratedUntilUtc = newBookings.Max(b => b.EndAt);
            recurring.LastGenerationRunAtUtc = nowUtc;

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

            _logger.LogInformation("Generated {Count} bookings for recurring booking {RecurringBookingId}.", newBookings.Count, recurring.Id);
        }

        if (skippedDueToConflicts.Count > 0)
        {
            await NotifyConflictsAsync(recurring, skippedDueToConflicts, publishEndpoint, cancellationToken);
        }
    }

    private static async Task NotifyConflictsAsync(
        RecurringBooking recurring,
    IEnumerable<(DateTime StartAt, IReadOnlyList<CoOwnershipVehicle.Domain.Entities.Booking> Conflicts)> conflicts,
        IPublishEndpoint publishEndpoint,
        CancellationToken cancellationToken)
    {
        var upcomingConflicts = conflicts.Take(3).ToList();
        var conflictDescriptions = string.Join(
            Environment.NewLine,
            upcomingConflicts.Select(c => $"- {c.StartAt:u} conflicts with {c.Conflicts.Count} existing booking(s)"));

        var message = $"We could not create one or more occurrences for your recurring booking starting at {upcomingConflicts.First().StartAt:u} due to conflicts:{Environment.NewLine}{conflictDescriptions}";

        await publishEndpoint.Publish(new BulkNotificationEvent
        {
            UserIds = new List<Guid> { recurring.UserId },
            GroupId = recurring.GroupId,
            Title = "Recurring booking conflict detected",
            Message = message,
            Type = "RecurringBookingConflict",
            Priority = "High",
            CreatedAt = DateTime.UtcNow,
            ActionUrl = $"/bookings/recurring/{recurring.Id}",
            ActionText = "View recurring booking"
        }, cancellationToken);
    }

    private static List<(DateTime StartAt, DateTime EndAt)> BuildOccurrences(
        RecurrencePattern pattern,
        int interval,
        IReadOnlyList<DayOfWeek> daysOfWeek,
        TimeSpan startTime,
        TimeSpan endTime,
        DateTime startDate,
        DateTime generationThrough)
    {
        var occurrences = new List<(DateTime, DateTime)>();

        switch (pattern)
        {
            case RecurrencePattern.Daily:
                {
                    var current = startDate;
                    while (current <= generationThrough)
                    {
                        AddOccurrence(occurrences, current, startTime, endTime);
                        current = current.AddDays(interval);
                    }

                    break;
                }
            case RecurrencePattern.Weekly:
                {
                    var days = daysOfWeek.Any()
                        ? daysOfWeek.Distinct().OrderBy(d => d).ToList()
                        : throw new InvalidOperationException("DaysOfWeek is required for weekly recurring bookings.");

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

                            AddOccurrence(occurrences, date, startTime, endTime);
                        }

                        currentWeekStart = currentWeekStart.AddDays(7 * interval);
                    }

                    break;
                }
            case RecurrencePattern.Monthly:
                {
                    var dayOfMonth = startDate.Day;
                    var currentDate = new DateTime(startDate.Year, startDate.Month, 1);
                    while (currentDate <= generationThrough)
                    {
                        var targetDay = Math.Min(dayOfMonth, DateTime.DaysInMonth(currentDate.Year, currentDate.Month));
                        var date = new DateTime(currentDate.Year, currentDate.Month, targetDay);
                        if (date >= startDate && date <= generationThrough)
                        {
                            AddOccurrence(occurrences, date, startTime, endTime);
                        }

                        currentDate = currentDate.AddMonths(interval);
                    }

                    break;
                }
            default:
                throw new NotSupportedException($"Recurrence pattern {pattern} is not supported.");
        }

        return occurrences;
    }

    private static void AddOccurrence(List<(DateTime StartAt, DateTime EndAt)> occurrences, DateTime date, TimeSpan startTime, TimeSpan endTime)
    {
        if (endTime <= startTime)
        {
            throw new InvalidOperationException("EndTime must be after StartTime.");
        }

        var startAt = DateTime.SpecifyKind(date.Date.Add(startTime), DateTimeKind.Utc);
        var endAt = DateTime.SpecifyKind(date.Date.Add(endTime), DateTimeKind.Utc);
        occurrences.Add((startAt, endAt));
    }

    private static async Task<BookingPriority> CalculateUserPriorityAsync(Guid userId, Guid vehicleId, IBookingRepository bookingRepository, CancellationToken cancellationToken)
    {
        var member = await bookingRepository.GetMemberForVehicleAsync(userId, vehicleId, cancellationToken);
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

    private static DateTime MinDateTime(DateTime first, DateTime second) => first <= second ? first : second;

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
}

internal static class RecurringDateExtensions
{
    public static DateTime StartOfWeek(this DateTime date, DayOfWeek startOfWeek)
    {
        int diff = (7 + (date.DayOfWeek - startOfWeek)) % 7;
        return date.AddDays(-1 * diff).Date;
    }
}
