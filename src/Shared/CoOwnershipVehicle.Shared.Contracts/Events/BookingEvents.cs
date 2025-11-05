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

public class LateReturnFeeCreatedEvent : BaseEvent
{
    public Guid LateReturnFeeId { get; set; }
    public Guid BookingId { get; set; }
    public Guid CheckInId { get; set; }
    public Guid UserId { get; set; }
    public Guid GroupId { get; set; }
    public Guid VehicleId { get; set; }
    public double LateByMinutes { get; set; }
    public double ChargeableMinutes { get; set; }
    public decimal FeeAmount { get; set; }
    public LateReturnFeeStatus Status { get; set; }
    public int GracePeriodMinutes { get; set; }
    public string? CalculationMethod { get; set; }
    public DateTime DetectedAt { get; set; }

    public LateReturnFeeCreatedEvent()
    {
        EventType = nameof(LateReturnFeeCreatedEvent);
    }
}

public class LateReturnFeeStatusChangedEvent : BaseEvent
{
    public Guid LateReturnFeeId { get; set; }
    public Guid BookingId { get; set; }
    public Guid CheckInId { get; set; }
    public Guid UserId { get; set; }
    public LateReturnFeeStatus Status { get; set; }
    public Guid ChangedBy { get; set; }
    public string? Reason { get; set; }
    public decimal? FeeAmount { get; set; }
    public Guid? ExpenseId { get; set; }
    public Guid? InvoiceId { get; set; }
    public DateTime ChangedAt { get; set; }

    public LateReturnFeeStatusChangedEvent()
    {
        EventType = nameof(LateReturnFeeStatusChangedEvent);
    }
}

public class RecurringBookingCreatedEvent : BaseEvent
{
    public Guid RecurringBookingId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid GroupId { get; set; }
    public Guid UserId { get; set; }
    public RecurrencePattern Pattern { get; set; }
    public int Interval { get; set; }
    public IReadOnlyList<DayOfWeek> DaysOfWeek { get; set; } = Array.Empty<DayOfWeek>();
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public DateTime RecurrenceStartDate { get; set; }
    public DateTime? RecurrenceEndDate { get; set; }
    public RecurringBookingStatus Status { get; set; }
    public IReadOnlyList<Guid> GeneratedBookingIds { get; set; } = Array.Empty<Guid>();

    public RecurringBookingCreatedEvent()
    {
        EventType = nameof(RecurringBookingCreatedEvent);
    }
}

public class RecurringBookingUpdatedEvent : BaseEvent
{
    public Guid RecurringBookingId { get; set; }
    public RecurrencePattern Pattern { get; set; }
    public int Interval { get; set; }
    public IReadOnlyList<DayOfWeek> DaysOfWeek { get; set; } = Array.Empty<DayOfWeek>();
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public DateTime RecurrenceStartDate { get; set; }
    public DateTime? RecurrenceEndDate { get; set; }
    public RecurringBookingStatus Status { get; set; }
    public DateTime UpdatedAt { get; set; }

    public RecurringBookingUpdatedEvent()
    {
        EventType = nameof(RecurringBookingUpdatedEvent);
    }
}

public class RecurringBookingStatusChangedEvent : BaseEvent
{
    public Guid RecurringBookingId { get; set; }
    public RecurringBookingStatus Status { get; set; }
    public Guid ChangedBy { get; set; }
    public DateTime ChangedAt { get; set; }
    public string? Reason { get; set; }

    public RecurringBookingStatusChangedEvent()
    {
        EventType = nameof(RecurringBookingStatusChangedEvent);
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

public class BookingRescheduledEvent : BaseEvent
{
    public Guid BookingId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid GroupId { get; set; }
    public Guid UserId { get; set; }
    public DateTime OriginalStartAt { get; set; }
    public DateTime OriginalEndAt { get; set; }
    public DateTime NewStartAt { get; set; }
    public DateTime NewEndAt { get; set; }
    public string Reason { get; set; } = string.Empty;

    public BookingRescheduledEvent()
    {
        EventType = nameof(BookingRescheduledEvent);
    }
}

public class EmergencyBookingAuditEvent : BaseEvent
{
    public Guid BookingId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid GroupId { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool AutoCancelApplied { get; set; }
    public IReadOnlyList<Guid> ConflictingBookingIds { get; set; } = Array.Empty<Guid>();
    public IReadOnlyList<Guid> AutoCancelledBookingIds { get; set; } = Array.Empty<Guid>();
    public IReadOnlyList<Guid> RescheduledBookingIds { get; set; } = Array.Empty<Guid>();
    public IReadOnlyList<Guid> PendingResolutionBookingIds { get; set; } = Array.Empty<Guid>();

    public EmergencyBookingAuditEvent()
    {
        EventType = nameof(EmergencyBookingAuditEvent);
    }
}

public class EmergencyBookingUsageEvent : BaseEvent
{
    public Guid BookingId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid GroupId { get; set; }
    public Guid UserId { get; set; }

    public EmergencyBookingUsageEvent()
    {
        EventType = nameof(EmergencyBookingUsageEvent);
    }
}
