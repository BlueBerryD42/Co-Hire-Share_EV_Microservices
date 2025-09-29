using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Shared.Contracts.Events;

public class BookingCreatedEvent : BaseEvent
{
    public Guid BookingId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid GroupId { get; set; }
    public Guid UserId { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public decimal PriorityScore { get; set; }
    public BookingStatus Status { get; set; }
    public bool IsEmergency { get; set; }
    public BookingPriority Priority { get; set; }
    
    public BookingCreatedEvent()
    {
        EventType = nameof(BookingCreatedEvent);
    }
}

public class BookingConfirmedEvent : BaseEvent
{
    public Guid BookingId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid UserId { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    
    public BookingConfirmedEvent()
    {
        EventType = nameof(BookingConfirmedEvent);
    }
}

public class BookingCancelledEvent : BaseEvent
{
    public Guid BookingId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid UserId { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public string? CancellationReason { get; set; }
    public string? Reason { get; set; }
    public Guid CancelledBy { get; set; }
    
    public BookingCancelledEvent()
    {
        EventType = nameof(BookingCancelledEvent);
    }
}

public class VehicleCheckedOutEvent : BaseEvent
{
    public Guid BookingId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid UserId { get; set; }
    public int Odometer { get; set; }
    public DateTime CheckOutTime { get; set; }
    public string? SignatureReference { get; set; }
    
    public VehicleCheckedOutEvent()
    {
        EventType = nameof(VehicleCheckedOutEvent);
    }
}

public class VehicleCheckedInEvent : BaseEvent
{
    public Guid BookingId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid UserId { get; set; }
    public int Odometer { get; set; }
    public DateTime CheckInTime { get; set; }
    public string? SignatureReference { get; set; }
    public List<string> PhotoUrls { get; set; } = new();
    
    public VehicleCheckedInEvent()
    {
        EventType = nameof(VehicleCheckedInEvent);
    }
}

public class BookingApprovedEvent : BaseEvent
{
    public Guid BookingId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid UserId { get; set; }
    public Guid ApprovedBy { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    
    public BookingApprovedEvent()
    {
        EventType = nameof(BookingApprovedEvent);
    }
}

public class BookingPendingApprovalEvent : BaseEvent
{
    public Guid BookingId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid UserId { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public bool IsEmergency { get; set; }
    public BookingPriority Priority { get; set; }
    public int ConflictCount { get; set; }
    
    public BookingPendingApprovalEvent()
    {
        EventType = nameof(BookingPendingApprovalEvent);
    }
}

public class VehicleStatusChangedEvent : BaseEvent
{
    public Guid VehicleId { get; set; }
    public Guid GroupId { get; set; }
    public VehicleStatus OldStatus { get; set; }
    public VehicleStatus NewStatus { get; set; }
    public Guid ChangedBy { get; set; }
    public DateTime ChangedAt { get; set; }
    public string? Reason { get; set; }
    
    public VehicleStatusChangedEvent()
    {
        EventType = nameof(VehicleStatusChangedEvent);
    }
}