using CoOwnershipVehicle.Domain.Enums;

namespace CoOwnershipVehicle.Vehicle.Api.DTOs;

/// <summary>
/// DTO for maintenance schedule item
/// </summary>
public class MaintenanceScheduleDto
{
    public Guid Id { get; set; }
    public Guid VehicleId { get; set; }
    public ServiceType ServiceType { get; set; }
    public DateTime ScheduledDate { get; set; }
    public MaintenanceStatus Status { get; set; }
    public decimal? EstimatedCost { get; set; }
    public int EstimatedDuration { get; set; }
    public string? ServiceProvider { get; set; }
    public string? Notes { get; set; }
    public MaintenancePriority Priority { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// DTO for maintenance history item
/// </summary>
public class MaintenanceHistoryDto
{
    public Guid Id { get; set; }
    public Guid VehicleId { get; set; }
    public ServiceType ServiceType { get; set; }
    public DateTime ServiceDate { get; set; }
    public int OdometerReading { get; set; }
    public decimal ActualCost { get; set; }
    public string ServiceProvider { get; set; } = string.Empty;
    public string WorkPerformed { get; set; } = string.Empty;
    public string? PartsReplaced { get; set; }
    public DateTime? NextServiceDue { get; set; }
    public int? NextServiceOdometer { get; set; }
    public Guid? ExpenseId { get; set; }
    public Guid PerformedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Paginated response for maintenance schedule
/// </summary>
public class MaintenanceScheduleResponse
{
    public List<MaintenanceScheduleDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}

/// <summary>
/// Paginated response for maintenance history
/// </summary>
public class MaintenanceHistoryResponse
{
    public List<MaintenanceHistoryDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
    public MaintenanceCostStatistics Statistics { get; set; } = new();
}

/// <summary>
/// Maintenance cost statistics
/// </summary>
public class MaintenanceCostStatistics
{
    public decimal TotalCostAllTime { get; set; }
    public decimal TotalCostThisYear { get; set; }
    public decimal TotalCostThisMonth { get; set; }
    public decimal AverageCostPerService { get; set; }
    public List<CostByServiceType> CostByServiceType { get; set; } = new();
}

/// <summary>
/// Cost breakdown by service type
/// </summary>
public class CostByServiceType
{
    public ServiceType ServiceType { get; set; }
    public int Count { get; set; }
    public decimal TotalCost { get; set; }
    public decimal AverageCost { get; set; }
}

/// <summary>
/// Query parameters for maintenance schedule
/// </summary>
public class MaintenanceScheduleQuery
{
    public MaintenanceStatus? Status { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// Query parameters for maintenance history
/// </summary>
public class MaintenanceHistoryQuery
{
    public ServiceType? ServiceType { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
