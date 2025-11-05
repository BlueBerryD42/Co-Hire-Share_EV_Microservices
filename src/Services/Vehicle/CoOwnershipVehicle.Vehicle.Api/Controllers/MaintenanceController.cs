using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CoOwnershipVehicle.Vehicle.Api.Data;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Vehicle.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MaintenanceController : ControllerBase
{
	private readonly VehicleDbContext _context;
	private readonly ILogger<MaintenanceController> _logger;

	public MaintenanceController(VehicleDbContext context, ILogger<MaintenanceController> logger)
	{
		_context = context;
		_logger = logger;
	}

	/// <summary>
	/// Get maintenance schedule for a vehicle (future and in-progress)
	/// </summary>
	[HttpGet("vehicle/{vehicleId:guid}")]
	public async Task<IActionResult> GetSchedule(Guid vehicleId, [FromQuery] MaintenanceStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
	{
		try
		{
			var userId = GetCurrentUserId();

			var vehicle = await _context.Vehicles
				.Include(v => v.Group)!.ThenInclude(g => g!.Members)
				.FirstOrDefaultAsync(v => v.Id == vehicleId);
			if (vehicle == null)
				return NotFound(new { message = "Vehicle not found" });

			var isMember = vehicle.Group != null && vehicle.Group.Members.Any(m => m.UserId == userId);
			if (!isMember)
				return StatusCode(403, new { message = "Unauthorized" });

			var now = DateTime.UtcNow;
			var query = _context.MaintenanceRecords
				.Where(m => m.VehicleId == vehicleId && (m.Status == MaintenanceStatus.Scheduled || m.Status == MaintenanceStatus.InProgress) && m.ScheduledDate >= now.AddDays(-1));

			if (status.HasValue)
				query = query.Where(m => m.Status == status);

			var total = await query.CountAsync();
			var items = await query
				.OrderBy(m => m.ScheduledDate)
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
				.Select(m => new MaintenanceScheduleItemDto
				{
					Id = m.Id,
					VehicleId = m.VehicleId,
					ServiceType = m.ServiceType,
					ScheduledDate = m.ScheduledDate,
					Provider = m.Provider,
					Status = m.Status,
					EstimatedCost = m.EstimatedCost,
					EstimatedDurationMinutes = m.EstimatedDurationMinutes,
					Priority = m.Priority
				})
				.ToListAsync();

			var response = new PagedResponseDto<MaintenanceScheduleItemDto>
			{
				Items = items,
				TotalCount = total,
				Page = page,
				PageSize = pageSize,
				TotalPages = (int)Math.Ceiling(total / (double)pageSize)
			};

			return Ok(response);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error getting maintenance schedule for {VehicleId}", vehicleId);
			return StatusCode(500, new { message = "An error occurred while retrieving maintenance schedule" });
		}
	}

	/// <summary>
	/// Get maintenance history for a vehicle (completed records)
	/// </summary>
	[HttpGet("history/{vehicleId:guid}")]
	public async Task<IActionResult> GetHistory(Guid vehicleId, [FromQuery] MaintenanceServiceType? serviceType, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
	{
		try
		{
			var userId = GetCurrentUserId();

			var vehicle = await _context.Vehicles
				.Include(v => v.Group)!.ThenInclude(g => g!.Members)
				.FirstOrDefaultAsync(v => v.Id == vehicleId);
			if (vehicle == null)
				return NotFound(new { message = "Vehicle not found" });

			var isMember = vehicle.Group != null && vehicle.Group.Members.Any(m => m.UserId == userId);
			if (!isMember)
				return StatusCode(403, new { message = "Unauthorized" });

			var query = _context.MaintenanceRecords
				.Where(m => m.VehicleId == vehicleId && m.Status == MaintenanceStatus.Completed);

			if (serviceType.HasValue)
				query = query.Where(m => m.ServiceType == serviceType);
			if (fromDate.HasValue)
				query = query.Where(m => m.ServiceCompletedDate >= fromDate);
			if (toDate.HasValue)
				query = query.Where(m => m.ServiceCompletedDate <= toDate);

			var total = await query.CountAsync();
			var items = await query
				.OrderByDescending(m => m.ServiceCompletedDate)
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
				.Select(m => new MaintenanceHistoryItemDto
				{
					Id = m.Id,
					VehicleId = m.VehicleId,
					ServiceType = m.ServiceType,
					ServiceCompletedDate = m.ServiceCompletedDate ?? m.UpdatedAt,
					Provider = m.Provider,
					ActualCost = m.ActualCost,
					ActualDurationMinutes = m.ActualDurationMinutes,
					WorkPerformed = m.WorkPerformed,
					PartsUsed = m.PartsUsed,
					OdometerAtService = m.OdometerAtService,
					ExpenseId = m.ExpenseId
				})
				.ToListAsync();

			var response = new PagedResponseDto<MaintenanceHistoryItemDto>
			{
				Items = items,
				TotalCount = total,
				Page = page,
				PageSize = pageSize,
				TotalPages = (int)Math.Ceiling(total / (double)pageSize)
			};

			// Calculate total cost for the filtered range
			var totalCost = await _context.MaintenanceRecords
				.Where(m => m.VehicleId == vehicleId && m.Status == MaintenanceStatus.Completed)
				.Where(m => !serviceType.HasValue || m.ServiceType == serviceType)
				.Where(m => !fromDate.HasValue || m.ServiceCompletedDate >= fromDate)
				.Where(m => !toDate.HasValue || m.ServiceCompletedDate <= toDate)
				.SumAsync(m => m.ActualCost ?? 0);

			Response.Headers["X-Total-Maintenance-Cost"] = totalCost.ToString();

			return Ok(response);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error getting maintenance history for {VehicleId}", vehicleId);
			return StatusCode(500, new { message = "An error occurred while retrieving maintenance history" });
		}
	}

	/// <summary>
	/// Maintenance timeline combining past and future records
	/// </summary>
	[HttpGet("timeline/{vehicleId:guid}")]
	public async Task<IActionResult> GetTimeline(Guid vehicleId)
	{
		try
		{
			var userId = GetCurrentUserId();
			var vehicle = await _context.Vehicles
				.Include(v => v.Group)!.ThenInclude(g => g!.Members)
				.FirstOrDefaultAsync(v => v.Id == vehicleId);
			if (vehicle == null)
				return NotFound(new { message = "Vehicle not found" });
			var isMember = vehicle.Group != null && vehicle.Group.Members.Any(m => m.UserId == userId);
			if (!isMember)
				return StatusCode(403, new { message = "Unauthorized" });

			var now = DateTime.UtcNow;
			var items = await _context.MaintenanceRecords
				.Where(m => m.VehicleId == vehicleId)
				.Select(m => new MaintenanceTimelineItemDto
				{
					Date = m.Status == MaintenanceStatus.Completed ? (m.ServiceCompletedDate ?? m.UpdatedAt) : m.ScheduledDate,
					Direction = (m.Status == MaintenanceStatus.Completed ? "past" : (m.ScheduledDate >= now ? "future" : "past")),
					Status = m.Status,
					ServiceType = m.ServiceType,
					Label = m.Provider
				})
				.OrderBy(i => i.Date)
				.ToListAsync();

			return Ok(items);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error getting maintenance timeline for {VehicleId}", vehicleId);
			return StatusCode(500, new { message = "An error occurred while retrieving maintenance timeline" });
		}
	}

	/// <summary>
	/// Maintenance cost statistics
	/// </summary>
	[HttpGet("stats/{vehicleId:guid}")]
	public async Task<IActionResult> GetCostStats(Guid vehicleId)
	{
		try
		{
			var userId = GetCurrentUserId();
			var vehicle = await _context.Vehicles
				.Include(v => v.Group)!.ThenInclude(g => g!.Members)
				.FirstOrDefaultAsync(v => v.Id == vehicleId);
			if (vehicle == null)
				return NotFound(new { message = "Vehicle not found" });
			var isMember = vehicle.Group != null && vehicle.Group.Members.Any(m => m.UserId == userId);
			if (!isMember)
				return StatusCode(403, new { message = "Unauthorized" });

			var now = DateTime.UtcNow;
			var startOfMonth = new DateTime(now.Year, now.Month, 1);
			var startOfYear = new DateTime(now.Year, 1, 1);

			var completed = _context.MaintenanceRecords.Where(m => m.VehicleId == vehicleId && m.Status == MaintenanceStatus.Completed);

			var totalAllTime = await completed.SumAsync(m => m.ActualCost ?? 0);
			var totalYear = await completed.Where(m => m.ServiceCompletedDate >= startOfYear).SumAsync(m => m.ActualCost ?? 0);
			var totalMonth = await completed.Where(m => m.ServiceCompletedDate >= startOfMonth).SumAsync(m => m.ActualCost ?? 0);

			var perType = await completed
				.GroupBy(m => m.ServiceType)
				.Select(g => new ServiceTypeCostDto
				{
					ServiceType = g.Key,
					TotalCost = g.Sum(x => x.ActualCost ?? 0),
					Count = g.Count()
				})
				.ToListAsync();

			var countAll = await completed.CountAsync();
			var avgCost = countAll > 0 ? totalAllTime / countAll : 0;

			var stats = new MaintenanceCostStatsDto
			{
				TotalAllTime = totalAllTime,
				TotalYear = totalYear,
				TotalMonth = totalMonth,
				CostPerServiceType = perType,
				AverageCostPerService = avgCost
			};

			return Ok(stats);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error getting maintenance stats for {VehicleId}", vehicleId);
			return StatusCode(500, new { message = "An error occurred while retrieving maintenance stats" });
		}
	}

	private Guid GetCurrentUserId()
	{
		var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
		if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
		{
			throw new UnauthorizedAccessException("Invalid user ID in token");
		}
		return userId;
	}
}


