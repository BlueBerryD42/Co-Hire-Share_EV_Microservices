using System.ComponentModel.DataAnnotations;
using CoOwnershipVehicle.Domain.Enums;

namespace CoOwnershipVehicle.Vehicle.Api.DTOs;

/// <summary>
/// Request DTO for scheduling vehicle maintenance
/// </summary>
public class ScheduleMaintenanceRequest
{
    [Required]
    public Guid VehicleId { get; set; }

    [Required]
    public ServiceType ServiceType { get; set; }

    [Required]
    public DateTime ScheduledDate { get; set; }

    [Required]
    [Range(1, 1440)] // 1 minute to 24 hours
    public int EstimatedDuration { get; set; }

    [StringLength(200)]
    public string? ServiceProvider { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    [Required]
    public MaintenancePriority Priority { get; set; } = MaintenancePriority.Medium;

    public decimal? EstimatedCost { get; set; }

    /// <summary>
    /// Force schedule even if there are conflicts (admin only)
    /// </summary>
    public bool ForceSchedule { get; set; } = false;
}

/// <summary>
/// Response DTO for schedule maintenance operation
/// </summary>
public class ScheduleMaintenanceResponse
{
    public Guid ScheduleId { get; set; }
    public Guid VehicleId { get; set; }
    public ServiceType ServiceType { get; set; }
    public DateTime ScheduledDate { get; set; }
    public DateTime MaintenanceStartTime { get; set; }
    public DateTime MaintenanceEndTime { get; set; }
    public MaintenanceStatus Status { get; set; }
    public bool VehicleStatusUpdated { get; set; }
    public List<string> Warnings { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// DTO for conflict detection results
/// </summary>
public class MaintenanceConflict
{
    public ConflictType Type { get; set; }
    public Guid ConflictingId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Description { get; set; } = string.Empty;
}

public enum ConflictType
{
    Booking,
    Maintenance,
    VehicleUnavailable
}
