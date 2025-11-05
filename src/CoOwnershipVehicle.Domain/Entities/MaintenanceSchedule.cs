using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CoOwnershipVehicle.Domain.Enums;

namespace CoOwnershipVehicle.Domain.Entities;

/// <summary>
/// Represents a scheduled maintenance task for a vehicle
/// </summary>
public class MaintenanceSchedule : BaseEntity
{
    /// <summary>
    /// Foreign key to the Vehicle being maintained
    /// </summary>
    [Required]
    public Guid VehicleId { get; set; }

    /// <summary>
    /// Type of maintenance service to be performed
    /// </summary>
    [Required]
    public ServiceType ServiceType { get; set; }

    /// <summary>
    /// Date when the maintenance is scheduled
    /// </summary>
    [Required]
    public DateTime ScheduledDate { get; set; }

    /// <summary>
    /// Current status of the scheduled maintenance
    /// </summary>
    [Required]
    public MaintenanceStatus Status { get; set; } = MaintenanceStatus.Scheduled;

    /// <summary>
    /// Estimated cost of the maintenance service
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? EstimatedCost { get; set; }

    /// <summary>
    /// Estimated duration of the service in minutes
    /// </summary>
    [Required]
    public int EstimatedDuration { get; set; }

    /// <summary>
    /// Name of the service provider or shop
    /// </summary>
    [StringLength(200)]
    public string? ServiceProvider { get; set; }

    /// <summary>
    /// Additional notes or instructions for the maintenance
    /// </summary>
    [StringLength(1000)]
    public string? Notes { get; set; }

    /// <summary>
    /// Priority level of this maintenance task
    /// </summary>
    [Required]
    public MaintenancePriority Priority { get; set; } = MaintenancePriority.Medium;

    /// <summary>
    /// User ID who created this schedule
    /// </summary>
    [Required]
    public Guid CreatedBy { get; set; }

    /// <summary>
    /// Cancellation reason if status is Cancelled
    /// </summary>
    [StringLength(500)]
    public string? CancellationReason { get; set; }

    /// <summary>
    /// User ID who cancelled this schedule (if applicable)
    /// </summary>
    public Guid? CancelledBy { get; set; }

    /// <summary>
    /// Original scheduled date (before any rescheduling)
    /// </summary>
    public DateTime? OriginalScheduledDate { get; set; }

    /// <summary>
    /// Number of times this maintenance has been rescheduled
    /// </summary>
    public int RescheduleCount { get; set; } = 0;

    /// <summary>
    /// Last reschedule reason
    /// </summary>
    [StringLength(500)]
    public string? LastRescheduleReason { get; set; }

    /// <summary>
    /// User ID who last rescheduled this maintenance
    /// </summary>
    public Guid? LastRescheduledBy { get; set; }

    // Navigation properties
    /// <summary>
    /// Navigation property to the Vehicle
    /// </summary>
    [ForeignKey(nameof(VehicleId))]
    public virtual Vehicle? Vehicle { get; set; }
}
