using CoOwnershipVehicle.Group.Api.DTOs;
using CoOwnershipVehicle.Group.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoOwnershipVehicle.Group.Api.Controllers;

/// <summary>
/// Controller for sharing documents with external parties
/// </summary>
[ApiController]
[Route("api/document")]
public class DocumentShareController : BaseAuthenticatedController
{
    private readonly IDocumentShareService _shareService;

    public DocumentShareController(
        IDocumentShareService shareService,
        ILogger<DocumentShareController> logger)
        : base(logger)
    {
        _shareService = shareService;
    }

    /// <summary>
    /// Create a share link for a document
    /// </summary>
    [HttpPost("{documentId}/share")]
    [Authorize]
    [ProducesResponseType(typeof(CreateShareResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CreateShareResponse>> CreateShare(
        Guid documentId, [FromBody] CreateShareRequest request)
    {
        try
        {
            var userId = GetUserId();
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var result = await _shareService.CreateShareAsync(documentId, request, userId, baseUrl);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            Logger.LogWarning(ex, "Document not found for sharing");
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Unauthorized share creation attempt");
            return Forbid();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error creating share");
            return StatusCode(500, new { error = "An error occurred while creating the share" });
        }
    }

    /// <summary>
    /// Access a shared document via token (PUBLIC - No authentication required)
    /// </summary>
    [HttpGet("shared/{token}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SharedDocumentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SharedDocumentResponse>> GetSharedDocument(
        string token, [FromQuery] string? password = null)
    {
        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

            var request = password != null ? new AccessSharedDocumentRequest { Password = password } : null;
            var result = await _shareService.GetSharedDocumentAsync(token, request, ipAddress, userAgent);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            Logger.LogWarning(ex, "Share link not found");
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Unauthorized access to shared document");
            return Unauthorized(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error accessing shared document");
            return StatusCode(500, new { error = "An error occurred while accessing the shared document" });
        }
    }

    /// <summary>
    /// Download a shared document (PUBLIC - No authentication required)
    /// </summary>
    [HttpGet("shared/{token}/download")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DownloadSharedDocument(
        string token, [FromQuery] string? password = null)
    {
        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

            var result = await _shareService.DownloadSharedDocumentAsync(token, password, ipAddress, userAgent);

            Response.Headers["Content-Disposition"] = $"attachment; filename=\"{result.FileName}\"";
            Response.Headers["Accept-Ranges"] = "bytes";

            return File(result.FileStream, result.ContentType, result.FileName);
        }
        catch (KeyNotFoundException ex)
        {
            Logger.LogWarning(ex, "Share link not found");
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Unauthorized download attempt");
            return Unauthorized(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error downloading shared document");
            return StatusCode(500, new { error = "An error occurred while downloading the document" });
        }
    }

    /// <summary>
    /// Preview a shared document (PUBLIC - No authentication required)
    /// </summary>
    [HttpGet("shared/{token}/preview")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PreviewSharedDocument(
        string token, [FromQuery] string? password = null)
    {
        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

            var result = await _shareService.PreviewSharedDocumentAsync(token, password, ipAddress, userAgent);

            Response.Headers["Content-Disposition"] = $"inline; filename=\"{result.FileName}\"";

            return File(result.FileStream, result.ContentType);
        }
        catch (KeyNotFoundException ex)
        {
            Logger.LogWarning(ex, "Share link not found");
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Unauthorized preview attempt");
            return Unauthorized(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            Logger.LogWarning(ex, "Preview not supported");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error previewing shared document");
            return StatusCode(500, new { error = "An error occurred while previewing the document" });
        }
    }

    /// <summary>
    /// Get all shares for a document
    /// </summary>
    [HttpGet("{documentId}/shares")]
    [Authorize]
    [ProducesResponseType(typeof(DocumentShareListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DocumentShareListResponse>> GetShares(Guid documentId)
    {
        try
        {
            var userId = GetUserId();
            var result = await _shareService.GetDocumentSharesAsync(documentId, userId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            Logger.LogWarning(ex, "Document not found");
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Unauthorized access to shares");
            return Forbid();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error retrieving shares");
            return StatusCode(500, new { error = "An error occurred while retrieving shares" });
        }
    }

    /// <summary>
    /// Get share analytics
    /// </summary>
    [HttpGet("{documentId}/share/{shareId}/analytics")]
    [Authorize]
    [ProducesResponseType(typeof(ShareAnalyticsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ShareAnalyticsResponse>> GetShareAnalytics(
        Guid documentId, Guid shareId)
    {
        try
        {
            var userId = GetUserId();
            var result = await _shareService.GetShareAnalyticsAsync(shareId, userId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            Logger.LogWarning(ex, "Share not found");
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Unauthorized access to share analytics");
            return Forbid();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error retrieving share analytics");
            return StatusCode(500, new { error = "An error occurred while retrieving share analytics" });
        }
    }

    /// <summary>
    /// Revoke a share
    /// </summary>
    [HttpDelete("{documentId}/share/{shareId}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RevokeShare(Guid documentId, Guid shareId)
    {
        try
        {
            var userId = GetUserId();
            await _shareService.RevokeShareAsync(documentId, shareId, userId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            Logger.LogWarning(ex, "Share not found");
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Unauthorized share revocation");
            return Forbid();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error revoking share");
            return StatusCode(500, new { error = "An error occurred while revoking the share" });
        }
    }

    /// <summary>
    /// Update share expiration
    /// </summary>
    [HttpPatch("{documentId}/share/{shareId}/expiration")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateShareExpiration(
        Guid documentId, Guid shareId, [FromBody] DateTime? expiresAt)
    {
        try
        {
            var userId = GetUserId();
            await _shareService.UpdateShareExpirationAsync(shareId, expiresAt, userId);
            return Ok(new { message = "Share expiration updated successfully" });
        }
        catch (KeyNotFoundException ex)
        {
            Logger.LogWarning(ex, "Share not found");
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Unauthorized expiration update");
            return Forbid();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error updating share expiration");
            return StatusCode(500, new { error = "An error occurred while updating share expiration" });
        }
    }

    /// <summary>
    /// Validate share token
    /// </summary>
    [HttpPost("shared/{token}/validate")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ValidateShareToken(
        string token, [FromBody] string? password = null)
    {
        try
        {
            var (isValid, errorMessage) = await _shareService.ValidateShareTokenAsync(token, password);

            if (isValid)
            {
                return Ok(new { valid = true, message = "Share token is valid" });
            }
            else
            {
                return BadRequest(new { valid = false, error = errorMessage });
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error validating share token");
            return StatusCode(500, new { error = "An error occurred while validating the share token" });
        }
    }
}
