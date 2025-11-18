using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using CoOwnershipVehicle.Booking.Api.Contracts;
using CoOwnershipVehicle.Booking.Api.DTOs;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using System.IdentityModel.Tokens.Jwt;


namespace CoOwnershipVehicle.Booking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BookingController : ControllerBase
{
    private readonly IBookingService _bookingService;
    private readonly ILogger<BookingController> _logger;

    public BookingController(IBookingService bookingService, ILogger<BookingController> logger)
    {
        _bookingService = bookingService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new booking
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateBooking([FromBody] CreateBookingDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Validate booking time
            if (createDto.StartAt >= createDto.EndAt)
                return BadRequest(new { message = "Start time must be before end time" });

            if (createDto.StartAt < DateTime.UtcNow.AddMinutes(-30))
                return BadRequest(new { message = "Cannot create bookings in the past" });

            var userId = GetCurrentUserId();
            var booking = await _bookingService.CreateBookingAsync(createDto, userId, createDto.IsEmergency, createDto.EmergencyReason);

            _logger.LogInformation("Booking created: {BookingId} for vehicle {VehicleId}",
                booking.Id, booking.VehicleId);

            return CreatedAtAction(nameof(GetBooking), new { id = booking.Id }, booking);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbidden(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating booking");
            return StatusCode(500, new { message = "An error occurred while creating booking" });
        }
    }

    /// <summary>
    /// Get user's bookings
    /// </summary>
    [HttpGet("my-bookings")]
    public async Task<IActionResult> GetMyBookings([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        try
        {
            var userId = GetCurrentUserId();
            var bookings = await _bookingService.GetUserBookingsAsync(userId, from, to);

            return Ok(bookings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user bookings");
            return StatusCode(500, new { message = "An error occurred while retrieving bookings" });
        }
    }

    /// <summary>
    /// Get all bookings (admin/staff only)
    /// </summary>
    [HttpGet("all")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    public async Task<IActionResult> GetAllBookings([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null, [FromQuery] Guid? userId = null, [FromQuery] Guid? groupId = null)
    {
        try
        {
            var bookings = await _bookingService.GetAllBookingsAsync(from, to, userId, groupId);
            return Ok(bookings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all bookings");
            return StatusCode(500, new { message = "An error occurred while retrieving bookings" });
        }
    }

    /// <summary>
    /// Get historical bookings with associated check-in records
    /// </summary>
    [HttpGet("my-bookings/history")]
    public async Task<IActionResult> GetMyBookingHistory([FromQuery] int limit = 20)
    {
        try
        {
            var userId = GetCurrentUserId();
            var history = await _bookingService.GetUserBookingHistoryAsync(userId, limit);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting booking history for current user");
            return StatusCode(500, new { message = "An error occurred while retrieving booking history" });
        }
    }

    /// <summary>
    /// Get bookings for a specific vehicle
    /// </summary>
    [HttpGet("vehicle/{vehicleId:guid}")]
    public async Task<IActionResult> GetVehicleBookings(
        Guid vehicleId,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        try
        {
            var bookings = await _bookingService.GetVehicleBookingsAsync(vehicleId, from, to);
            return Ok(bookings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting vehicle bookings for {VehicleId}", vehicleId);
            return StatusCode(500, new { message = "An error occurred while retrieving vehicle bookings" });
        }
    }

    /// <summary>
    /// Get a specific booking
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetBooking(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var bookings = await _bookingService.GetUserBookingsAsync(userId);
            var booking = bookings.FirstOrDefault(b => b.Id == id);

            if (booking == null)
                return NotFound(new { message = "Booking not found" });

            return Ok(booking);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting booking {BookingId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving booking" });
        }
    }

    /// <summary>
    /// Check for booking conflicts
    /// </summary>
    [HttpGet("conflicts")]
    public async Task<IActionResult> CheckConflicts(
        [FromQuery] Guid vehicleId,
        [FromQuery] DateTime startAt,
        [FromQuery] DateTime endAt,
        [FromQuery] Guid? excludeBookingId = null)
    {
        try
        {
            var conflicts = await _bookingService.CheckBookingConflictsAsync(vehicleId, startAt, endAt, excludeBookingId);
            return Ok(conflicts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking booking conflicts for vehicle {VehicleId}", vehicleId);
            return StatusCode(500, new { message = "An error occurred while checking conflicts" });
        }
    }

    /// <summary>
    /// Get booking priority queue for a vehicle and time period
    /// </summary>
    [HttpGet("priority-queue")]
    public async Task<IActionResult> GetPriorityQueue(
        [FromQuery] Guid vehicleId,
        [FromQuery] DateTime startAt,
        [FromQuery] DateTime endAt)
    {
        try
        {
            var priorityQueue = await _bookingService.GetBookingPriorityQueueAsync(vehicleId, startAt, endAt);
            return Ok(priorityQueue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting priority queue for vehicle {VehicleId}", vehicleId);
            return StatusCode(500, new { message = "An error occurred while retrieving priority queue" });
        }
    }

    /// <summary>
    /// Get pending approval bookings (Group Admins only)
    /// </summary>
    [HttpGet("pending-approvals")]
    [Authorize(Roles = "SystemAdmin,GroupAdmin")]
    public async Task<IActionResult> GetPendingApprovals()
    {
        try
        {
            var userId = GetCurrentUserId();
            var pendingBookings = await _bookingService.GetPendingApprovalsAsync(userId);

            return Ok(pendingBookings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending approvals");
            return StatusCode(500, new { message = "An error occurred while retrieving pending approvals" });
        }
    }

    /// <summary>
    /// Approve a booking (Group Admins only)
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "SystemAdmin,GroupAdmin")]
    public async Task<IActionResult> ApproveBooking(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var booking = await _bookingService.ApproveBookingAsync(id, userId);

            _logger.LogInformation("Booking {BookingId} approved by {UserId}", id, userId);

            return Ok(booking);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbidden(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving booking {BookingId}", id);
            return StatusCode(500, new { message = "An error occurred while approving booking" });
        }
    }

    /// <summary>
    /// Cancel a booking
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> CancelBooking(Guid id, [FromBody] CancelBookingDto cancelDto)
    {
        try
        {
            var userId = GetCurrentUserId();
            var booking = await _bookingService.CancelBookingAsync(id, userId, cancelDto.Reason);

            _logger.LogInformation("Booking {BookingId} cancelled by {UserId}", id, userId);

            return Ok(booking);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbidden(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling booking {BookingId}", id);
            return StatusCode(500, new { message = "An error occurred while cancelling booking" });
        }
    }

    /// <summary>
    /// Update the recorded vehicle status for a booking.
    /// </summary>
    [HttpPatch("{id:guid}/vehicle-status")]
    public async Task<IActionResult> UpdateVehicleStatus(Guid id, [FromBody] CoOwnershipVehicle.Booking.Api.DTOs.UpdateVehicleStatusDto dto)
    {
        try
        {
            var booking = await _bookingService.UpdateVehicleStatusAsync(id, dto);
            return Ok(booking);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating vehicle status for booking {BookingId}", id);
            return StatusCode(500, new { message = "An error occurred while updating vehicle status" });
        }
    }

    /// <summary>
    /// Record trip summary (distance, fee) after completion.
    /// </summary>
    [HttpPatch("{id:guid}/trip-summary")]
    public async Task<IActionResult> UpdateTripSummary(Guid id, [FromBody] UpdateTripSummaryDto dto)
    {
        try
        {
            var booking = await _bookingService.UpdateTripSummaryAsync(id, dto);
            return Ok(booking);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating trip summary for booking {BookingId}", id);
            return StatusCode(500, new { message = "An error occurred while updating trip summary" });
        }
    }

    /// <summary>
    /// Get booking calendar for a vehicle (7-day view)
    /// </summary>
    [HttpGet("calendar")]
    public async Task<IActionResult> GetBookingCalendar(
        [FromQuery] Guid vehicleId,
        [FromQuery] DateTime? startDate = null)
    {
        try
        {
            var start = startDate ?? DateTime.UtcNow.Date;
            var end = start.AddDays(7);

            var bookings = await _bookingService.GetVehicleBookingsAsync(vehicleId, start, end);

            var calendar = new
            {
                VehicleId = vehicleId,
                StartDate = start,
                EndDate = end,
                Bookings = bookings,
                TotalBookings = bookings.Count,
                ConfirmedBookings = bookings.Count(b => b.Status == BookingStatus.Confirmed),
                PendingBookings = bookings.Count(b => b.Status == BookingStatus.PendingApproval)
            };

            return Ok(calendar);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting booking calendar for vehicle {VehicleId}", vehicleId);
            return StatusCode(500, new { message = "An error occurred while retrieving calendar" });
        }
    }

    /// <summary>
    /// Get smart booking suggestions based on availability and user priority
    /// </summary>
    [HttpGet("suggestions")]
    public async Task<IActionResult> GetBookingSuggestions(
        [FromQuery] Guid vehicleId,
        [FromQuery] DateTime preferredDate,
        [FromQuery] int durationHours = 4)
    {
        try
        {
            var userId = GetCurrentUserId();
            var suggestions = new List<object>();

            // Check availability around preferred time
            var startTime = preferredDate.Date.AddHours(8); // Start checking from 8 AM
            var endTime = preferredDate.Date.AddHours(22);   // Until 10 PM

            for (var time = startTime; time <= endTime.AddHours(-durationHours); time = time.AddHours(1))
            {
                var proposedEnd = time.AddHours(durationHours);
                var conflicts = await _bookingService.CheckBookingConflictsAsync(vehicleId, time, proposedEnd);

                if (!conflicts.HasConflicts)
                {
                    suggestions.Add(new
                    {
                        StartAt = time,
                        EndAt = proposedEnd,
                        IsOptimal = Math.Abs((time - preferredDate).TotalHours) < 2, // Within 2 hours of preference
                        Confidence = "High"
                    });

                    if (suggestions.Count >= 5) break; // Limit to 5 suggestions
                }
            }

            return Ok(new
            {
                VehicleId = vehicleId,
                PreferredDate = preferredDate,
                DurationHours = durationHours,
                Suggestions = suggestions
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting booking suggestions for vehicle {VehicleId}", vehicleId);
            return StatusCode(500, new { message = "An error occurred while generating suggestions" });
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

public class CancelBookingDto
{
    public string? Reason { get; set; }
}
