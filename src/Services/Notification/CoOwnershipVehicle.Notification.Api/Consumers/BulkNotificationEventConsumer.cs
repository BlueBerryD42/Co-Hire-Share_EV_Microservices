using MassTransit;
using CoOwnershipVehicle.Shared.Contracts.Events;
using CoOwnershipVehicle.Notification.Api.Services;

namespace CoOwnershipVehicle.Notification.Api.Consumers;

public class BulkNotificationEventConsumer : IConsumer<BulkNotificationEvent>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<BulkNotificationEventConsumer> _logger;

    public BulkNotificationEventConsumer(INotificationService notificationService, ILogger<BulkNotificationEventConsumer> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<BulkNotificationEvent> context)
    {
        var message = context.Message;
        
        _logger.LogInformation("Processing BulkNotificationEvent for {UserCount} users", message.UserIds.Count);

        try
        {
            var dto = new CreateBulkNotificationDto
            {
                UserIds = message.UserIds,
                GroupId = message.GroupId,
                Title = message.Title,
                Message = message.Message,
                Type = message.Type,
                Priority = message.Priority,
                ScheduledFor = message.CreatedAt,
                ActionUrl = message.ActionUrl,
                ActionText = message.ActionText
            };

            await _notificationService.CreateBulkNotificationAsync(dto);
            
            _logger.LogInformation("Successfully processed BulkNotificationEvent for {UserCount} users", message.UserIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing BulkNotificationEvent for {UserCount} users", message.UserIds.Count);
            throw;
        }
    }
}
