using MassTransit;
using CoOwnershipVehicle.Shared.Contracts.Events;
using CoOwnershipVehicle.Notification.Api.Services;

namespace CoOwnershipVehicle.Notification.Api.Consumers;

public class NotificationCreatedEventConsumer : IConsumer<NotificationCreatedEvent>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<NotificationCreatedEventConsumer> _logger;

    public NotificationCreatedEventConsumer(INotificationService notificationService, ILogger<NotificationCreatedEventConsumer> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<NotificationCreatedEvent> context)
    {
        var message = context.Message;
        
        _logger.LogInformation("Processing NotificationCreatedEvent for user {UserId}", message.UserId);

        try
        {
            var dto = new CreateNotificationDto
            {
                UserId = message.UserId,
                GroupId = message.GroupId,
                Title = message.Title,
                Message = message.Message,
                Type = message.Type,
                Priority = message.Priority,
                ScheduledFor = message.CreatedAt,
                ActionUrl = message.ActionUrl,
                ActionText = message.ActionText
            };

            await _notificationService.CreateNotificationAsync(dto);
            
            _logger.LogInformation("Successfully processed NotificationCreatedEvent for user {UserId}", message.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing NotificationCreatedEvent for user {UserId}", message.UserId);
            throw;
        }
    }
}
