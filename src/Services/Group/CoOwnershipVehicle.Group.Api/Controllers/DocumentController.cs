using CoOwnershipVehicle.Group.Api.DTOs;
using CoOwnershipVehicle.Group.Api.Helpers;
using CoOwnershipVehicle.Group.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CoOwnershipVehicle.Group.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
//[Authorize]
public class DocumentController : ControllerBase
{
    private readonly IDocumentService _documentService;
    private readonly ILogger<DocumentController> _logger;

    public DocumentController(IDocumentService documentService, ILogger<DocumentController> logger)
    {
        _documentService = documentService;
        _logger = logger;
    }

    /// <summary>
    /// Upload a document to a group
    /// </summary>
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(DocumentUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    [RequestSizeLimit(52428800)] // 50MB
    [RequestFormLimits(MultipartBodyLengthLimit = 52428800)]
    public async Task<ActionResult<DocumentUploadResponse>> UploadDocument([FromForm] DocumentUploadRequest request)
    {
        try
        {
            var userId = GetUserId();

            _logger.LogInformation("User {UserId} uploading document to group {GroupId}", userId, request.GroupId);

            var result = await _documentService.UploadDocumentAsync(request, userId);

            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized document upload attempt");
            return Unauthorized(new { error = ex.Message }); // ← THAY ĐỔI
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid file upload");
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Document upload operation failed");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred during document upload for group {GroupId}", request.GroupId);
            return StatusCode(500, new { error = "An error occurred while uploading the document" });
        }
    }

    /// <summary>
    /// Get all documents for a group
    /// </summary>
    [HttpGet("group/{groupId}")]
    [ProducesResponseType(typeof(List<DocumentListResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<DocumentListResponse>>> GetGroupDocuments(Guid groupId)
    {
        try
        {
            var userId = GetUserId();
            var documents = await _documentService.GetGroupDocumentsAsync(groupId, userId);
            return Ok(documents);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to group documents");
            return Unauthorized(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving group documents");
            return StatusCode(500, new { error = "An error occurred while retrieving documents" });
        }
    }

    /// <summary>
    /// Get paginated and filtered documents for a group
    /// </summary>
    [HttpGet("group/{groupId}/paginated")]
    [ProducesResponseType(typeof(PaginatedDocumentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PaginatedDocumentResponse>> GetGroupDocumentsPaginated(
        Guid groupId,
        [FromQuery] DocumentQueryParameters parameters)
    {
        try
        {
            var userId = GetUserId();
            var documents = await _documentService.GetGroupDocumentsPaginatedAsync(groupId, userId, parameters);
            return Ok(documents);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to group documents");
            return Unauthorized(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving group documents");
            return StatusCode(500, new { error = "An error occurred while retrieving documents" });
        }
    }

    /// <summary>
    /// Get document details by ID
    /// </summary>
    [HttpGet("{documentId}")]
    [ProducesResponseType(typeof(DocumentDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentDetailResponse>> GetDocumentById(Guid documentId)
    {
        try
        {
            var userId = GetUserId();
            var document = await _documentService.GetDocumentByIdAsync(documentId, userId);
            return Ok(document);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Document not found");
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to document");
            return Unauthorized(new { error = ex.Message }); // ← THAY ĐỔI
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving document");
            return StatusCode(500, new { error = "An error occurred while retrieving the document" });
        }
    }

    /// <summary>
    /// Delete a document
    /// </summary>
    [HttpDelete("{documentId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDocument(Guid documentId)
    {
        try
        {
            var userId = GetUserId();
            await _documentService.DeleteDocumentAsync(documentId, userId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Document not found for deletion");
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized document deletion attempt");
            return Unauthorized(new { error = ex.Message }); // ← THAY ĐỔI
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document");
            return StatusCode(500, new { error = "An error occurred while deleting the document" });
        }
    }

    /// <summary>
    /// Get secure download URL for a document
    /// </summary>
    [HttpGet("{documentId}/download-url")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<object>> GetDownloadUrl(Guid documentId)
    {
        try
        {
            var userId = GetUserId();
            var url = await _documentService.GetDocumentDownloadUrlAsync(documentId, userId);
            return Ok(new { downloadUrl = url });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Document not found");
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to document download");
            return Unauthorized(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating download URL");
            return StatusCode(500, new { error = "An error occurred while generating download URL" });
        }
    }

    /// <summary>
    /// Download document with tracking
    /// </summary>
    [HttpGet("{documentId}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadDocument(Guid documentId)
    {
        try
        {
            var userId = GetUserId();
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

            var result = await _documentService.DownloadDocumentAsync(documentId, userId, ipAddress, userAgent);

            Response.Headers["Content-Disposition"] = $"attachment; filename=\"{result.FileName}\"";
            Response.Headers["Accept-Ranges"] = "bytes";

            return File(result.FileStream, result.ContentType, result.FileName);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Document not found for download");
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized document download attempt");
            return Unauthorized(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading document");
            return StatusCode(500, new { error = "An error occurred while downloading the document" });
        }
    }

    /// <summary>
    /// Preview document inline (PDF, images)
    /// </summary>
    [HttpGet("{documentId}/preview")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PreviewDocument(Guid documentId)
    {
        try
        {
            var userId = GetUserId();
            var result = await _documentService.PreviewDocumentAsync(documentId, userId);

            Response.Headers["Content-Disposition"] = $"inline; filename=\"{result.FileName}\"";

            return File(result.FileStream, result.ContentType);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Document not found for preview");
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized document preview attempt");
            return Unauthorized(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Preview not supported for document type");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error previewing document");
            return StatusCode(500, new { error = "An error occurred while previewing the document" });
        }
    }

    /// <summary>
    /// Get download tracking information for a document
    /// </summary>
    [HttpGet("{documentId}/downloads/tracking")]
    [ProducesResponseType(typeof(DownloadTrackingInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DownloadTrackingInfo>> GetDownloadTracking(Guid documentId)
    {
        try
        {
            var userId = GetUserId();
            var tracking = await _documentService.GetDownloadTrackingInfoAsync(documentId, userId);
            return Ok(tracking);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Document not found for tracking");
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to download tracking");
            return Unauthorized(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving download tracking");
            return StatusCode(500, new { error = "An error occurred while retrieving download tracking" });
        }
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
            _logger.LogWarning(ex, "Document not found for signing");
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized send for signing attempt");
            return Unauthorized(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid request for send for signing");
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation for send for signing");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending document for signing");
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
            _logger.LogWarning(ex, "Document or signature not found");
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized signing attempt");
            return Unauthorized(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid signature data");
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid signing operation");
            return Conflict(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error signing document");
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
            _logger.LogWarning(ex, "Document not found for signature status");
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to signature status");
            return Unauthorized(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation getting signature status");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving signature status");
            return StatusCode(500, new { error = "An error occurred while retrieving signature status" });
        }
    }

    /// <summary>
    /// Verify certificate authenticity by certificate ID (returns HTML page)
    /// </summary>
    [HttpGet("verify-certificate/{certificateId}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> VerifyCertificate(string certificateId, [FromQuery] string? hash = null)
    {
        try
        {
            var result = await _documentService.VerifyCertificateAsync(certificateId, hash);

            // Return HTML page for better user experience
            var html = HtmlTemplates.GenerateVerificationHtml(result);
            return Content(html, "text/html");
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Certificate not found: {CertificateId}", certificateId);
            var errorHtml = HtmlTemplates.GenerateErrorHtml("Certificate Not Found", ex.Message, certificateId);
            return Content(errorHtml, "text/html");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying certificate: {CertificateId}", certificateId);
            var errorHtml = HtmlTemplates.GenerateErrorHtml("Verification Error", "An error occurred while verifying the certificate", certificateId);
            return Content(errorHtml, "text/html");
        }
    }

    /// <summary>
    /// Verify certificate and return JSON (for API calls)
    /// </summary>
    [HttpGet("verify-certificate-json/{certificateId}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> VerifyCertificateJson(string certificateId, [FromQuery] string? hash = null)
    {
        try
        {
            var result = await _documentService.VerifyCertificateAsync(certificateId, hash);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Certificate not found: {CertificateId}", certificateId);
            return NotFound(new { error = ex.Message, certificateId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying certificate: {CertificateId}", certificateId);
            return StatusCode(500, new { error = "An error occurred while verifying the certificate", certificateId });
        }
    }

    /// <summary>
    /// Get signing certificate for a fully signed document
    /// </summary>
    [HttpGet("{documentId}/certificate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetSigningCertificate(Guid documentId)
    {
        try
        {
            var userId = GetUserId();

            // Get base URL for QR code
            var baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";

            var result = await _documentService.GetSigningCertificateAsync(documentId, userId, baseUrl);

            var fileName = $"Certificate_{result.FileName}_{result.CertificateId}.pdf";

            return File(result.CertificatePdf, "application/pdf", fileName);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Document not found for certificate");
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access to certificate");
            return Unauthorized(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Document not fully signed");
            return Conflict(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating certificate");
            return StatusCode(500, new { error = "An error occurred while generating the certificate" });
        }
    }

    private Guid GetUserId()
    {
        _logger.LogWarning("Using hardcoded test user ID for development");
        return Guid.Parse("196F184E-7D93-4103-BB5A-3C0F78036DD4"); // ← Paste UserId
    }

    //private Guid GetUserId()
    //{
    //    // TODO: Remove hardcoded userId after implementing authentication
    //    _logger.LogWarning("Using hardcoded test user ID for development");
    //    return Guid.Parse("00000000-0000-0000-0000-000000000001"); // ← THÊM HARDCODED USER ID

    //    /* UNCOMMENT sau khi có authentication
    //    var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
    //                     ?? User.FindFirst("sub")?.Value
    //                     ?? User.FindFirst("userId")?.Value;

    //    if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
    //    {
    //        throw new UnauthorizedAccessException("User ID not found in token");
    //    }

    //    return userId;
    //    */
    //}

}