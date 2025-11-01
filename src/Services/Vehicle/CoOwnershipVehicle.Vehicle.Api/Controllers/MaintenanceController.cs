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
    private readonly IMaintenancePdfService _pdfService;
    private readonly ILogger<MaintenanceController> _logger;

    public MaintenanceController(
        IMaintenanceService maintenanceService,
        IMaintenancePdfService pdfService,
        ILogger<MaintenanceController> logger)
    {
        _maintenanceService = maintenanceService;
        _pdfService = pdfService;
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

    /// <summary>
    /// Complete a scheduled maintenance
    /// </summary>
    /// <remarks>
    /// Marks a scheduled or in-progress maintenance as completed with detailed record keeping.
    ///
    /// Features:
    /// - Validates user is group member or admin
    /// - Validates maintenance schedule exists and is not already completed
    /// - Validates odometer reading (must be >= previous reading)
    /// - Updates maintenance schedule status to Completed
    /// - Creates detailed maintenance record
    /// - Updates vehicle status back to Available (if it was in Maintenance)
    /// - Updates vehicle odometer reading
    /// - Publishes MaintenanceCompletedEvent
    /// - Sends completion notification to group members
    /// - Optionally creates expense record in Payment service
    /// - Auto-schedules next maintenance if recurring service
    ///
    /// Sample request:
    ///
    ///     PUT /api/maintenance/{id}/complete
    ///     {
    ///         "actualCost": 150.50,
    ///         "odometerReading": 25000,
    ///         "workPerformed": "Oil change, oil filter replacement, air filter replacement, tire rotation",
    ///         "partsReplaced": "Oil filter (OEM), Air filter (OEM)",
    ///         "nextServiceDue": "2026-04-15T10:00:00Z",
    ///         "nextServiceOdometer": 30000,
    ///         "notes": "All fluids checked and topped up. Tire pressure adjusted.",
    ///         "createExpenseRecord": true,
    ///         "expenseCategory": "Maintenance",
    ///         "serviceProviderRating": 5,
    ///         "serviceProviderReview": "Excellent service, completed on time"
    ///     }
    ///
    /// Returns 404 if maintenance schedule not found
    /// Returns 403 if user is not authorized
    /// Returns 400 if validation fails (invalid status, odometer reading, etc.)
    /// Returns 409 if maintenance is already completed
    /// </remarks>
    [HttpPut("{id:guid}/complete")]
    [ProducesResponseType(typeof(CompleteMaintenanceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CompleteMaintenance(Guid id, [FromBody] CompleteMaintenanceRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var userId = GetCurrentUserId();
            var accessToken = GetAccessToken();
            var isAdmin = User.IsInRole("Admin") || User.IsInRole("SystemAdmin");

            var response = await _maintenanceService.CompleteMaintenanceAsync(id, request, userId, accessToken, isAdmin);

            return Ok(response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            _logger.LogWarning(ex, "Maintenance schedule {ScheduleId} not found", id);
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Cannot complete"))
        {
            _logger.LogWarning(ex, "Cannot complete maintenance {ScheduleId}", id);
            return StatusCode(409, new { message = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Invalid odometer"))
        {
            _logger.LogWarning(ex, "Invalid odometer reading for maintenance {ScheduleId}", id);
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized attempt to complete maintenance {ScheduleId}", id);
            return StatusCode(403, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation for maintenance {ScheduleId}", id);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing maintenance {ScheduleId}", id);
            return StatusCode(500, new { message = "An error occurred while completing maintenance" });
        }
    }

    /// <summary>
    /// Get upcoming maintenance within specified days
    /// </summary>
    /// <remarks>
    /// Returns all scheduled and in-progress maintenance within the next X days.
    /// Results are grouped by vehicle and sorted by date (nearest first).
    ///
    /// Sample request:
    ///     GET /api/maintenance/upcoming?days=30&amp;priority=1&amp;serviceType=0
    ///
    /// Query parameters:
    /// - days: Number of days ahead to check (default: 30)
    /// - priority: Filter by priority level (optional)
    /// - serviceType: Filter by service type (optional)
    /// </remarks>
    [HttpGet("upcoming")]
    [ProducesResponseType(typeof(UpcomingMaintenanceResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUpcomingMaintenance(
        [FromQuery] int days = 30,
        [FromQuery] MaintenancePriority? priority = null,
        [FromQuery] ServiceType? serviceType = null)
    {
        try
        {
            var response = await _maintenanceService.GetUpcomingMaintenanceAsync(days, priority, serviceType);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting upcoming maintenance");
            return StatusCode(500, new { message = "An error occurred while retrieving upcoming maintenance" });
        }
    }

    /// <summary>
    /// Get all overdue maintenance
    /// </summary>
    /// <remarks>
    /// Returns all maintenance that are past their scheduled date and not yet completed.
    /// Results are sorted by priority then by days overdue (most overdue first).
    /// Critical items are flagged (Critical priority OR more than 30 days overdue).
    ///
    /// Sample request:
    ///     GET /api/maintenance/overdue
    /// </remarks>
    [HttpGet("overdue")]
    [ProducesResponseType(typeof(OverdueMaintenanceResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOverdueMaintenance()
    {
        try
        {
            var response = await _maintenanceService.GetOverdueMaintenanceAsync();
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting overdue maintenance");
            return StatusCode(500, new { message = "An error occurred while retrieving overdue maintenance" });
        }
    }

    /// <summary>
    /// Reschedule a maintenance appointment
    /// </summary>
    /// <remarks>
    /// Reschedules a maintenance to a new date. Requires group membership or admin role.
    ///
    /// Features:
    /// - Validates user authorization (group member or admin)
    /// - Validates new date is in the future
    /// - Checks for conflicts at the new date/time
    /// - Tracks reschedule history and count
    /// - Stores original scheduled date
    /// - Publishes MaintenanceRescheduledEvent
    /// - Sends notifications to group members
    ///
    /// Sample request:
    ///     POST /api/maintenance/{id}/reschedule
    ///     {
    ///         "newScheduledDate": "2025-11-15T10:00:00Z",
    ///         "reason": "Service provider requested different date due to parts availability",
    ///         "forceReschedule": false
    ///     }
    ///
    /// Returns 404 if maintenance schedule not found
    /// Returns 403 if unauthorized
    /// Returns 400 if new date is invalid
    /// Returns 409 if conflicts detected and forceReschedule=false
    /// </remarks>
    [HttpPost("{id:guid}/reschedule")]
    [ProducesResponseType(typeof(RescheduleMaintenanceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RescheduleMaintenance(Guid id, [FromBody] RescheduleMaintenanceRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var userId = GetCurrentUserId();
            var accessToken = GetAccessToken();
            var isAdmin = User.IsInRole("Admin") || User.IsInRole("SystemAdmin");

            var response = await _maintenanceService.RescheduleMaintenanceAsync(id, request, userId, accessToken, isAdmin);

            // If conflicts detected, return 409
            if (response.HasConflicts)
            {
                return StatusCode(409, response);
            }

            return Ok(response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            _logger.LogWarning(ex, "Maintenance schedule {ScheduleId} not found", id);
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized attempt to reschedule maintenance {ScheduleId}", id);
            return StatusCode(403, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation for rescheduling maintenance {ScheduleId}", id);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rescheduling maintenance {ScheduleId}", id);
            return StatusCode(500, new { message = "An error occurred while rescheduling maintenance" });
        }
    }

    /// <summary>
    /// Cancel a scheduled maintenance
    /// </summary>
    /// <remarks>
    /// Cancels a scheduled or in-progress maintenance. Requires group membership or admin role.
    ///
    /// Features:
    /// - Validates user authorization (group member or admin)
    /// - Cannot cancel already completed maintenance
    /// - Updates status to Cancelled
    /// - Records cancellation reason and user
    /// - Reverts vehicle status to Available if it was in Maintenance
    /// - Frees up the time slot
    /// - Publishes MaintenanceCancelledEvent
    /// - Sends notifications to group members
    ///
    /// Sample request:
    ///     DELETE /api/maintenance/{id}
    ///     {
    ///         "cancellationReason": "Service provider is no longer available, will reschedule with different provider"
    ///     }
    ///
    /// Returns 404 if maintenance schedule not found
    /// Returns 403 if unauthorized
    /// Returns 400 if trying to cancel completed maintenance
    /// </remarks>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelMaintenance(Guid id, [FromBody] CancelMaintenanceRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var userId = GetCurrentUserId();
            var accessToken = GetAccessToken();
            var isAdmin = User.IsInRole("Admin") || User.IsInRole("SystemAdmin");

            var success = await _maintenanceService.CancelMaintenanceAsync(id, request, userId, accessToken, isAdmin);

            return Ok(new { message = "Maintenance cancelled successfully", scheduleId = id });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            _logger.LogWarning(ex, "Maintenance schedule {ScheduleId} not found", id);
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized attempt to cancel maintenance {ScheduleId}", id);
            return StatusCode(403, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation for cancelling maintenance {ScheduleId}", id);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling maintenance {ScheduleId}", id);
            return StatusCode(500, new { message = "An error occurred while cancelling maintenance" });
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
    /// Download maintenance completion report as PDF
    /// </summary>
    /// <remarks>
    /// Generates and downloads a PDF report for a completed maintenance record.
    ///
    /// The PDF includes:
    /// - Vehicle information (model, plate number, odometer)
    /// - Service details (type, date, provider, completion percentage)
    /// - Work performed and parts replaced
    /// - Cost information
    /// - Next service due information
    /// - Service provider rating and review (if provided)
    ///
    /// Sample request:
    ///     GET /api/maintenance/records/{recordId}/pdf
    /// </remarks>
    [HttpGet("records/{recordId:guid}/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadMaintenanceReport(Guid recordId)
    {
        try
        {
            var pdfBytes = await _pdfService.GenerateMaintenanceReportPdfAsync(recordId);
            var fileName = $"Maintenance_Report_{recordId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";

            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            _logger.LogWarning(ex, "Maintenance record {RecordId} not found for PDF generation", recordId);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating PDF report for maintenance record {RecordId}", recordId);
            return StatusCode(500, new { message = "An error occurred while generating the PDF report" });
        }
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
