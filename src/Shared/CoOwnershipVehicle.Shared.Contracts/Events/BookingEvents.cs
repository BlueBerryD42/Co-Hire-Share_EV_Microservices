using System;
using System.Collections.Generic;
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

public class TripStartedEvent : BaseEvent
{
    public Guid BookingId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid GroupId { get; set; }
    public Guid UserId { get; set; }
    public Guid CheckInId { get; set; }
    public DateTime CheckOutTime { get; set; }
    public int Odometer { get; set; }
    public string? Notes { get; set; }
    public string? SignatureReference { get; set; }
    public List<string> PhotoUrls { get; set; } = new();

    public TripStartedEvent()
    {
        EventType = nameof(TripStartedEvent);
    }
}

public class TripEndedEvent : BaseEvent
{
    public Guid BookingId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid GroupId { get; set; }
    public Guid UserId { get; set; }
    public Guid CheckInId { get; set; }
    public Guid CheckOutId { get; set; }
    public DateTime CheckInTime { get; set; }
    public DateTime CheckOutTime { get; set; }
    public int TripDistance { get; set; }
    public double TripDurationMinutes { get; set; }
    public bool IsLateReturn { get; set; }
    public double LateByMinutes { get; set; }
    public int StartOdometer { get; set; }
    public int EndOdometer { get; set; }
    public string? Notes { get; set; }
    public string? SignatureReference { get; set; }
    public List<string> PhotoUrls { get; set; } = new();

    public TripEndedEvent()
    {
        EventType = nameof(TripEndedEvent);
    }
}

public class SignatureCapturedEvent : BaseEvent
{
    public Guid CheckInId { get; set; }
    public Guid BookingId { get; set; }
    public Guid UserId { get; set; }
    public string SignatureUrl { get; set; } = string.Empty;
    public string SignatureHash { get; set; } = string.Empty;
    public DateTime CapturedAt { get; set; }
    public string? Device { get; set; }
    public string? DeviceId { get; set; }
    public string? IpAddress { get; set; }
    public bool? MatchesPrevious { get; set; }
    public string? CertificateUrl { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public SignatureCapturedEvent()
    {
        EventType = nameof(SignatureCapturedEvent);
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

public class DamageReportedEvent : BaseEvent
{
    public Guid DamageReportId { get; set; }
    public Guid CheckInId { get; set; }
    public Guid BookingId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid GroupId { get; set; }
    public Guid ReportedByUserId { get; set; }
    public DamageSeverity Severity { get; set; }
    public DamageLocation Location { get; set; }
    public decimal? EstimatedCost { get; set; }
    public string Description { get; set; } = string.Empty;
    public IReadOnlyList<Guid> PhotoIds { get; set; } = Array.Empty<Guid>();

    public DamageReportedEvent()
    {
        EventType = nameof(DamageReportedEvent);
    }
}

public class DamageReportStatusChangedEvent : BaseEvent
{
    public Guid DamageReportId { get; set; }
    public Guid BookingId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid GroupId { get; set; }
    public DamageReportStatus Status { get; set; }
    public Guid ChangedByUserId { get; set; }
    public decimal? EstimatedCost { get; set; }
    public Guid? ExpenseId { get; set; }
    public string? Notes { get; set; }
    public DateTime ChangedAt { get; set; }

    public DamageReportStatusChangedEvent()
    {
        EventType = nameof(DamageReportStatusChangedEvent);
    }
}
