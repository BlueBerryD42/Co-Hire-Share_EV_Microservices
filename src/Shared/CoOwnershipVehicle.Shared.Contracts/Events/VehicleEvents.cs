using CoOwnershipVehicle.Domain.Enums;

namespace CoOwnershipVehicle.Shared.Contracts.Events;

public class MaintenanceScheduledEvent : BaseEvent
{
    public Guid MaintenanceScheduleId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid GroupId { get; set; }
    public ServiceType ServiceType { get; set; }
    public DateTime ScheduledDate { get; set; }
    public int EstimatedDuration { get; set; }
    public decimal? EstimatedCost { get; set; }
    public string? ServiceProvider { get; set; }
    public MaintenancePriority Priority { get; set; }
    public Guid ScheduledBy { get; set; }
    public DateTime MaintenanceStartTime { get; set; }
    public DateTime MaintenanceEndTime { get; set; }

    public MaintenanceScheduledEvent()
    {
        EventType = nameof(MaintenanceScheduledEvent);
    }
}

public class MaintenanceCompletedEvent : BaseEvent
{
    public Guid MaintenanceScheduleId { get; set; }
    public Guid MaintenanceRecordId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid GroupId { get; set; }
    public ServiceType ServiceType { get; set; }
    public DateTime ServiceDate { get; set; }
    public decimal ActualCost { get; set; }
    public int OdometerReading { get; set; }
    public string? WorkPerformed { get; set; }
    public string? PartsReplaced { get; set; }
    public string? ServiceProvider { get; set; }
    public DateTime? NextServiceDue { get; set; }
    public int? NextServiceOdometer { get; set; }
    public Guid PerformedBy { get; set; }

    public MaintenanceCompletedEvent()
    {
        EventType = nameof(MaintenanceCompletedEvent);
    }
}

public class MaintenanceCancelledEvent : BaseEvent
{
    public Guid MaintenanceScheduleId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid GroupId { get; set; }
    public string? CancellationReason { get; set; }
    public Guid CancelledBy { get; set; }

    public MaintenanceCancelledEvent()
    {
        EventType = nameof(MaintenanceCancelledEvent);
    }
}
