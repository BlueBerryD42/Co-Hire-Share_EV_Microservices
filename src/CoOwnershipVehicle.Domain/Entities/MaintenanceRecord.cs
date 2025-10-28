using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CoOwnershipVehicle.Domain.Enums;

namespace CoOwnershipVehicle.Domain.Entities;

/// <summary>
/// Represents a completed maintenance record for a vehicle
/// </summary>
public class MaintenanceRecord : BaseEntity
{
    /// <summary>
    /// Foreign key to the Vehicle that was serviced
    /// </summary>
    [Required]
    public Guid VehicleId { get; set; }

    /// <summary>
    /// Type of maintenance service that was performed
    /// </summary>
    [Required]
    public ServiceType ServiceType { get; set; }

    /// <summary>
    /// Date when the service was performed
    /// </summary>
    [Required]
    public DateTime ServiceDate { get; set; }

    /// <summary>
    /// Odometer reading at the time of service
    /// </summary>
    [Required]
    public int OdometerReading { get; set; }

    /// <summary>
    /// Actual cost of the service performed
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal ActualCost { get; set; }

    /// <summary>
    /// Name of the service provider or shop that performed the work
    /// </summary>
    [Required]
    [StringLength(200)]
    public string ServiceProvider { get; set; } = string.Empty;

    /// <summary>
    /// Detailed description of the work performed
    /// </summary>
    [Required]
    [StringLength(2000)]
    public string WorkPerformed { get; set; } = string.Empty;

    /// <summary>
    /// List of parts that were replaced during service
    /// </summary>
    [StringLength(1000)]
    public string? PartsReplaced { get; set; }

    /// <summary>
    /// Date when the next service of this type is due
    /// </summary>
    public DateTime? NextServiceDue { get; set; }

    /// <summary>
    /// Odometer reading when the next service should be performed
    /// </summary>
    public int? NextServiceOdometer { get; set; }

    /// <summary>
    /// Optional foreign key to an Expense record if this maintenance incurred an expense
    /// </summary>
    public Guid? ExpenseId { get; set; }

    /// <summary>
    /// User ID who performed or recorded this maintenance
    /// </summary>
    [Required]
    public Guid PerformedBy { get; set; }

    // Navigation properties
    /// <summary>
    /// Navigation property to the Vehicle
    /// </summary>
    [ForeignKey(nameof(VehicleId))]
    public virtual Vehicle? Vehicle { get; set; }

    /// <summary>
    /// Navigation property to the related Expense (if any)
    /// </summary>
    [ForeignKey(nameof(ExpenseId))]
    public virtual Expense? Expense { get; set; }
}
