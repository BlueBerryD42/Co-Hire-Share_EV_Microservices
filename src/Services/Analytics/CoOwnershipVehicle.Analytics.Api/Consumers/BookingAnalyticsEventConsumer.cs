using MassTransit;
using CoOwnershipVehicle.Shared.Contracts.Events;
using CoOwnershipVehicle.Analytics.Api.Services;

namespace CoOwnershipVehicle.Analytics.Api.Consumers;

public class BookingAnalyticsEventConsumer : IConsumer<BookingCreatedEvent>
{
    private readonly IAnalyticsService _analyticsService;
    private readonly ILogger<BookingAnalyticsEventConsumer> _logger;

    public BookingAnalyticsEventConsumer(IAnalyticsService analyticsService, ILogger<BookingAnalyticsEventConsumer> logger)
    {
        _analyticsService = analyticsService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<BookingCreatedEvent> context)
    {
        var message = context.Message;
        
        _logger.LogInformation("Processing BookingCreatedEvent for analytics - BookingId: {BookingId}", message.BookingId);

        try
        {
            // Process analytics for the booking's vehicle and group
            await _analyticsService.ProcessAnalyticsAsync(message.GroupId, message.VehicleId);
            
            _logger.LogInformation("Successfully processed BookingCreatedEvent for analytics - BookingId: {BookingId}", message.BookingId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing BookingCreatedEvent for analytics - BookingId: {BookingId}", message.BookingId);
            throw;
        }
    }
}
