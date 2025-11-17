using CoOwnershipVehicle.Booking.Api.Contracts;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoOwnershipVehicle.Booking.Api.Controllers;

[ApiController]
[Route("api/damage-reports")]
[Authorize]
public class DamageReportsController : ControllerBase
{
    private readonly IDamageReportService _damageReportService;
    private readonly ILogger<DamageReportsController> _logger;

    public DamageReportsController(IDamageReportService damageReportService, ILogger<DamageReportsController> logger)
    {
        _damageReportService = damageReportService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieve all damage reports for a booking.
    /// </summary>
    [HttpGet("by-booking/{bookingId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<DamageReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByBooking(Guid bookingId, CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();
            var reports = await _damageReportService.GetByBookingAsync(bookingId, userId, cancellationToken);
            return Ok(reports);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving damage reports for booking {BookingId}", bookingId);
            return StatusCode(500, new { message = "An error occurred while retrieving damage reports" });
        }
    }

    /// <summary>
    /// Retrieve all damage reports for a vehicle.
    /// </summary>
    [HttpGet("by-vehicle/{vehicleId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<DamageReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetByVehicle(Guid vehicleId, CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();
            var reports = await _damageReportService.GetByVehicleAsync(vehicleId, userId, cancellationToken);
            return Ok(reports);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving damage reports for vehicle {VehicleId}", vehicleId);
            return StatusCode(500, new { message = "An error occurred while retrieving damage reports" });
        }
    }

    /// <summary>
    /// Update the status of a damage report (admin only).
    /// </summary>
    [HttpPut("{reportId:guid}/status")]
    [Authorize(Roles = "SystemAdmin,GroupAdmin")]
    [ProducesResponseType(typeof(DamageReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(Guid reportId, [FromBody] UpdateDamageReportStatusDto request, CancellationToken cancellationToken)
    {
        try
        {
            if (request == null)
            {
                return BadRequest(new { message = "Status update payload is required." });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = GetCurrentUserId();
            var result = await _damageReportService.UpdateStatusAsync(reportId, userId, request, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating damage report {ReportId}", reportId);
            return StatusCode(500, new { message = "An error occurred while updating the damage report" });
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
