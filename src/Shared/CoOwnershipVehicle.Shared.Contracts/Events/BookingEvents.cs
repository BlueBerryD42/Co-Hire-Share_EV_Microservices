using CoOwnershipVehicle.Shared.Contracts.DTOs;

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
