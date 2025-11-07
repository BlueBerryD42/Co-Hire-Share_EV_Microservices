using CoOwnershipVehicle.Group.Api.DTOs;
using CoOwnershipVehicle.Group.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoOwnershipVehicle.Group.Api.Controllers;

[ApiController]
[Route("api/document")]
public class DocumentSigningController : BaseAuthenticatedController
{
    private readonly IDocumentService _documentService;

    public DocumentSigningController(IDocumentService documentService, ILogger<DocumentSigningController> logger)
        : base(logger)
    {
        _documentService = documentService;
    }

    /// <summary>
    /// Debug: Get document signing readiness info
    /// </summary>
    [HttpGet("{documentId}/signing-debug")]
    public async Task<IActionResult> GetSigningDebugInfo(Guid documentId)
    {
        try
        {
            var userId = GetUserId();
            var document = await _documentService.GetDocumentByIdAsync(documentId, userId);

            return Ok(new
            {
                documentId = document.Id,
                fileName = document.FileName,
                status = document.SignatureStatus.ToString(),
                groupId = document.GroupId,
                currentUserId = userId,
                message = "Check if status is 'Draft' and user is a group member"
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message, details = ex.InnerException?.Message });
        }
    }

    /// <summary>
    /// Send document for electronic signature
    /// </summary>
    [HttpPost("{documentId}/send-for-signing")]
    [ProducesResponseType(typeof(SendForSigningResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SendForSigningResponse>> SendForSigning(
        Guid documentId,
        [FromBody] SendForSigningRequest request)
    {
        try
        {
            var userId = GetUserId();

            // Get base URL from request
            var baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";

            var result = await _documentService.SendForSigningAsync(documentId, request, userId, baseUrl);

            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            Logger.LogWarning(ex, "Document not found for signing");
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Unauthorized send for signing attempt");
            return Unauthorized(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            Logger.LogWarning(ex, "Invalid request for send for signing");
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            Logger.LogWarning(ex, "Invalid operation for send for signing");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error sending document for signing");
            return StatusCode(500, new { error = "An error occurred while sending document for signing", details = ex.Message, innerException = ex.InnerException?.Message });
        }
    }

    /// <summary>
    /// Sign a document with digital signature
    /// </summary>
    [HttpPost("{documentId}/sign")]
    [ProducesResponseType(typeof(SignDocumentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SignDocumentResponse>> SignDocument(
        Guid documentId,
        [FromBody] SignDocumentRequest request)
    {
        try
        {
            var userId = GetUserId();
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

            var result = await _documentService.SignDocumentAsync(documentId, request, userId, ipAddress, userAgent);

            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            Logger.LogWarning(ex, "Document or signature not found");
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Unauthorized signing attempt");
            return Unauthorized(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            Logger.LogWarning(ex, "Invalid signature data");
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            Logger.LogWarning(ex, "Invalid signing operation");
            return Conflict(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error signing document");
            return StatusCode(500, new { error = "An error occurred while signing the document" });
        }
    }

    /// <summary>
    /// Get signature status and progress for a document
    /// </summary>
    [HttpGet("{documentId}/signatures")]
    [ProducesResponseType(typeof(DocumentSignatureStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentSignatureStatusResponse>> GetSignatureStatus(Guid documentId)
    {
        try
        {
            var userId = GetUserId();
            var result = await _documentService.GetSignatureStatusAsync(documentId, userId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            Logger.LogWarning(ex, "Document not found for signature status");
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Unauthorized access to signature status");
            return Unauthorized(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            Logger.LogWarning(ex, "Invalid operation getting signature status");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error retrieving signature status");
            return StatusCode(500, new { error = "An error occurred while retrieving signature status" });
        }
    }
}
