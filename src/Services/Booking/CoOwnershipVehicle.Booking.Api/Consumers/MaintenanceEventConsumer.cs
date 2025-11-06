using CoOwnershipVehicle.Booking.Api.Data;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Domain.Enums;
using CoOwnershipVehicle.Shared.Contracts.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace CoOwnershipVehicle.Booking.Api.Consumers;

/// <summary>
/// Consumes MaintenanceScheduledEvent to create calendar blocks
/// Prevents bookings during scheduled maintenance
/// </summary>
public class MaintenanceScheduledConsumer : IConsumer<MaintenanceScheduledEvent>
{
    private readonly BookingDbContext _context;
    private readonly ILogger<MaintenanceScheduledConsumer> _logger;

    public MaintenanceScheduledConsumer(BookingDbContext context, ILogger<MaintenanceScheduledConsumer> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<MaintenanceScheduledEvent> context)
    {
        var evt = context.Message;

        _logger.LogInformation(
            "Received MaintenanceScheduledEvent: ScheduleId={ScheduleId}, VehicleId={VehicleId}, Time={Start}-{End}",
            evt.MaintenanceScheduleId, evt.VehicleId, evt.MaintenanceStartTime, evt.MaintenanceEndTime);

        try
        {
            // Check if block already exists
            var existing = await _context.MaintenanceBlocks
                .FirstOrDefaultAsync(m => m.MaintenanceScheduleId == evt.MaintenanceScheduleId);

            if (existing != null)
            {
                _logger.LogWarning("MaintenanceBlock already exists for ScheduleId={ScheduleId}", evt.MaintenanceScheduleId);
                return;
            }

            // Create new maintenance block
            var block = new MaintenanceBlock
            {
                Id = Guid.NewGuid(),
                MaintenanceScheduleId = evt.MaintenanceScheduleId,
                VehicleId = evt.VehicleId,
                GroupId = evt.GroupId,
                ServiceType = evt.ServiceType,
                StartTime = evt.MaintenanceStartTime,
                EndTime = evt.MaintenanceEndTime,
                Status = Domain.Enums.MaintenanceStatus.Scheduled,
                Priority = evt.Priority, // evt.Priority is already Domain.Enums.MaintenancePriority
                Notes = $"Maintenance: {evt.ServiceType}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.MaintenanceBlocks.Add(block);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Created MaintenanceBlock: Id={BlockId}, VehicleId={VehicleId}, Time={Start}-{End}",
                block.Id, block.VehicleId, block.StartTime, block.EndTime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating MaintenanceBlock for ScheduleId={ScheduleId}", evt.MaintenanceScheduleId);
            throw; // Let MassTransit handle retry
        }
    }
}

/// <summary>
/// Consumes MaintenanceCancelledEvent to remove calendar blocks
/// </summary>
public class MaintenanceCancelledConsumer : IConsumer<MaintenanceCancelledEvent>
{
    private readonly BookingDbContext _context;
    private readonly ILogger<MaintenanceCancelledConsumer> _logger;

    public MaintenanceCancelledConsumer(BookingDbContext context, ILogger<MaintenanceCancelledConsumer> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<MaintenanceCancelledEvent> context)
    {
        var evt = context.Message;

        _logger.LogInformation(
            "Received MaintenanceCancelledEvent: ScheduleId={ScheduleId}, VehicleId={VehicleId}",
            evt.MaintenanceScheduleId, evt.VehicleId);

        try
        {
            var block = await _context.MaintenanceBlocks
                .FirstOrDefaultAsync(m => m.MaintenanceScheduleId == evt.MaintenanceScheduleId);

            if (block != null)
            {
                block.Status = Domain.Enums.MaintenanceStatus.Cancelled;
                block.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Updated MaintenanceBlock status to Cancelled: BlockId={BlockId}",
                    block.Id);
            }
            else
            {
                _logger.LogWarning("MaintenanceBlock not found for ScheduleId={ScheduleId}", evt.MaintenanceScheduleId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling MaintenanceBlock for ScheduleId={ScheduleId}", evt.MaintenanceScheduleId);
            throw;
        }
    }
}

/// <summary>
/// Consumes MaintenanceCompletedEvent to update calendar blocks
/// </summary>
public class MaintenanceCompletedConsumer : IConsumer<MaintenanceCompletedEvent>
{
    private readonly BookingDbContext _context;
    private readonly ILogger<MaintenanceCompletedConsumer> _logger;

    public MaintenanceCompletedConsumer(BookingDbContext context, ILogger<MaintenanceCompletedConsumer> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<MaintenanceCompletedEvent> context)
    {
        var evt = context.Message;

        _logger.LogInformation(
            "Received MaintenanceCompletedEvent: RecordId={RecordId}, VehicleId={VehicleId}",
            evt.MaintenanceRecordId, evt.VehicleId);

        // Note: We would need MaintenanceScheduleId in the event to update the block
        // For now, just log that we received it
        _logger.LogInformation("Maintenance completed - no action needed for calendar block");
    }
}
