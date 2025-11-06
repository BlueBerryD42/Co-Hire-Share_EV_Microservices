using System.ComponentModel.DataAnnotations;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Shared.Contracts.DTOs;

public class DamageReportDto
{
    public Guid Id { get; set; }
    public Guid CheckInId { get; set; }
    public Guid BookingId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid GroupId { get; set; }
    public Guid ReportedByUserId { get; set; }
    public string Description { get; set; } = string.Empty;
    public DamageSeverity Severity { get; set; }
    public DamageLocation Location { get; set; }
    public decimal? EstimatedCost { get; set; }
    public DamageReportStatus Status { get; set; }
    public string? Notes { get; set; }
    public Guid? ExpenseId { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public IReadOnlyList<Guid> PhotoIds { get; set; } = Array.Empty<Guid>();
}

public class CreateDamageReportDto
{
    [Required]
    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public DamageSeverity Severity { get; set; }

    [Required]
    public DamageLocation Location { get; set; }

    public decimal? EstimatedCost { get; set; }

    public List<Guid> PhotoIds { get; set; } = new();
}

public class UpdateDamageReportStatusDto
{
    [Required]
    public DamageReportStatus Status { get; set; }

    public decimal? EstimatedCost { get; set; }

    public Guid? ExpenseId { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }
}
