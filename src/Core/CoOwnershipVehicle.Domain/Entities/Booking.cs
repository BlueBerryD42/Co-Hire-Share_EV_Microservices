using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoOwnershipVehicle.Domain.Entities;

public class Booking : BaseEntity
{
    public Guid VehicleId { get; set; }
    
    public Guid GroupId { get; set; }
    
    public Guid UserId { get; set; }
    
    public DateTime StartAt { get; set; }
    
    public DateTime EndAt { get; set; }
    
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    
    [Column(TypeName = "decimal(10,4)")]
    public decimal PriorityScore { get; set; }
    
    [StringLength(500)]
    public string? Notes { get; set; }
    
    [StringLength(200)]
    public string? Purpose { get; set; }
    
    public bool IsEmergency { get; set; } = false;
    
    [StringLength(1000)]
    public string? EmergencyReason { get; set; } // Added EmergencyReason
    
    public BookingPriority Priority { get; set; } = BookingPriority.Normal;

    public bool RequiresDamageReview { get; set; }

    public VehicleStatus VehicleStatus { get; set; } = VehicleStatus.Available;

    [Column(TypeName = "decimal(18,2)")]
    public decimal? DistanceKm { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TripFeeAmount { get; set; }

    public DateTime? PreCheckoutReminderSentAt { get; set; }

    public DateTime? FinalCheckoutReminderSentAt { get; set; }

    public DateTime? MissedCheckoutReminderSentAt { get; set; }

    public Guid? RecurringBookingId { get; set; }
    public Guid? BookingTemplateId { get; set; } // Added BookingTemplateId
    
    // Navigation properties
    public virtual Vehicle Vehicle { get; set; } = null!;
    public virtual OwnershipGroup Group { get; set; } = null!;
    public virtual User User { get; set; } = null!;
    public virtual ICollection<CheckIn> CheckIns { get; set; } = new List<CheckIn>();
    public virtual ICollection<LateReturnFee> LateReturnFees { get; set; } = new List<LateReturnFee>();
    public virtual RecurringBooking? RecurringBooking { get; set; }
    public virtual BookingTemplate? BookingTemplate { get; set; } // Added BookingTemplate navigation property
}

public enum BookingStatus
{
    Pending = 0,
    PendingApproval = 1,
    Confirmed = 2,
    InProgress = 3,
    Completed = 4,
    Cancelled = 5,
    NoShow = 6
}

public enum BookingPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Emergency = 3
}
