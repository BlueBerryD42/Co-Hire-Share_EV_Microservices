using System.ComponentModel.DataAnnotations;

namespace CoOwnershipVehicle.Domain.Entities;

public enum DamageSeverity
{
    Minor = 0,
    Moderate = 1,
    Severe = 2
}

public enum DamageLocation
{
    Front = 0,
    Rear = 1,
    Left = 2,
    Right = 3,
    Interior = 4,
    Roof = 5,
    Undercarriage = 6,
    Other = 7
}

public enum DamageReportStatus
{
    Reported = 0,
    UnderReview = 1,
    Resolved = 2
}

public class DamageReport : BaseEntity
{
    public Guid CheckInId { get; set; }
    public Guid BookingId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid GroupId { get; set; }
    public Guid ReportedByUserId { get; set; }

    [Required]
    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;

    public DamageSeverity Severity { get; set; }
    public DamageLocation Location { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? EstimatedCost { get; set; }

    public DamageReportStatus Status { get; set; } = DamageReportStatus.Reported;

    [StringLength(1000)]
    public string? Notes { get; set; }

    [StringLength(2000)]
    public string? PhotoIdsJson { get; set; }

    public Guid? ExpenseId { get; set; }

    public Guid? ResolvedByUserId { get; set; }
    public DateTime? ResolvedAt { get; set; }

    // Navigation
    public virtual CheckIn CheckIn { get; set; } = null!;
}
