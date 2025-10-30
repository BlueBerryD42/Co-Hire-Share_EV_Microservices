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
    
    public BookingPriority Priority { get; set; } = BookingPriority.Normal;

    public bool RequiresDamageReview { get; set; }
    
    // Navigation properties
    public virtual Vehicle Vehicle { get; set; } = null!;
    public virtual OwnershipGroup Group { get; set; } = null!;
    public virtual User User { get; set; } = null!;
    public virtual ICollection<CheckIn> CheckIns { get; set; } = new List<CheckIn>();
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
