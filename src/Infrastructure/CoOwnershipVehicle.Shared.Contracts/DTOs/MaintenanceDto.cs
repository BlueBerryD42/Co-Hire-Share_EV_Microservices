using System.ComponentModel.DataAnnotations;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Domain.Enums;

namespace CoOwnershipVehicle.Shared.Contracts.DTOs;

public class MaintenanceScheduleRequestDto
{
	public MaintenanceStatus? Status { get; set; }
	public int Page { get; set; } = 1;
	public int PageSize { get; set; } = 20;
}

public class MaintenanceHistoryRequestDto
{
	public MaintenanceServiceType? ServiceType { get; set; }
	public DateTime? FromDate { get; set; }
	public DateTime? ToDate { get; set; }
	public int Page { get; set; } = 1;
	public int PageSize { get; set; } = 20;
}

public class PagedResponseDto<T>
{
	public List<T> Items { get; set; } = new();
	public int TotalCount { get; set; }
	public int Page { get; set; }
	public int PageSize { get; set; }
	public int TotalPages { get; set; }
}

public class MaintenanceScheduleItemDto
{
	public Guid Id { get; set; }
	public Guid VehicleId { get; set; }
	public MaintenanceServiceType ServiceType { get; set; }
	public DateTime ScheduledDate { get; set; }
	public string? Provider { get; set; }
	public MaintenanceStatus Status { get; set; }
	public decimal? EstimatedCost { get; set; }
	public int? EstimatedDurationMinutes { get; set; }
	public MaintenancePriority Priority { get; set; }
}

public class MaintenanceHistoryItemDto
{
	public Guid Id { get; set; }
	public Guid VehicleId { get; set; }
	public MaintenanceServiceType ServiceType { get; set; }
	public DateTime ServiceCompletedDate { get; set; }
	public string? Provider { get; set; }
	public decimal? ActualCost { get; set; }
	public int? ActualDurationMinutes { get; set; }
	public string? WorkPerformed { get; set; }
	public string? PartsUsed { get; set; }
	public int? OdometerAtService { get; set; }
	public Guid? ExpenseId { get; set; }
}

public class MaintenanceTimelineItemDto
{
	public DateTime Date { get; set; }
	public string Direction { get; set; } = "past"; // past or future
	public MaintenanceStatus Status { get; set; }
	public MaintenanceServiceType ServiceType { get; set; }
	public string? Label { get; set; }
}

public class MaintenanceCostStatsDto
{
	public decimal TotalAllTime { get; set; }
	public decimal TotalYear { get; set; }
	public decimal TotalMonth { get; set; }
	public List<ServiceTypeCostDto> CostPerServiceType { get; set; } = new();
	public decimal AverageCostPerService { get; set; }
}

public class ServiceTypeCostDto
{
	public MaintenanceServiceType ServiceType { get; set; }
	public decimal TotalCost { get; set; }
	public int Count { get; set; }
}


