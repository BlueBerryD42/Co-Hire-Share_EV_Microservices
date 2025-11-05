using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoOwnershipVehicle.Domain.Entities;

public class MaintenanceRecord : BaseEntity
{
	public Guid VehicleId { get; set; }
	public Guid? GroupId { get; set; }

	[Required]
	public MaintenanceServiceType ServiceType { get; set; }

	[Required]
	public MaintenanceStatus Status { get; set; } = MaintenanceStatus.Scheduled;

	[Required]
	public DateTime ScheduledDate { get; set; }

	public DateTime? ServiceCompletedDate { get; set; }

	[StringLength(200)]
	public string? Provider { get; set; }

	[StringLength(500)]
	public string? Notes { get; set; }

	[Column(TypeName = "decimal(18,2)")]
	public decimal? EstimatedCost { get; set; }

	public int? EstimatedDurationMinutes { get; set; }

	[Column(TypeName = "decimal(18,2)")]
	public decimal? ActualCost { get; set; }

	public int? ActualDurationMinutes { get; set; }

	public MaintenancePriority Priority { get; set; } = MaintenancePriority.Medium;

	// Details for history
	public int? OdometerAtService { get; set; }

	[StringLength(2000)]
	public string? WorkPerformed { get; set; }

	[StringLength(2000)]
	public string? PartsUsed { get; set; }

	// Link to financials if any
	public Guid? ExpenseId { get; set; }

	// Navigation
	public virtual Vehicle Vehicle { get; set; } = null!;
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


