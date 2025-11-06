using System.ComponentModel.DataAnnotations;

namespace CoOwnershipVehicle.Domain.Entities;

public class AuditLog : BaseEntity
{
    [Required]
    [StringLength(100)]
    public string Entity { get; set; } = string.Empty;
    
    public Guid EntityId { get; set; }
    
    [Required]
    [StringLength(50)]
    public string Action { get; set; } = string.Empty;
    
    public Guid PerformedBy { get; set; }
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    [StringLength(2000)]
    public string? Details { get; set; }
    
    [StringLength(50)]
    public string? IpAddress { get; set; }
    
    [StringLength(500)]
    public string? UserAgent { get; set; }
    
    // Navigation properties
    public virtual User User { get; set; } = null!;
}
