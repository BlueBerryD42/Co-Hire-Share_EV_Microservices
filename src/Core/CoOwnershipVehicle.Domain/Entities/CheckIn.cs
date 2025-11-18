using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoOwnershipVehicle.Domain.Entities;

public class CheckIn : BaseEntity
{
    public Guid BookingId { get; set; }
    
    public Guid UserId { get; set; }

    public Guid? VehicleId { get; set; }
    
    public CheckInType Type { get; set; }
    
    public int Odometer { get; set; }
    
    [StringLength(1000)]
    public string? Notes { get; set; }
    
    [StringLength(500)]
    public string? SignatureReference { get; set; }

    [StringLength(200)]
    public string? SignatureDevice { get; set; }

    [StringLength(100)]
    public string? SignatureDeviceId { get; set; }

    [StringLength(45)]
    public string? SignatureIpAddress { get; set; }

    public DateTime? SignatureCapturedAt { get; set; }

    [StringLength(128)]
    public string? SignatureHash { get; set; }

    [StringLength(500)]
    public string? SignatureCertificateUrl { get; set; }

    public bool? SignatureMatchesPrevious { get; set; }

    [StringLength(2000)]
    public string? SignatureMetadataJson { get; set; }
    
    public DateTime CheckInTime { get; set; } = DateTime.UtcNow;

    public bool IsLateReturn { get; set; }

    public double? LateReturnMinutes { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? LateFeeAmount { get; set; }
    
    // Navigation properties
    public virtual Booking Booking { get; set; } = null!;
    public virtual User User { get; set; } = null!;
    public virtual Vehicle? Vehicle { get; set; }
    public virtual LateReturnFee? LateReturnFee { get; set; }
    public virtual ICollection<CheckInPhoto> Photos { get; set; } = new List<CheckInPhoto>();
}

public class CheckInPhoto : BaseEntity
{
    public Guid CheckInId { get; set; }
    
    [Required]
    [StringLength(4000)]
    public string PhotoUrl { get; set; } = string.Empty;

    [StringLength(500)]
    public string? ThumbnailUrl { get; set; }

    [StringLength(1000)]
    public string? StoragePath { get; set; }

    [StringLength(1000)]
    public string? ThumbnailPath { get; set; }

    [StringLength(100)]
    public string? ContentType { get; set; }

    public DateTime? CapturedAt { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public bool IsDeleted { get; set; }
    
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
