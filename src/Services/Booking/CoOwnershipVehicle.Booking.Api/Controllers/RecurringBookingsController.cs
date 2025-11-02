using System;
using System.Security.Claims;
using System.Threading.Tasks;
using CoOwnershipVehicle.Booking.Api.Contracts;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CoOwnershipVehicle.Booking.Api.Controllers;

[ApiController]
[Route("api/booking/recurring")]
public class RecurringBookingsController : ControllerBase
{
    private readonly IRecurringBookingService _recurringBookingService;
    private readonly ILogger<RecurringBookingsController> _logger;

    public RecurringBookingsController(
        IRecurringBookingService recurringBookingService,
        ILogger<RecurringBookingsController> logger)
    {
        _recurringBookingService = recurringBookingService;
        _logger = logger;
    }

    private Guid GetUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            throw new InvalidOperationException("User ID not found in claims.");
        }

        return Guid.Parse(userId);
    }

    [HttpPost]
    public async Task<IActionResult> CreateRecurringBooking([FromBody] CreateRecurringBookingDto createDto)
    {
        try
        {
            var userId = GetUserId();
            var recurringBooking = await _recurringBookingService.CreateAsync(createDto, userId);

            return CreatedAtAction(nameof(GetRecurringBooking), new { id = recurringBooking.Id }, recurringBooking);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized recurring booking creation attempt.");
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogInformation(ex, "Recurring booking conflict detected during creation.");
            return Conflict(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogInformation(ex, "Recurring booking creation failed due to missing resource.");
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while creating recurring booking.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while creating the recurring booking." });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetRecurringBooking(Guid id)
    {
        try
        {
            var userId = GetUserId();
            var recurringBooking = await _recurringBookingService.GetByIdAsync(id, userId);

            return Ok(recurringBooking);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized recurring booking access attempt. RecurringBookingId: {RecurringBookingId}", id);
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogInformation(ex, "Recurring booking not found. RecurringBookingId: {RecurringBookingId}", id);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while fetching recurring booking {RecurringBookingId}.", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving the recurring booking." });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateRecurringBooking(Guid id, [FromBody] UpdateRecurringBookingDto updateDto)
    {
        try
        {
            var userId = GetUserId();
            var recurringBooking = await _recurringBookingService.UpdateAsync(id, updateDto, userId);

            return Ok(recurringBooking);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized recurring booking update attempt. RecurringBookingId: {RecurringBookingId}", id);
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogInformation(ex, "Recurring booking conflict detected during update. RecurringBookingId: {RecurringBookingId}", id);
            return Conflict(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogInformation(ex, "Recurring booking not found during update. RecurringBookingId: {RecurringBookingId}", id);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while updating recurring booking {RecurringBookingId}.", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while updating the recurring booking." });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteRecurringBooking(Guid id)
    {
        try
        {
            var userId = GetUserId();
            await _recurringBookingService.CancelAsync(id, userId);

            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized recurring booking cancellation attempt. RecurringBookingId: {RecurringBookingId}", id);
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogInformation(ex, "Recurring booking not found during cancellation. RecurringBookingId: {RecurringBookingId}", id);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while cancelling recurring booking {RecurringBookingId}.", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while cancelling the recurring booking." });
        }
    }
}
