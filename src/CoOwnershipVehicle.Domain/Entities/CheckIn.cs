using System.ComponentModel.DataAnnotations;

namespace CoOwnershipVehicle.Domain.Entities;

public class CheckIn : BaseEntity
{
    public Guid BookingId { get; set; }
    
    public Guid UserId { get; set; }
    
    public CheckInType Type { get; set; }
    
    public int Odometer { get; set; }
    
    [StringLength(1000)]
    public string? Notes { get; set; }
    
    [StringLength(500)]
    public string? SignatureReference { get; set; }
    
    public DateTime CheckInTime { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Booking Booking { get; set; } = null!;
    public virtual User User { get; set; } = null!;
    public virtual ICollection<CheckInPhoto> Photos { get; set; } = new List<CheckInPhoto>();
}

public class CheckInPhoto : BaseEntity
{
    public Guid CheckInId { get; set; }
    
    [Required]
    [StringLength(500)]
    public string PhotoUrl { get; set; } = string.Empty;
    
    [StringLength(200)]
    public string? Description { get; set; }
    
    public PhotoType Type { get; set; }
    
    // Navigation properties
    public virtual CheckIn CheckIn { get; set; } = null!;
}

public enum CheckInType
{
    CheckOut = 0,
    CheckIn = 1
}

public enum PhotoType
{
    Exterior = 0,
    Interior = 1,
    Dashboard = 2,
    Damage = 3,
    Other = 4
}
