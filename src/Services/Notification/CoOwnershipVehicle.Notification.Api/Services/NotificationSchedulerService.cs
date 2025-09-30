using Microsoft.EntityFrameworkCore;
using CoOwnershipVehicle.Notification.Api.Data;
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
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

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
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

        // TODO: Reminder notifications should be handled via events from other services
        // This method should be refactored to only handle notifications that are already
        // in the NotificationDbContext, not query other services' entities directly
        
        _logger.LogInformation("ProcessReminderNotifications - TODO: Implement event-based reminders");
        
        // For now, just process any scheduled notifications that are ready
        var readyNotifications = await context.Notifications
            .Where(n => n.ScheduledFor <= DateTime.UtcNow && n.Status == NotificationStatus.Unread)
            .ToListAsync();

        foreach (var notification in readyNotifications)
        {
            _logger.LogInformation("Processing reminder notification {NotificationId} for user {UserId}", 
                notification.Id, notification.UserId);
        }

        await context.SaveChangesAsync();
    }

    private async Task ProcessCleanup()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

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
