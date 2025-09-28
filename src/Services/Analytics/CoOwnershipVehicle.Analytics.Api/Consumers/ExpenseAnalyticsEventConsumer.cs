using MassTransit;
using CoOwnershipVehicle.Shared.Contracts.Events;
using CoOwnershipVehicle.Analytics.Api.Services;

namespace CoOwnershipVehicle.Analytics.Api.Consumers;

public class ExpenseAnalyticsEventConsumer : IConsumer<ExpenseCreatedEvent>
{
    private readonly IAnalyticsService _analyticsService;
    private readonly ILogger<ExpenseAnalyticsEventConsumer> _logger;

    public ExpenseAnalyticsEventConsumer(IAnalyticsService analyticsService, ILogger<ExpenseAnalyticsEventConsumer> logger)
    {
        _analyticsService = analyticsService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ExpenseCreatedEvent> context)
    {
        var message = context.Message;
        
        _logger.LogInformation("Processing ExpenseCreatedEvent for analytics - ExpenseId: {ExpenseId}", message.ExpenseId);

        try
        {
            // Process analytics for the expense's group and vehicle
            await _analyticsService.ProcessAnalyticsAsync(message.GroupId, message.VehicleId);
            
            _logger.LogInformation("Successfully processed ExpenseCreatedEvent for analytics - ExpenseId: {ExpenseId}", message.ExpenseId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing ExpenseCreatedEvent for analytics - ExpenseId: {ExpenseId}", message.ExpenseId);
            throw;
        }
    }
}
