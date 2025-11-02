using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using CoOwnershipVehicle.Booking.Api.Contracts;
using CoOwnershipVehicle.Booking.Api.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CoOwnershipVehicle.Booking.Api.Controllers;

[ApiController]
[Route("api/booking")]
public class BookingTemplatesController : ControllerBase
{
    private readonly IBookingTemplateService _bookingTemplateService;
    private readonly ILogger<BookingTemplatesController> _logger;

    public BookingTemplatesController(
        IBookingTemplateService bookingTemplateService,
        ILogger<BookingTemplatesController> logger)
    {
        _bookingTemplateService = bookingTemplateService;
        _logger = logger;
    }

    private Guid GetUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            throw new InvalidOperationException("User ID claim is missing.");
        }

        return Guid.Parse(userId);
    }

    [HttpPost("template")]
    public async Task<IActionResult> CreateBookingTemplate([FromBody] CreateBookingTemplateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var userId = GetUserId();
            var template = await _bookingTemplateService.CreateBookingTemplateAsync(request, userId);

            return CreatedAtAction(nameof(GetBookingTemplate), new { id = template.Id }, template);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized template creation attempt.");
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid template creation request.");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while creating booking template.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while creating the booking template." });
        }
    }

    [HttpGet("templates")]
    public async Task<ActionResult<IReadOnlyList<BookingTemplateResponse>>> GetBookingTemplates()
    {
        try
        {
            var userId = GetUserId();
            var templates = await _bookingTemplateService.GetUserBookingTemplatesAsync(userId);
            return Ok(templates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while retrieving booking templates.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving booking templates." });
        }
    }

    [HttpGet("template/{id:guid}")]
    public async Task<ActionResult<BookingTemplateResponse>> GetBookingTemplate(Guid id)
    {
        try
        {
            var userId = GetUserId();
            var template = await _bookingTemplateService.GetBookingTemplateByIdAsync(id, userId);
            if (template == null)
            {
                return NotFound(new { message = "Booking template not found." });
            }

            return Ok(template);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to booking template {TemplateId}.", id);
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while retrieving booking template {TemplateId}.", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving the booking template." });
        }
    }

    [HttpPost("from-template/{templateId:guid}")]
    public async Task<IActionResult> CreateBookingFromTemplate(Guid templateId, [FromBody] CreateBookingFromTemplateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var userId = GetUserId();
            var bookingResponse = await _bookingTemplateService.CreateBookingFromTemplateAsync(templateId, request, userId);

            return CreatedAtAction("GetBooking", "Booking", new { id = bookingResponse.Id }, bookingResponse);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized booking-from-template attempt. TemplateId: {TemplateId}", templateId);
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogInformation(ex, "Booking template not found. TemplateId: {TemplateId}", templateId);
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogInformation(ex, "Booking conflict while creating from template {TemplateId}.", templateId);
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while creating booking from template {TemplateId}.", templateId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while creating the booking from template." });
        }
    }

    [HttpPut("template/{id:guid}")]
    public async Task<IActionResult> UpdateBookingTemplate(Guid id, [FromBody] UpdateBookingTemplateRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var userId = GetUserId();
            var updatedTemplate = await _bookingTemplateService.UpdateBookingTemplateAsync(id, request, userId);
            if (updatedTemplate == null)
            {
                return NotFound(new { message = "Booking template not found." });
            }

            return Ok(updatedTemplate);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized template update attempt. TemplateId: {TemplateId}", id);
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while updating booking template {TemplateId}.", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while updating the booking template." });
        }
    }

    [HttpDelete("template/{id:guid}")]
    public async Task<IActionResult> DeleteBookingTemplate(Guid id)
    {
        try
        {
            var userId = GetUserId();
            await _bookingTemplateService.DeleteBookingTemplateAsync(id, userId);

            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized template deletion attempt. TemplateId: {TemplateId}", id);
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogInformation(ex, "Booking template not found during deletion. TemplateId: {TemplateId}", id);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while deleting booking template {TemplateId}.", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while deleting the booking template." });
        }
    }
}
