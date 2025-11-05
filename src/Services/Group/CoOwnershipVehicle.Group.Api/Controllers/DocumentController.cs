using CoOwnershipVehicle.Group.Api.DTOs;
using CoOwnershipVehicle.Group.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoOwnershipVehicle.Group.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentController : BaseAuthenticatedController
{
    private readonly IDocumentService _documentService;

    public DocumentController(IDocumentService documentService, ILogger<DocumentController> logger)
        : base(logger)
    {
        _documentService = documentService;
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

            Logger.LogInformation("User {UserId} uploading document to group {GroupId}", userId, request.GroupId);

            var result = await _documentService.UploadDocumentAsync(request, userId);

            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Unauthorized document upload attempt");
            return Unauthorized(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            Logger.LogWarning(ex, "Invalid file upload");
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            Logger.LogWarning(ex, "Document upload operation failed");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "An unexpected error occurred during document upload for group {GroupId}", request.GroupId);
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
            Logger.LogWarning(ex, "Unauthorized access to group documents");
            return Unauthorized(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error retrieving group documents");
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
            Logger.LogWarning(ex, "Unauthorized access to group documents");
            return Unauthorized(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error retrieving group documents");
            return StatusCode(500, new { error = "An error occurred while retrieving documents" });
        }
    }

    /// <summary>
    /// Get deleted documents for a group (Admin only)
    /// </summary>
    /// <param name="groupId">Group ID</param>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="pageSize">Page size (default: 20)</param>
    [HttpGet("group/{groupId}/deleted")]
    [ProducesResponseType(typeof(PaginatedDocumentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PaginatedDocumentResponse>> GetDeletedDocuments(
        Guid groupId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var userId = GetUserId();
            var parameters = new DocumentQueryParameters
            {
                Page = page,
                PageSize = pageSize,
                OnlyDeleted = true // Show only deleted documents
            };

            var documents = await _documentService.GetGroupDocumentsPaginatedAsync(groupId, userId, parameters);
            return Ok(documents);
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Unauthorized access to deleted documents");
            return Unauthorized(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error retrieving deleted documents");
            return StatusCode(500, new { error = "An error occurred while retrieving deleted documents" });
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
            Logger.LogWarning(ex, "Document not found");
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Unauthorized access to document");
            return Unauthorized(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error retrieving document");
            return StatusCode(500, new { error = "An error occurred while retrieving the document" });
        }
    }

    /// <summary>
    /// Soft delete a document (marks as deleted but keeps files)
    /// Note: Cannot delete fully signed documents (legal protection)
    /// </summary>
    [HttpDelete("{documentId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
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
            Logger.LogWarning(ex, "Document not found for deletion");
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            Logger.LogWarning(ex, "Invalid delete operation");

            // Return 409 Conflict for fully signed documents (legal protection)
            if (ex.Message.Contains("fully signed") || ex.Message.Contains("legal binding"))
            {
                return Conflict(new { error = ex.Message });
            }

            return BadRequest(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Unauthorized document deletion attempt");
            return Unauthorized(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error deleting document");
            return StatusCode(500, new { error = "An error occurred while deleting the document" });
        }
    }

    /// <summary>
    /// Restore a soft-deleted document
    /// </summary>
    [HttpPost("{documentId}/restore")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RestoreDocument(Guid documentId)
    {
        try
        {
            var userId = GetUserId();
            await _documentService.RestoreDocumentAsync(documentId, userId);
            return Ok(new { message = "Document restored successfully" });
        }
        catch (KeyNotFoundException ex)
        {
            Logger.LogWarning(ex, "Document not found for restoration");
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            Logger.LogWarning(ex, "Invalid restore operation");
            return BadRequest(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Unauthorized document restoration attempt");
            return Unauthorized(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error restoring document");
            return StatusCode(500, new { error = "An error occurred while restoring the document" });
        }
    }

    /// <summary>
    /// Permanently delete a document (must be soft-deleted first, removes files from storage)
    /// </summary>
    [HttpDelete("{documentId}/permanent")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PermanentlyDeleteDocument(Guid documentId)
    {
        try
        {
            var userId = GetUserId();
            await _documentService.PermanentlyDeleteDocumentAsync(documentId, userId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            Logger.LogWarning(ex, "Document not found for permanent deletion");
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            Logger.LogWarning(ex, "Invalid permanent delete operation");
            return BadRequest(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Unauthorized permanent deletion attempt");
            return Unauthorized(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error permanently deleting document");
            return StatusCode(500, new { error = "An error occurred while permanently deleting the document" });
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
            Logger.LogWarning(ex, "Document not found");
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Unauthorized access to document download");
            return Unauthorized(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error generating download URL");
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
            Logger.LogWarning(ex, "Document not found for download");
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Unauthorized document download attempt");
            return Unauthorized(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error downloading document");
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
            Logger.LogWarning(ex, "Document not found for preview");
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Unauthorized document preview attempt");
            return Unauthorized(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            Logger.LogWarning(ex, "Preview not supported for document type");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error previewing document");
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

            Logger.LogWarning(ex, "Document not found for tracking");
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Unauthorized access to download tracking");
            return Unauthorized(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error retrieving download tracking");
            return StatusCode(500, new { error = "An error occurred while retrieving download tracking" });
        }
    }
}
