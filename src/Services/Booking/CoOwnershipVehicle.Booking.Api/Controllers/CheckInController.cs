using System.IdentityModel.Tokens.Jwt;
using CoOwnershipVehicle.Booking.Api.Contracts;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoOwnershipVehicle.Booking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CheckInController : ControllerBase
{
    private readonly ICheckInService _checkInService;
    private readonly ILogger<CheckInController> _logger;

    public CheckInController(ICheckInService checkInService, ILogger<CheckInController> logger)
    {
        _checkInService = checkInService ?? throw new ArgumentNullException(nameof(checkInService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost("start")]
    public async Task<IActionResult> StartTrip([FromBody] StartTripDto request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();
            var dto = await _checkInService.StartTripAsync(request, userId, cancellationToken);
            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start trip for booking {BookingId}", request.BookingId);
            return Problem(ex.Message);
        }
    }

    [HttpPost("end")]
    public async Task<IActionResult> EndTrip([FromBody] EndTripDto request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();
            var dto = await _checkInService.EndTripAsync(request, userId, cancellationToken);
            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to end trip for booking {BookingId}", request.BookingId);
            return Problem(ex.Message);
        }
    }

    [HttpGet("history/{bookingId:guid}")]
    public async Task<IActionResult> GetHistory(Guid bookingId, CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();
            var history = await _checkInService.GetBookingHistoryAsync(bookingId, userId, cancellationToken);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load check-in history for booking {BookingId}", bookingId);
            return Problem(ex.Message);
        }
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? User.FindFirst("sub")?.Value;
        if (Guid.TryParse(sub, out var userId))
        {
            return userId;
        }

        throw new UnauthorizedAccessException("User id missing in token.");
    }
}
