using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using CoOwnershipVehicle.Booking.Api.Contracts;
using CoOwnershipVehicle.Booking.Api.DTOs;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace CoOwnershipVehicle.Booking.Api.Controllers;

[ApiController]
[Route("api/booking-templates")]
public class BookingTemplatesController : ControllerBase
{
    private readonly IBookingTemplateService _bookingTemplateService;

    public BookingTemplatesController(IBookingTemplateService bookingTemplateService)
    {
        _bookingTemplateService = bookingTemplateService;
    }

    private Guid GetUserId()
    {
        // This is a placeholder. In a real application, you would get the user ID from the authenticated user's claims.
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            throw new InvalidOperationException("User ID claim is missing.");
        return Guid.Parse(userId);
    }

    [HttpPost]
    public async Task<IActionResult> CreateBookingTemplate([FromBody] CreateBookingTemplateRequest request)
    {
        var userId = GetUserId();
        var template = await _bookingTemplateService.CreateBookingTemplateAsync(request, userId);
        return CreatedAtAction(nameof(GetBookingTemplate), new { id = template.Id }, template);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BookingTemplateResponse>>> GetBookingTemplates()
    {
        var userId = GetUserId();
        var templates = await _bookingTemplateService.GetUserBookingTemplatesAsync(userId);
        return Ok(templates);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<BookingTemplateResponse>> GetBookingTemplate(Guid id)
    {
        var userId = GetUserId();
        var template = await _bookingTemplateService.GetBookingTemplateByIdAsync(id, userId);
        if (template == null)
        {
            return NotFound();
        }
        return Ok(template);
    }

    [HttpPost("from-template/{templateId}")]
    public async Task<IActionResult> CreateBookingFromTemplate(Guid templateId, [FromBody] CreateBookingFromTemplateRequest request)
    {
        var userId = GetUserId();
        var bookingResponse = await _bookingTemplateService.CreateBookingFromTemplateAsync(templateId, request, userId);
        return CreatedAtAction("GetBooking", "Booking", new { id = bookingResponse.Id }, bookingResponse); // Assuming a BookingController with GetBooking action
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateBookingTemplate(Guid id, [FromBody] UpdateBookingTemplateRequest request)
    {
        var userId = GetUserId();
        var updatedTemplate = await _bookingTemplateService.UpdateBookingTemplateAsync(id, request, userId);
        if (updatedTemplate == null)
        {
            return NotFound();
        }
        return Ok(updatedTemplate);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteBookingTemplate(Guid id)
    {
        var userId = GetUserId();
        await _bookingTemplateService.DeleteBookingTemplateAsync(id, userId);
        return NoContent();
    }
}

public class CreateBookingFromTemplateRequest
{
    [Required]
    public DateTime StartDateTime { get; set; }
    public Guid? VehicleId { get; set; } // Optional override for template's vehicle
}