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

/// <summary>
/// Request DTO for completing maintenance
/// </summary>
public class CompleteMaintenanceRequest
{
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Actual cost must be greater than 0")]
    public decimal ActualCost { get; set; }

    [Required]
    [Range(0, int.MaxValue, ErrorMessage = "Odometer reading must be non-negative")]
    public int OdometerReading { get; set; }

    [Required]
    [StringLength(2000, MinimumLength = 10, ErrorMessage = "Work performed description must be between 10 and 2000 characters")]
    public string WorkPerformed { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? PartsReplaced { get; set; }

    public DateTime? NextServiceDue { get; set; }

    [Range(0, int.MaxValue)]
    public int? NextServiceOdometer { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    /// <summary>
    /// Completion percentage for partial completion (0-100). Default 100 = fully completed.
    /// For multi-day services, set < 100 to mark as partial completion.
    /// </summary>
    [Range(0, 100, ErrorMessage = "Completion percentage must be between 0 and 100")]
    public int CompletionPercentage { get; set; } = 100;

    /// <summary>
    /// Create expense record in Payment service
    /// </summary>
    public bool CreateExpenseRecord { get; set; } = false;

    /// <summary>
    /// Expense category for payment service
    /// </summary>
    [StringLength(100)]
    public string? ExpenseCategory { get; set; }

    /// <summary>
    /// Optional service provider rating (1-5 stars)
    /// </summary>
    [Range(1, 5)]
    public int? ServiceProviderRating { get; set; }

    [StringLength(1000)]
    public string? ServiceProviderReview { get; set; }
}

/// <summary>
/// Response DTO for complete maintenance operation
/// </summary>
public class CompleteMaintenanceResponse
{
    public Guid MaintenanceScheduleId { get; set; }
    public Guid MaintenanceRecordId { get; set; }
    public Guid VehicleId { get; set; }
    public MaintenanceStatus Status { get; set; }
    public decimal ActualCost { get; set; }
    public int OdometerReading { get; set; }
    public bool VehicleStatusUpdated { get; set; }
    public bool ExpenseRecordCreated { get; set; }
    public Guid? ExpenseId { get; set; }
    public DateTime? NextServiceDue { get; set; }
    public int? NextServiceOdometer { get; set; }
    public string Message { get; set; } = string.Empty;
}
