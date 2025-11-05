using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CoOwnershipVehicle.Booking.Api.Contracts;
using CoOwnershipVehicle.Booking.Api.DTOs;
using CoOwnershipVehicle.Booking.Api.Repositories;
using CoOwnershipVehicle.Domain.Entities;
using BookingEntity = CoOwnershipVehicle.Domain.Entities.Booking;
using CoOwnershipVehicle.Shared.Contracts.Events;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CoOwnershipVehicle.Booking.Api.Services;

public class BookingReminderBackgroundService : BackgroundService
{
    private static readonly TimeSpan ExecutionInterval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ThirtyMinuteWindowOffset = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan ThirtyMinuteWindowTolerance = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan FiveMinuteWindowOffset = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan FiveMinuteWindowTolerance = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan MissedWindowLowerBound = TimeSpan.FromMinutes(45);
    private static readonly TimeSpan MissedWindowUpperBound = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookingReminderBackgroundService> _logger;

    public BookingReminderBackgroundService(IServiceScopeFactory scopeFactory, ILogger<BookingReminderBackgroundService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Booking reminder background service starting with interval {Interval}", ExecutionInterval);

        using var timer = new PeriodicTimer(ExecutionInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessRemindersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing booking reminders");
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

        _logger.LogInformation("Booking reminder background service stopping");
    }

    private async Task ProcessRemindersAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var bookingRepository = scope.ServiceProvider.GetRequiredService<IBookingRepository>();
        var preferenceService = scope.ServiceProvider.GetRequiredService<INotificationPreferenceService>();
        var qrCodeService = scope.ServiceProvider.GetRequiredService<IQrCodeService>();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var nowUtc = DateTime.UtcNow;
        var thirtyWindowStart = nowUtc + ThirtyMinuteWindowOffset - ThirtyMinuteWindowTolerance;
        var thirtyWindowEnd = nowUtc + ThirtyMinuteWindowOffset + ThirtyMinuteWindowTolerance;
        var fiveWindowStart = nowUtc + FiveMinuteWindowOffset - FiveMinuteWindowTolerance;
        var fiveWindowEnd = nowUtc + FiveMinuteWindowOffset + FiveMinuteWindowTolerance;
        var missedWindowStart = nowUtc - MissedWindowLowerBound;
        var missedWindowEnd = nowUtc - MissedWindowUpperBound;

        var candidates = await bookingRepository.GetBookingsForReminderProcessingAsync(
            thirtyWindowStart,
            thirtyWindowEnd,
            fiveWindowStart,
            fiveWindowEnd,
            missedWindowStart,
            missedWindowEnd,
            stoppingToken);

        if (!candidates.Any())
        {
            _logger.LogDebug("No bookings qualified for reminders at {Timestamp:O}", nowUtc);
            return;
        }

        _logger.LogInformation("Processing {Count} booking reminder candidates", candidates.Count);

        foreach (var booking in candidates)
        {
            stoppingToken.ThrowIfCancellationRequested();

            try
            {
                await ProcessBookingReminderAsync(
                    booking,
                    preferenceService,
                    qrCodeService,
                    publishEndpoint,
                    nowUtc,
                    stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process reminder for booking {BookingId}", booking.Id);
            }
        }

        await bookingRepository.SaveChangesAsync(stoppingToken);
    }

    private async Task ProcessBookingReminderAsync(
        BookingEntity booking,
        INotificationPreferenceService preferenceService,
        IQrCodeService qrCodeService,
        IPublishEndpoint publishEndpoint,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var reminderType = DetermineReminderType(booking, nowUtc);
        if (reminderType == ReminderType.None)
        {
            return;
        }

        var preferences = await preferenceService.GetAsync(booking.UserId, cancellationToken);
        if (!preferences.EnableReminders)
        {
            _logger.LogDebug("User {UserId} has disabled booking reminders", booking.UserId);
            SetReminderSentTimestamp(booking, reminderType, nowUtc);
            return;
        }

        if (!preferences.EnableEmail && !preferences.EnableSms)
        {
            _logger.LogDebug("User {UserId} disabled both email and SMS reminders", booking.UserId);
            SetReminderSentTimestamp(booking, reminderType, nowUtc);
            return;
        }

        var qrCode = await qrCodeService.GetVehicleQrCodeAsync(booking.VehicleId, booking.UserId, cancellationToken);
        var reminder = BuildReminderPayload(booking, preferences, reminderType, qrCode, nowUtc);

        if (preferences.EnableEmail)
        {
            await publishEndpoint.Publish(reminder.CreateEvent(ReminderDeliveryChannel.Email), cancellationToken);
        }

        if (preferences.EnableSms)
        {
            await publishEndpoint.Publish(reminder.CreateEvent(ReminderDeliveryChannel.Sms), cancellationToken);
        }

        SetReminderSentTimestamp(booking, reminderType, nowUtc);

        _logger.LogInformation(
            "Queued {ReminderType} reminder for booking {BookingId} at {Timestamp:O}",
            reminderType,
            booking.Id,
            nowUtc);
    }

    private static ReminderType DetermineReminderType(BookingEntity booking, DateTime nowUtc)
    {
        if (booking.Status != BookingStatus.Confirmed)
        {
            return ReminderType.None;
        }

        var startAtUtc = booking.StartAt;
        var untilStart = startAtUtc - nowUtc;

        if (booking.PreCheckoutReminderSentAt == null && Math.Abs(untilStart.TotalMinutes - 30) <= ThirtyMinuteWindowTolerance.TotalMinutes)
        {
            return ReminderType.PreCheck;
        }

        if (booking.FinalCheckoutReminderSentAt == null && Math.Abs(untilStart.TotalMinutes - 5) <= FiveMinuteWindowTolerance.TotalMinutes)
        {
            return ReminderType.FinalCall;
        }

        if (booking.MissedCheckoutReminderSentAt == null && nowUtc - startAtUtc >= MissedWindowUpperBound && nowUtc - startAtUtc <= MissedWindowLowerBound)
        {
            return ReminderType.Missed;
        }

        return ReminderType.None;
    }

    private static void SetReminderSentTimestamp(BookingEntity booking, ReminderType type, DateTime timestampUtc)
    {
        switch (type)
        {
            case ReminderType.PreCheck:
                booking.PreCheckoutReminderSentAt = timestampUtc;
                break;
            case ReminderType.FinalCall:
                booking.FinalCheckoutReminderSentAt = timestampUtc;
                break;
            case ReminderType.Missed:
                booking.MissedCheckoutReminderSentAt = timestampUtc;
                break;
        }
    }

    private static ReminderNotification BuildReminderPayload(
        BookingEntity booking,
        BookingNotificationPreferenceDto preferences,
        ReminderType reminderType,
        VehicleQrCodeResult qrCode,
        DateTime nowUtc)
    {
        var vehicle = booking.Vehicle;
        var user = booking.User;
        var timeZone = ResolveTimeZone(preferences.PreferredTimeZoneId);
        var startLocal = TimeZoneInfo.ConvertTimeFromUtc(booking.StartAt, timeZone);

        var title = reminderType switch
        {
            ReminderType.PreCheck => "Your booking starts soon",
            ReminderType.FinalCall => "Time to check out",
            ReminderType.Missed => "Did you forget to check out?",
            _ => "Vehicle booking reminder"
        };

        var actionUrl = $"/bookings/{booking.Id}/checkout";
        var builder = new StringBuilder();
        builder.AppendLine($"Hi {user?.FirstName ?? "there"},");
        builder.AppendLine();

        if (reminderType == ReminderType.Missed)
        {
            builder.AppendLine("Your booking has already started and we didn't detect a vehicle check-out.");
        }
        else if (reminderType == ReminderType.FinalCall)
        {
            builder.AppendLine("It's time to pick up your shared vehicle.");
        }
        else
        {
            builder.AppendLine("Your booking is coming up shortly—please get ready to check out.");
        }

        builder.AppendLine();
        builder.AppendLine($"Vehicle: {vehicle?.Model ?? "Vehicle"} ({vehicle?.PlateNumber ?? "Unknown plate"})");
        builder.AppendLine($"Start time ({timeZone.StandardName}): {startLocal:MMM d, yyyy h:mm tt}");
        builder.AppendLine("Location: Check the app for the latest vehicle location details.");
        builder.AppendLine("Instructions: Use the link below to review check-out steps and scan the QR code to unlock.");
        builder.AppendLine();
        builder.AppendLine($"Check-out link: {actionUrl}");
        builder.AppendLine($"QR code (copy into browser if needed): {qrCode.DataUrl}");

        if (reminderType == ReminderType.Missed)
        {
            builder.AppendLine();
            builder.AppendLine("If you've already started the trip, please complete the check-out in the app so other members stay informed.");
        }

        var emailMessage = builder.ToString();
        var smsMessage = $"{title}: {vehicle?.Model ?? "Vehicle"} ({vehicle?.PlateNumber ?? "plate"}) starts {startLocal:MMM d h:mm tt} {timeZone.StandardName}. Link: {actionUrl}";

        return new ReminderNotification
        {
            BookingId = booking.Id,
            UserId = booking.UserId,
            GroupId = booking.GroupId,
            Title = title,
            EmailMessage = emailMessage,
            SmsMessage = smsMessage,
            ActionUrl = actionUrl,
            ActionText = "Open check-out",
            VehicleId = booking.VehicleId,
            ReminderType = reminderType
        };
    }

    private static TimeZoneInfo ResolveTimeZone(string? preferredId)
    {
        if (!string.IsNullOrWhiteSpace(preferredId))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(preferredId);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }

    private enum ReminderType
    {
        None,
        PreCheck,
        FinalCall,
        Missed
    }

    private enum ReminderDeliveryChannel
    {
        Email,
        Sms
    }

    private sealed class ReminderNotification
    {
        public Guid BookingId { get; init; }
        public Guid UserId { get; init; }
        public Guid GroupId { get; init; }
        public Guid VehicleId { get; init; }
        public string Title { get; init; } = string.Empty;
        public string EmailMessage { get; init; } = string.Empty;
        public string SmsMessage { get; init; } = string.Empty;
        public string ActionUrl { get; init; } = string.Empty;
        public string ActionText { get; init; } = "Open";
        public ReminderType ReminderType { get; init; }

        public BulkNotificationEvent CreateEvent(ReminderDeliveryChannel channel)
        {
            var baseType = ReminderType switch
            {
                ReminderType.PreCheck => "BookingPreCheckReminder",
                ReminderType.FinalCall => "BookingFinalCheckoutReminder",
                ReminderType.Missed => "BookingMissedCheckoutReminder",
                _ => "BookingReminder"
            };

            var notificationType = channel == ReminderDeliveryChannel.Email
                ? $"{baseType}Email"
                : $"{baseType}Sms";

            var message = channel == ReminderDeliveryChannel.Email ? EmailMessage : SmsMessage;

            return new BulkNotificationEvent
            {
                UserIds = new List<Guid> { UserId },
                GroupId = GroupId,
                Title = Title,
                Message = message,
                Type = notificationType,
                Priority = ReminderType == ReminderType.Missed ? "High" : "Normal",
                CreatedAt = DateTime.UtcNow,
                ActionUrl = ActionUrl,
                ActionText = ActionText
            };
        }
    }
}











