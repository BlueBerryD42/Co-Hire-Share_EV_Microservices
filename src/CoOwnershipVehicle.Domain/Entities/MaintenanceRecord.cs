using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoOwnershipVehicle.Domain.Entities
{
    /// <summary>
    /// Represents a detailed maintenance record for a vehicle, including scheduling, completion, and review information.
    /// </summary>
    public class MaintenanceRecord : BaseEntity
    {
        // Core relations
        [Required]
        public Guid VehicleId { get; set; }
        public Guid? GroupId { get; set; }

        // Maintenance classification
        [Required]
        public MaintenanceServiceType ServiceType { get; set; }

        [Required]
        public MaintenanceStatus Status { get; set; } = MaintenanceStatus.Scheduled;

        public MaintenancePriority Priority { get; set; } = MaintenancePriority.Medium;

        // Scheduling
        [Required]
        public DateTime ScheduledDate { get; set; }
        public DateTime? ServiceCompletedDate { get; set; }

        // Provider information
        [StringLength(200)]
        public string? Provider { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        [StringLength(200)]
        public string? ServiceProvider { get; set; }

        // Cost & duration
        [Column(TypeName = "decimal(18,2)")]
        public decimal? EstimatedCost { get; set; }

        public int? EstimatedDurationMinutes { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? ActualCost { get; set; }

        public int? ActualDurationMinutes { get; set; }

        // Vehicle data
        public int? OdometerReading { get; set; }
        public int? OdometerAtService { get; set; }

        // Work details
        [StringLength(2000)]
        public string? WorkPerformed { get; set; }

        [StringLength(2000)]
        public string? PartsUsed { get; set; }

        [StringLength(1000)]
        public string? PartsReplaced { get; set; }

        // Future scheduling
        public DateTime? NextServiceDue { get; set; }
        public int? NextServiceOdometer { get; set; }

        // Review & rating
        [Range(1, 5)]
        public int? ServiceProviderRating { get; set; }

        [StringLength(1000)]
        public string? ServiceProviderReview { get; set; }

        // Meta
        [Range(0, 100)]
        public int CompletionPercentage { get; set; } = 100;

        [Required]
        public Guid PerformedBy { get; set; }

        // Links
        public Guid? ExpenseId { get; set; }

        // Navigation
        [ForeignKey(nameof(VehicleId))]
        public virtual Vehicle Vehicle { get; set; } = null!;

        [ForeignKey(nameof(ExpenseId))]
        public virtual Expense? Expense { get; set; }

        public virtual OwnershipGroup? Group { get; set; }
    }

    public enum MaintenanceServiceType
    {
        GeneralService = 0,
        OilChange = 1,
        TireRotation = 2,
        BrakeService = 3,
        Battery = 4,
        AirFilter = 5,
        Coolant = 6,
        Transmission = 7,
        Diagnostics = 8,
        Repair = 9,
        Other = 10
    }

    public enum MaintenanceStatus
    {
        Scheduled = 0,
        InProgress = 1,
        Completed = 2,
        Cancelled = 3
    }

    public enum MaintenancePriority
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Critical = 3
    }
}
