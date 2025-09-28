using MassTransit;
using CoOwnershipVehicle.Shared.Contracts.Events;
using CoOwnershipVehicle.Analytics.Api.Services;

namespace CoOwnershipVehicle.Analytics.Api.Consumers;

public class VehicleAnalyticsEventConsumer : IConsumer<VehicleStatusChangedEvent>
{
    private readonly IAnalyticsService _analyticsService;
    private readonly ILogger<VehicleAnalyticsEventConsumer> _logger;

    public VehicleAnalyticsEventConsumer(IAnalyticsService analyticsService, ILogger<VehicleAnalyticsEventConsumer> logger)
    {
        _analyticsService = analyticsService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<VehicleStatusChangedEvent> context)
    {
        var message = context.Message;
        
        _logger.LogInformation("Processing VehicleStatusChangedEvent for analytics - VehicleId: {VehicleId}", message.VehicleId);

        try
        {
            // Process analytics for the vehicle and its group
            await _analyticsService.ProcessAnalyticsAsync(message.GroupId, message.VehicleId);
            
            _logger.LogInformation("Successfully processed VehicleStatusChangedEvent for analytics - VehicleId: {VehicleId}", message.VehicleId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing VehicleStatusChangedEvent for analytics - VehicleId: {VehicleId}", message.VehicleId);
            throw;
        }
    }
}
