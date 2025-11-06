using System.ComponentModel.DataAnnotations;
using CoOwnershipVehicle.Domain.Enums;

namespace CoOwnershipVehicle.Domain.Entities;

/// <summary>
/// Represents a calendar block for vehicle maintenance
/// Used by Booking Service to prevent bookings during maintenance
/// </summary>
public class MaintenanceBlock : BaseEntity
{
    /// <summary>
    /// ID of the maintenance schedule in Vehicle Service
    /// </summary>
    [Required]
    public Guid MaintenanceScheduleId { get; set; }

    /// <summary>
    /// Vehicle being maintained
    /// </summary>
    [Required]
    public Guid VehicleId { get; set; }

    /// <summary>
    /// Group that owns the vehicle
    /// </summary>
    [Required]
    public Guid GroupId { get; set; }

    /// <summary>
    /// Type of maintenance service
    /// </summary>
    [Required]
    public ServiceType ServiceType { get; set; }

    /// <summary>
    /// When the maintenance window starts
    /// </summary>
    [Required]
    public DateTime StartTime { get; set; }

    /// <summary>
    /// When the maintenance window ends
    /// </summary>
    [Required]
    public DateTime EndTime { get; set; }

    /// <summary>
    /// Status of the maintenance (Scheduled, InProgress, Completed, Cancelled)
    /// </summary>
    [Required]
    public MaintenanceStatus Status { get; set; }

    /// <summary>
    /// Priority level
    /// </summary>
    [Required]
    public MaintenancePriority Priority { get; set; }

    /// <summary>
    /// Optional notes about the maintenance
    /// </summary>
    [StringLength(1000)]
    public string? Notes { get; set; }

    // Navigation properties
    public virtual Vehicle? Vehicle { get; set; }
}
