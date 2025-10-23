using CoOwnershipVehicle.Booking.Api.Services;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CoOwnershipVehicle.Booking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CheckInController : ControllerBase
{
    private readonly ICheckInService _checkInService;
    private readonly ILogger<CheckInController> _logger;
    private const long MaxUploadFileSize = 10 * 1024 * 1024;
    private const int MaxPhotosPerCheckIn = 10;

    public CheckInController(ICheckInService checkInService, ILogger<CheckInController> logger)
    {
        _checkInService = checkInService;
        _logger = logger;
    }

    /// <summary>
    /// Get a specific check-in by identifier
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetCheckIn(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _checkInService.GetByIdAsync(id, cancellationToken);
            if (result == null)
            {
                return NotFound(new { message = "Check-in not found" });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving check-in {CheckInId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving the check-in" });
        }
    }

    /// <summary>
    /// Upload photos for a check-in record
    /// </summary>
    [HttpPost("{id:guid}/photos")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(IReadOnlyList<CheckInPhotoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> UploadPhotos(Guid id, [FromForm] List<IFormFile> files, [FromForm] List<string>? photoTypes, [FromForm] List<string>? descriptions, CancellationToken cancellationToken)
    {
        try
        {
            if (files == null || files.Count == 0)
            {
                return BadRequest(new { message = "At least one photo file is required." });
            }

            if (files.Count > MaxPhotosPerCheckIn)
            {
                return BadRequest(new { message = $"Cannot upload more than {MaxPhotosPerCheckIn} photos at a time." });
            }

            var uploadItems = new List<PhotoUploadItem>();

            for (var index = 0; index < files.Count; index++)
            {
                var file = files[index];

                if (file.Length > MaxUploadFileSize)
                {
                    return StatusCode(StatusCodes.Status413PayloadTooLarge, new { message = $"File '{file.FileName}' exceeds the maximum size of 10MB." });
                }

                var photoType = PhotoType.Other;
                if (photoTypes != null && index < photoTypes.Count && !string.IsNullOrWhiteSpace(photoTypes[index]))
                {
                    if (!Enum.TryParse(photoTypes[index], true, out photoType))
                    {
                        return BadRequest(new { message = $"Invalid photo type '{photoTypes[index]}' supplied." });
                    }
                }

                var description = descriptions != null && index < descriptions.Count ? descriptions[index] : null;
                uploadItems.Add(new PhotoUploadItem(file, photoType, description));
            }

            var userId = GetCurrentUserId();
            var uploaded = await _checkInService.UploadPhotosAsync(id, userId, uploadItems, cancellationToken);

            return Ok(uploaded);
        }
        catch (PhotoUploadException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbidden(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading photos for check-in {CheckInId}", id);
            return StatusCode(500, new { message = "An error occurred while uploading photos" });
        }
    }

    /// <summary>
    /// Soft delete a check-in photo
    /// </summary>
    [HttpDelete("{checkInId:guid}/photos/{photoId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePhoto(Guid checkInId, Guid photoId, CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();
            await _checkInService.DeletePhotoAsync(checkInId, photoId, userId, cancellationToken);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbidden(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting photo {PhotoId} for check-in {CheckInId}", photoId, checkInId);
            return StatusCode(500, new { message = "An error occurred while deleting the photo" });
        }
    }

    /// <summary>
    /// Get all check-ins logged for a booking
    /// </summary>
    [HttpGet("booking/{bookingId:guid}")]
    public async Task<IActionResult> GetBookingCheckIns(Guid bookingId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _checkInService.GetByBookingAsync(bookingId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving check-ins for booking {BookingId}", bookingId);
            return StatusCode(500, new { message = "An error occurred while retrieving check-ins" });
        }
    }

    /// <summary>
    /// Start a trip (vehicle check-out)
    /// </summary>
    [HttpPost("start")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> StartTrip([FromBody] StartTripDto request, CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = GetCurrentUserId();
            var result = await _checkInService.StartTripAsync(request, userId, cancellationToken);

            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbidden(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting trip for booking {BookingId}", request.BookingId);
            return StatusCode(500, new { message = "An error occurred while starting the trip" });
        }
    }

    /// <summary>
    /// End a trip (vehicle check-in)
    /// </summary>
    [HttpPost("end")]
    [ProducesResponseType(typeof(TripCompletionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EndTrip([FromBody] EndTripDto request, CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = GetCurrentUserId();
            var result = await _checkInService.EndTripAsync(request, userId, cancellationToken);

            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbidden(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ending trip for booking {BookingId}", request.BookingId);
            return StatusCode(500, new { message = "An error occurred while ending the trip" });
        }
    }

    /// <summary>
    /// Log a new check-in/check-out record
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateCheckIn([FromBody] CreateCheckInDto request, CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = GetCurrentUserId();
            var result = await _checkInService.CreateAsync(request, userId, cancellationToken);

            return CreatedAtAction(nameof(GetCheckIn), new { id = result.Id }, result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbidden(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating check-in for booking {BookingId}", request.BookingId);
            return StatusCode(500, new { message = "An error occurred while creating the check-in" });
        }
    }

    /// <summary>
    /// Update an existing check-in record
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateCheckIn(Guid id, [FromBody] UpdateCheckInDto request, CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = GetCurrentUserId();
            var result = await _checkInService.UpdateAsync(id, request, userId, cancellationToken);

            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbidden(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating check-in {CheckInId}", id);
            return StatusCode(500, new { message = "An error occurred while updating the check-in" });
        }
    }

    /// <summary>
    /// Delete a check-in record
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteCheckIn(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();
            await _checkInService.DeleteAsync(id, userId, cancellationToken);

            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbidden(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting check-in {CheckInId}", id);
            return StatusCode(500, new { message = "An error occurred while deleting the check-in" });
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

    private IActionResult Forbidden(object value)
    {
        return StatusCode(403, value);
    }
}



