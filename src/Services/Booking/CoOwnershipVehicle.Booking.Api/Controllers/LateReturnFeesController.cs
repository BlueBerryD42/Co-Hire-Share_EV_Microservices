using System.Security.Claims;
using CoOwnershipVehicle.Booking.Api.Contracts;
using CoOwnershipVehicle.Booking.Api.DTOs;
using CoOwnershipVehicle.Booking.Api.Repositories;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CoOwnershipVehicle.Booking.Api.Controllers;

[ApiController]
[Route("api/late-return-fees")]
[Authorize]
public class LateReturnFeesController : ControllerBase
{
    private readonly ILateReturnFeeService _lateReturnFeeService;
    private readonly IBookingRepository _bookingRepository;
    private readonly ILogger<LateReturnFeesController> _logger;

    public LateReturnFeesController(
        ILateReturnFeeService lateReturnFeeService,
        IBookingRepository bookingRepository,
        ILogger<LateReturnFeesController> logger)
    {
        _lateReturnFeeService = lateReturnFeeService ?? throw new ArgumentNullException(nameof(lateReturnFeeService));
        _bookingRepository = bookingRepository ?? throw new ArgumentNullException(nameof(bookingRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieve a specific late return fee record.
    /// </summary>
    [HttpGet("{feeId:guid}")]
    [ProducesResponseType(typeof(LateReturnFeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid feeId, CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();
            var fee = await _lateReturnFeeService.GetByIdAsync(feeId, cancellationToken);

            if (fee == null)
            {
                return NotFound(new { message = "Late return fee not found." });
            }

            if (!await HasAccessToGroupAsync(userId, fee.GroupId, fee.UserId, cancellationToken))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Access denied to this late return fee." });
            }

            return Ok(fee);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving late return fee {FeeId}", feeId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving the late return fee." });
        }
    }

    /// <summary>
    /// Retrieve late return fees for a specific booking.
    /// </summary>
    [HttpGet("by-booking/{bookingId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<LateReturnFeeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByBooking(Guid bookingId, CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();
            var booking = await _bookingRepository.GetBookingWithDetailsAsync(bookingId, cancellationToken);
            if (booking == null)
            {
                return NotFound(new { message = "Booking not found." });
            }

            if (!await HasAccessToGroupAsync(userId, booking.GroupId, booking.UserId, cancellationToken))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Access denied to this booking." });
            }

            var fees = await _lateReturnFeeService.GetByBookingAsync(bookingId, cancellationToken);
            return Ok(fees);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving late return fees for booking {BookingId}", bookingId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving late return fees." });
        }
    }

    /// <summary>
    /// Retrieve the current user's late return fee history.
    /// </summary>
    [HttpGet("history")]
    [ProducesResponseType(typeof(IReadOnlyList<LateReturnFeeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserHistory([FromQuery] int? take, CancellationToken cancellationToken)
    {
        try
        {
            var userId = GetCurrentUserId();
            var history = await _lateReturnFeeService.GetUserHistoryAsync(userId, take, cancellationToken);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving late return fee history for current user");
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while retrieving the fee history." });
        }
    }

    /// <summary>
    /// Waive a late return fee (group admins only).
    /// </summary>
    [HttpPost("{feeId:guid}/waive")]
    [Authorize(Roles = "SystemAdmin,GroupAdmin")]
    [ProducesResponseType(typeof(LateReturnFeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Waive(Guid feeId, [FromBody] WaiveLateReturnFeeDto? request, CancellationToken cancellationToken)
    {
        try
        {
            var adminId = GetCurrentUserId();
            request ??= new WaiveLateReturnFeeDto();

            var fee = await _lateReturnFeeService.WaiveAsync(feeId, adminId, request.Reason, cancellationToken);
            return Ok(fee);
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
            _logger.LogError(ex, "Error waiving late return fee {FeeId}", feeId);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "An error occurred while updating the late return fee." });
        }
    }

    private async Task<bool> HasAccessToGroupAsync(Guid userId, Guid groupId, Guid bookingOwnerId, CancellationToken cancellationToken)
    {
        if (userId == bookingOwnerId)
        {
            return true;
        }

        if (User.IsInRole("SystemAdmin") || User.IsInRole("GroupAdmin"))
        {
            return true;
        }

        return await _bookingRepository.UserHasGroupAccessAsync(userId, groupId, cancellationToken);
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("User identifier not found in token.");
        }

        return userId;
    }
}
