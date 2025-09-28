using Microsoft.EntityFrameworkCore;
using CoOwnershipVehicle.Data;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Notification.Api.Services;

public class NotificationSchedulerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationSchedulerService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);

    public NotificationSchedulerService(IServiceProvider serviceProvider, ILogger<NotificationSchedulerService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessScheduledNotifications();
                await ProcessReminderNotifications();
                await ProcessCleanup();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in notification scheduler");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task ProcessScheduledNotifications()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var now = DateTime.UtcNow;
        var scheduledNotifications = await context.Notifications
            .Where(n => n.ScheduledFor <= now && n.Status == NotificationStatus.Unread)
            .ToListAsync();

        foreach (var notification in scheduledNotifications)
        {
            _logger.LogInformation("Processing scheduled notification {NotificationId} for user {UserId}", 
                notification.Id, notification.UserId);
        }
    }

    private async Task ProcessReminderNotifications()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Booking reminders (1 hour before)
        var upcomingBookings = await context.Bookings
            .Include(b => b.User)
            .Include(b => b.Vehicle)
            .Where(b => b.Status == BookingStatus.Confirmed && 
                       b.StartAt <= DateTime.UtcNow.AddHours(1) && 
                       b.StartAt > DateTime.UtcNow)
            .ToListAsync();

        foreach (var booking in upcomingBookings)
        {
            var reminderExists = await context.Notifications
                .AnyAsync(n => n.UserId == booking.UserId && 
                              n.Type == NotificationType.BookingReminder &&
                              n.ScheduledFor.Date == booking.StartAt.Date);

            if (!reminderExists)
            {
                var reminder = new Notification
                {
                    UserId = booking.UserId,
                    GroupId = booking.GroupId,
                    Title = "Booking Reminder",
                    Message = $"Your booking for {booking.Vehicle.Model} starts in 1 hour at {booking.StartAt:HH:mm}",
                    Type = NotificationType.BookingReminder,
                    Priority = NotificationPriority.Normal,
                    ScheduledFor = DateTime.UtcNow
                };

                context.Notifications.Add(reminder);
            }
        }

        // Payment due reminders
        var overduePayments = await context.Payments
            .Include(p => p.Invoice)
            .ThenInclude(i => i.User)
            .Where(p => p.Status == PaymentStatus.Pending && 
                       p.DueDate < DateTime.UtcNow)
            .ToListAsync();

        foreach (var payment in overduePayments)
        {
            var reminderExists = await context.Notifications
                .AnyAsync(n => n.UserId == payment.Invoice.UserId && 
                              n.Type == NotificationType.OverduePayment &&
                              n.CreatedAt.Date == DateTime.UtcNow.Date);

            if (!reminderExists)
            {
                var reminder = new Notification
                {
                    UserId = payment.Invoice.UserId,
                    Title = "Payment Overdue",
                    Message = $"Payment of {payment.Amount:C} is overdue. Please make payment as soon as possible.",
                    Type = NotificationType.OverduePayment,
                    Priority = NotificationPriority.High,
                    ScheduledFor = DateTime.UtcNow
                };

                context.Notifications.Add(reminder);
            }
        }

        await context.SaveChangesAsync();
    }

    private async Task ProcessCleanup()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Clean up old read notifications (older than 30 days)
        var cutoffDate = DateTime.UtcNow.AddDays(-30);
        var oldNotifications = await context.Notifications
            .Where(n => n.Status == NotificationStatus.Read && n.CreatedAt < cutoffDate)
            .ToListAsync();

        if (oldNotifications.Any())
        {
            context.Notifications.RemoveRange(oldNotifications);
            await context.SaveChangesAsync();
            _logger.LogInformation("Cleaned up {Count} old notifications", oldNotifications.Count);
        }
    }
}
