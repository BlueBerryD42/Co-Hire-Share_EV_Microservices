using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Domain.Enums;
using CoOwnershipVehicle.Vehicle.Api.Services;
using CoOwnershipVehicle.Vehicle.Api.DTOs;
using System.Security.Claims;

namespace CoOwnershipVehicle.Vehicle.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MaintenanceController : ControllerBase
{
    private readonly IMaintenanceService _maintenanceService;
    private readonly ILogger<MaintenanceController> _logger;

    public MaintenanceController(IMaintenanceService maintenanceService, ILogger<MaintenanceController> logger)
    {
        _maintenanceService = maintenanceService;
        _logger = logger;
    }

    #region MaintenanceSchedule Endpoints

    /// <summary>
    /// Get maintenance schedule by ID
    /// </summary>
    [HttpGet("schedules/{id:guid}")]
    public async Task<IActionResult> GetSchedule(Guid id)
    {
        var schedule = await _maintenanceService.GetScheduleByIdAsync(id);
        if (schedule == null)
            return NotFound(new { message = "Maintenance schedule not found" });

        return Ok(schedule);
    }

    /// <summary>
    /// Get maintenance schedules by status
    /// </summary>
    [HttpGet("schedules/status/{status}")]
    public async Task<IActionResult> GetSchedulesByStatus(MaintenanceStatus status)
    {
        var schedules = await _maintenanceService.GetSchedulesByStatusAsync(status);
        return Ok(schedules);
    }

    /// <summary>
    /// Get all overdue maintenance schedules
    /// </summary>
    [HttpGet("schedules/overdue")]
    public async Task<IActionResult> GetOverdueSchedules()
    {
        var schedules = await _maintenanceService.GetOverdueSchedulesAsync();
        return Ok(schedules);
    }

    /// <summary>
    /// Get maintenance schedule for a vehicle
    /// </summary>
    /// <remarks>
    /// Returns all scheduled maintenance (future and in-progress by default).
    /// Supports filtering by status and pagination.
    ///
    /// Query parameters:
    /// - status: Filter by maintenance status (0=Scheduled, 1=InProgress, 2=Completed, 3=Cancelled, 4=Overdue)
    /// - pageNumber: Page number (default: 1)
    /// - pageSize: Items per page (default: 20, max: 100)
    ///
    /// Returns sorted by scheduled date (nearest first).
    /// </remarks>
    [HttpGet("vehicle/{vehicleId:guid}")]
    [ProducesResponseType(typeof(MaintenanceScheduleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVehicleMaintenanceSchedule(
        Guid vehicleId,
        [FromQuery] MaintenanceStatus? status = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var userId = GetCurrentUserId();
            var accessToken = GetAccessToken();

            var query = new MaintenanceScheduleQuery
            {
                Status = status,
                PageNumber = pageNumber,
                PageSize = Math.Min(pageSize, 100) // Max 100 items per page
            };

            var result = await _maintenanceService.GetVehicleMaintenanceScheduleAsync(vehicleId, query, userId, accessToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to vehicle maintenance schedule");
            return StatusCode(403, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving vehicle maintenance schedule");
            return StatusCode(500, new { message = "An error occurred while retrieving maintenance schedule" });
        }
    }

    /// <summary>
    /// Get maintenance history for a vehicle
    /// </summary>
    /// <remarks>
    /// Returns all completed maintenance records with cost statistics.
    /// Supports filtering by service type, date range, and pagination.
    ///
    /// Query parameters:
    /// - serviceType: Filter by service type (0=OilChange, 1=TireRotation, etc.)
    /// - startDate: Filter records from this date (ISO 8601 format)
    /// - endDate: Filter records until this date (ISO 8601 format)
    /// - pageNumber: Page number (default: 1)
    /// - pageSize: Items per page (default: 20, max: 100)
    ///
    /// Returns sorted by service date (most recent first) with cost statistics.
    ///
    /// Sample request:
    ///     GET /api/maintenance/history/{vehicleId}?serviceType=0&amp;startDate=2024-01-01&amp;pageSize=10
    /// </remarks>
    [HttpGet("history/{vehicleId:guid}")]
    [ProducesResponseType(typeof(MaintenanceHistoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVehicleMaintenanceHistory(
        Guid vehicleId,
        [FromQuery] ServiceType? serviceType = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var userId = GetCurrentUserId();
            var accessToken = GetAccessToken();

            var query = new MaintenanceHistoryQuery
            {
                ServiceType = serviceType,
                StartDate = startDate,
                EndDate = endDate,
                PageNumber = pageNumber,
                PageSize = Math.Min(pageSize, 100) // Max 100 items per page
            };

            var result = await _maintenanceService.GetVehicleMaintenanceHistoryAsync(vehicleId, query, userId, accessToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to vehicle maintenance history");
            return StatusCode(403, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving vehicle maintenance history");
            return StatusCode(500, new { message = "An error occurred while retrieving maintenance history" });
        }
    }

    /// <summary>
    /// Schedule maintenance with conflict detection and validation
    /// </summary>
    /// <remarks>
    /// Validates user membership, checks for conflicts with bookings and other maintenance schedules,
    /// updates vehicle status if maintenance is imminent (within 24 hours), publishes events,
    /// and sends notifications to group members.
    ///
    /// Sample request:
    ///
    ///     POST /api/maintenance/schedule
    ///     {
    ///         "vehicleId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    ///         "serviceType": 1,
    ///         "scheduledDate": "2025-11-01T10:00:00Z",
    ///         "estimatedDuration": 120,
    ///         "serviceProvider": "ABC Auto Service",
    ///         "notes": "Regular maintenance check",
    ///         "priority": 1,
    ///         "estimatedCost": 150.00,
    ///         "forceSchedule": false
    ///     }
    ///
    /// Returns 409 Conflict if scheduling conflicts exist (unless forceSchedule is true and user is admin)
    /// Returns 403 Forbidden if user is not a member of the vehicle's group
    /// Returns 400 Bad Request if scheduled date is not in the future
    /// </remarks>
    [HttpPost("schedule")]
    [ProducesResponseType(typeof(ScheduleMaintenanceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ScheduleMaintenance([FromBody] ScheduleMaintenanceRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var userId = GetCurrentUserId();
            var accessToken = GetAccessToken();
            var isAdmin = User.IsInRole("Admin");

            var response = await _maintenanceService.ScheduleMaintenanceAsync(request, userId, accessToken, isAdmin);

            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized attempt to schedule maintenance");
            return StatusCode(403, new { message = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("conflicts detected"))
        {
            _logger.LogWarning(ex, "Maintenance scheduling conflict");
            return StatusCode(409, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid maintenance schedule request");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling maintenance");
            return StatusCode(500, new { message = "An error occurred while scheduling maintenance" });
        }
    }

    /// <summary>
    /// Update an existing maintenance schedule
    /// </summary>
    [HttpPut("schedules/{id:guid}")]
    public async Task<IActionResult> UpdateSchedule(Guid id, [FromBody] MaintenanceSchedule schedule)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var updated = await _maintenanceService.UpdateScheduleAsync(id, schedule);
            if (updated == null)
                return NotFound(new { message = "Maintenance schedule not found" });

            return Ok(updated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating maintenance schedule {ScheduleId}", id);
            return StatusCode(500, new { message = "An error occurred while updating the maintenance schedule" });
        }
    }

    /// <summary>
    /// Delete a maintenance schedule
    /// </summary>
    [HttpDelete("schedules/{id:guid}")]
    public async Task<IActionResult> DeleteSchedule(Guid id)
    {
        try
        {
            var deleted = await _maintenanceService.DeleteScheduleAsync(id);
            if (!deleted)
                return NotFound(new { message = "Maintenance schedule not found" });

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting maintenance schedule {ScheduleId}", id);
            return StatusCode(500, new { message = "An error occurred while deleting the maintenance schedule" });
        }
    }

    /// <summary>
    /// Update maintenance schedule status
    /// </summary>
    [HttpPatch("schedules/{id:guid}/status")]
    public async Task<IActionResult> UpdateScheduleStatus(Guid id, [FromBody] UpdateStatusDto statusDto)
    {
        try
        {
            var updated = await _maintenanceService.UpdateScheduleStatusAsync(id, statusDto.Status);
            if (!updated)
                return NotFound(new { message = "Maintenance schedule not found" });

            return Ok(new { message = "Status updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating schedule status {ScheduleId}", id);
            return StatusCode(500, new { message = "An error occurred while updating the schedule status" });
        }
    }

    #endregion

    #region MaintenanceRecord Endpoints

    /// <summary>
    /// Get maintenance record by ID
    /// </summary>
    [HttpGet("records/{id:guid}")]
    public async Task<IActionResult> GetRecord(Guid id)
    {
        var record = await _maintenanceService.GetRecordByIdAsync(id);
        if (record == null)
            return NotFound(new { message = "Maintenance record not found" });

        return Ok(record);
    }

    /// <summary>
    /// Get the latest maintenance record for a specific service type
    /// </summary>
    [HttpGet("records/vehicle/{vehicleId:guid}/type/{serviceType}/latest")]
    public async Task<IActionResult> GetLatestRecordByType(Guid vehicleId, ServiceType serviceType)
    {
        var record = await _maintenanceService.GetLatestRecordByTypeAsync(vehicleId, serviceType);
        if (record == null)
            return NotFound(new { message = "No maintenance record found for this service type" });

        return Ok(record);
    }

    /// <summary>
    /// Create a new maintenance record
    /// </summary>
    [HttpPost("records")]
    public async Task<IActionResult> CreateRecord([FromBody] MaintenanceRecord record)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var userId = GetCurrentUserId();
            record.PerformedBy = userId;

            var created = await _maintenanceService.CreateRecordAsync(record);
            return CreatedAtAction(nameof(GetRecord), new { id = created.Id }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating maintenance record");
            return StatusCode(500, new { message = "An error occurred while creating the maintenance record" });
        }
    }

    /// <summary>
    /// Update an existing maintenance record
    /// </summary>
    [HttpPut("records/{id:guid}")]
    public async Task<IActionResult> UpdateRecord(Guid id, [FromBody] MaintenanceRecord record)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var updated = await _maintenanceService.UpdateRecordAsync(id, record);
            if (updated == null)
                return NotFound(new { message = "Maintenance record not found" });

            return Ok(updated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating maintenance record {RecordId}", id);
            return StatusCode(500, new { message = "An error occurred while updating the maintenance record" });
        }
    }

    /// <summary>
    /// Delete a maintenance record
    /// </summary>
    [HttpDelete("records/{id:guid}")]
    public async Task<IActionResult> DeleteRecord(Guid id)
    {
        try
        {
            var deleted = await _maintenanceService.DeleteRecordAsync(id);
            if (!deleted)
                return NotFound(new { message = "Maintenance record not found" });

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting maintenance record {RecordId}", id);
            return StatusCode(500, new { message = "An error occurred while deleting the maintenance record" });
        }
    }

    #endregion

    #region Helper Endpoints

    /// <summary>
    /// Get total maintenance cost for a vehicle
    /// </summary>
    [HttpGet("costs/vehicle/{vehicleId:guid}")]
    public async Task<IActionResult> GetTotalMaintenanceCost(Guid vehicleId, [FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var totalCost = await _maintenanceService.GetTotalMaintenanceCostAsync(vehicleId, startDate, endDate);
            return Ok(new { vehicleId, totalCost, startDate, endDate });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating maintenance cost for vehicle {VehicleId}", vehicleId);
            return StatusCode(500, new { message = "An error occurred while calculating maintenance cost" });
        }
    }

    #endregion

    #region Private Helpers

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid user ID in token");
        }
        return userId;
    }

    private string GetAccessToken()
    {
        var authHeader = HttpContext.Request.Headers["Authorization"].ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            throw new UnauthorizedAccessException("Missing or invalid authorization header");
        }
        return authHeader.Substring("Bearer ".Length).Trim();
    }

    #endregion
}

/// <summary>
/// DTO for updating maintenance schedule status
/// </summary>
public class UpdateStatusDto
{
    public MaintenanceStatus Status { get; set; }
}
