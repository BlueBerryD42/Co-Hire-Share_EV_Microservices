using CoOwnershipVehicle.Booking.Api.Contracts;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoOwnershipVehicle.Booking.Api.Controllers;

[ApiController]
[Route("api/vehicle")]
[Authorize]
public class VehicleQrController : ControllerBase
{
    private readonly IQrCodeService _qrCodeService;
    private readonly ILogger<VehicleQrController> _logger;

    public VehicleQrController(IQrCodeService qrCodeService, ILogger<VehicleQrController> logger)
    {
        _qrCodeService = qrCodeService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieve a vehicle QR code for rapid check-in/check-out workflows.
    /// </summary>
    /// <param name="vehicleId">Vehicle identifier.</param>
    /// <param name="format">Optional response format: image (default), dataUrl, or payload.</param>
    [HttpGet("{vehicleId:guid}/qr")]
    [Produces("image/png", "application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVehicleQr(Guid vehicleId, [FromQuery] string? format, CancellationToken cancellationToken)
    {
        Guid? userId = null;
        try
        {
            userId = GetCurrentUserId();
            var result = await _qrCodeService.GetVehicleQrCodeAsync(vehicleId, userId.Value, cancellationToken);
            var responseFormat = string.IsNullOrWhiteSpace(format) ? "image" : format.Trim().ToLowerInvariant();

            return responseFormat switch
            {
                "image" or "png" => File(result.ImageBytes, "image/png"),
                "dataurl" or "data-url" or "json" => Ok(new VehicleQrCodeResponseDto
                {
                    VehicleId = vehicleId,
                    Format = "dataUrl",
                    Payload = result.DataUrl,
                    ExpiresAt = result.ExpiresAt
                }),
                "payload" => Ok(new VehicleQrCodeResponseDto
                {
                    VehicleId = vehicleId,
                    Format = "payload",
                    Payload = result.Payload,
                    ExpiresAt = result.ExpiresAt
                }),
                _ => BadRequest(new { message = "Unsupported format. Use 'image', 'dataUrl', or 'payload'." })
            };
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating vehicle QR code for {VehicleId} and user {UserId}", vehicleId, userId?.ToString() ?? "unknown");
            return StatusCode(500, new { message = "An error occurred while generating the vehicle QR code" });
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
