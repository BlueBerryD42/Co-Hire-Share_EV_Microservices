using MassTransit;
using CoOwnershipVehicle.Shared.Contracts.Events;
using CoOwnershipVehicle.Analytics.Api.Services;

namespace CoOwnershipVehicle.Analytics.Api.Consumers;

public class PaymentAnalyticsEventConsumer : IConsumer<PaymentCompletedEvent>
{
    private readonly IAnalyticsService _analyticsService;
    private readonly ILogger<PaymentAnalyticsEventConsumer> _logger;

    public PaymentAnalyticsEventConsumer(IAnalyticsService analyticsService, ILogger<PaymentAnalyticsEventConsumer> logger)
    {
        _analyticsService = analyticsService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PaymentCompletedEvent> context)
    {
        var message = context.Message;
        
        _logger.LogInformation("Processing PaymentCompletedEvent for analytics - PaymentId: {PaymentId}", message.PaymentId);

        try
        {
            // Process analytics for the payment's group
            await _analyticsService.ProcessAnalyticsAsync(message.GroupId, null);
            
            _logger.LogInformation("Successfully processed PaymentCompletedEvent for analytics - PaymentId: {PaymentId}", message.PaymentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing PaymentCompletedEvent for analytics - PaymentId: {PaymentId}", message.PaymentId);
            throw;
        }
    }
}
