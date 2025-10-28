using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Domain.Enums;
using CoOwnershipVehicle.Vehicle.Api.Services;

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
    /// Get all maintenance schedules for a vehicle
    /// </summary>
    [HttpGet("schedules/vehicle/{vehicleId:guid}")]
    public async Task<IActionResult> GetSchedulesByVehicle(Guid vehicleId)
    {
        var schedules = await _maintenanceService.GetSchedulesByVehicleIdAsync(vehicleId);
        return Ok(schedules);
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
    /// Create a new maintenance schedule
    /// </summary>
    [HttpPost("schedules")]
    public async Task<IActionResult> CreateSchedule([FromBody] MaintenanceSchedule schedule)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var userId = GetCurrentUserId();
            schedule.CreatedBy = userId;

            var created = await _maintenanceService.CreateScheduleAsync(schedule);
            return CreatedAtAction(nameof(GetSchedule), new { id = created.Id }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating maintenance schedule");
            return StatusCode(500, new { message = "An error occurred while creating the maintenance schedule" });
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
    /// Get all maintenance records for a vehicle
    /// </summary>
    [HttpGet("records/vehicle/{vehicleId:guid}")]
    public async Task<IActionResult> GetRecordsByVehicle(Guid vehicleId)
    {
        var records = await _maintenanceService.GetRecordsByVehicleIdAsync(vehicleId);
        return Ok(records);
    }

    /// <summary>
    /// Get maintenance records by service type
    /// </summary>
    [HttpGet("records/vehicle/{vehicleId:guid}/type/{serviceType}")]
    public async Task<IActionResult> GetRecordsByServiceType(Guid vehicleId, ServiceType serviceType)
    {
        var records = await _maintenanceService.GetRecordsByServiceTypeAsync(vehicleId, serviceType);
        return Ok(records);
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

    /// <summary>
    /// Get maintenance history for a vehicle
    /// </summary>
    [HttpGet("history/vehicle/{vehicleId:guid}")]
    public async Task<IActionResult> GetMaintenanceHistory(Guid vehicleId, [FromQuery] int? limit = null)
    {
        try
        {
            var history = await _maintenanceService.GetMaintenanceHistoryAsync(vehicleId, limit);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance history for vehicle {VehicleId}", vehicleId);
            return StatusCode(500, new { message = "An error occurred while retrieving maintenance history" });
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

    #endregion
}

/// <summary>
/// DTO for updating maintenance schedule status
/// </summary>
public class UpdateStatusDto
{
    public MaintenanceStatus Status { get; set; }
}
