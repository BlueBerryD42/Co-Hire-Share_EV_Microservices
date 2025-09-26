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
    
    // Navigation properties
    public virtual Vehicle Vehicle { get; set; } = null!;
    public virtual OwnershipGroup Group { get; set; } = null!;
    public virtual User User { get; set; } = null!;
    public virtual ICollection<CheckIn> CheckIns { get; set; } = new List<CheckIn>();
}

public enum BookingStatus
{
    Pending = 0,
    Confirmed = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4,
    NoShow = 5
}
