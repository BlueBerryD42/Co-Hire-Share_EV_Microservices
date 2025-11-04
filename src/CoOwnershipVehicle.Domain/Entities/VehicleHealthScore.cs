using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoOwnershipVehicle.Domain.Entities;

/// <summary>
/// Stores historical vehicle health scores for trend analysis
/// </summary>
public class VehicleHealthScore : BaseEntity
{
    /// <summary>
    /// Foreign key to the Vehicle
    /// </summary>
    [Required]
    public Guid VehicleId { get; set; }

    /// <summary>
    /// Overall health score (0-100)
    /// </summary>
    [Required]
    [Range(0, 100)]
    [Column(TypeName = "decimal(5,2)")]
    public decimal OverallScore { get; set; }

    /// <summary>
    /// Health category at this point in time
    /// </summary>
    [Required]
    public HealthCategory Category { get; set; }

    /// <summary>
    /// Date when this score was calculated
    /// </summary>
    [Required]
    public DateTime CalculatedAt { get; set; }

    // Component scores (individual breakdown)

    /// <summary>
    /// Maintenance adherence score (0-30)
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal MaintenanceScore { get; set; }

    /// <summary>
    /// Odometer vs age score (0-20)
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal OdometerAgeScore { get; set; }

    /// <summary>
    /// Damage reports score (0-20)
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal DamageScore { get; set; }

    /// <summary>
    /// Service frequency score (0-15)
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal ServiceFrequencyScore { get; set; }

    /// <summary>
    /// Vehicle age score (0-10)
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal VehicleAgeScore { get; set; }

    /// <summary>
    /// Inspection results score (0-5)
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal InspectionScore { get; set; }

    // Metadata

    /// <summary>
    /// Odometer reading at time of calculation
    /// </summary>
    public int OdometerAtCalculation { get; set; }

    /// <summary>
    /// Number of overdue maintenance items at time of calculation
    /// </summary>
    public int OverdueMaintenanceCount { get; set; }

    /// <summary>
    /// Number of damage reports at time of calculation
    /// </summary>
    public int DamageReportCount { get; set; }

    /// <summary>
    /// Optional note about this score calculation
    /// </summary>
    [StringLength(500)]
    public string? Note { get; set; }

    // Navigation properties

    /// <summary>
    /// Navigation property to the Vehicle
    /// </summary>
    [ForeignKey(nameof(VehicleId))]
    public virtual Vehicle? Vehicle { get; set; }
}

/// <summary>
/// Health category enum
/// </summary>
public enum HealthCategory
{
    Critical = 0,   // 0-19
    Poor = 1,       // 20-39
    Fair = 2,       // 40-59
    Good = 3,       // 60-79
    Excellent = 4   // 80-100
}
