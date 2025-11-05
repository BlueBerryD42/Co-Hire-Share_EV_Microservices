using CoOwnershipVehicle.Group.Api.DTOs;
using CoOwnershipVehicle.Group.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoOwnershipVehicle.Group.Api.Controllers;

[ApiController]
[Route("api/document")]
public class DocumentVersionController : BaseAuthenticatedController
{
    private readonly IDocumentService _documentService;

    public DocumentVersionController(IDocumentService documentService, ILogger<DocumentVersionController> logger)
        : base(logger)
    {
        _documentService = documentService;
    }

    /// <summary>
    /// Upload a new version of an existing document
    /// </summary>
    [HttpPost("{documentId}/version")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(DocumentVersionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentVersionResponse>> UploadNewVersion(
        [FromRoute] Guid documentId,
        [FromForm] UploadDocumentVersionRequest request)
    {
        try
        {
            if (request.File == null || request.File.Length == 0)
            {
                return BadRequest(new { error = "File is required" });
            }

            var userId = GetUserId();
            var result = await _documentService.UploadNewVersionAsync(documentId, request.File, request.ChangeDescription, userId);

            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            Logger.LogWarning(ex, "Document not found");
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Unauthorized version upload");
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            Logger.LogWarning(ex, "Invalid file");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error uploading new version for document {DocumentId}. Exception: {Message}, StackTrace: {StackTrace}",
                documentId, ex.Message, ex.StackTrace);
            return StatusCode(500, new
            {
                error = "An error occurred while uploading the new version",
                details = ex.Message,
                innerException = ex.InnerException?.Message
            });
        }
    }

    /// <summary>
    /// Get all versions of a document
    /// </summary>
    [HttpGet("{documentId}/versions")]
    [ProducesResponseType(typeof(DocumentVersionListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentVersionListResponse>> GetDocumentVersions(Guid documentId)
    {
        try
        {
            var userId = GetUserId();
            var result = await _documentService.GetDocumentVersionsAsync(documentId, userId);

            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            Logger.LogWarning(ex, "Document not found");
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Unauthorized version access");
            return Forbid();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error retrieving document versions");
            return StatusCode(500, new { error = "An error occurred while retrieving document versions" });
        }
    }

    /// <summary>
    /// Download a specific version of a document
    /// </summary>
    [HttpGet("version/{versionId}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadVersion(Guid versionId)
    {
        try
        {
            var userId = GetUserId();
            var result = await _documentService.DownloadVersionAsync(versionId, userId);

            Response.Headers["Content-Disposition"] = $"attachment; filename=\"{result.FileName}\"";
            Response.Headers["Accept-Ranges"] = "bytes";

            return File(result.FileStream, result.ContentType, result.FileName);
        }
        catch (KeyNotFoundException ex)
        {
            Logger.LogWarning(ex, "Version not found");
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Unauthorized version download");
            return Forbid();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error downloading version");
            return StatusCode(500, new { error = "An error occurred while downloading the version" });
        }
    }

    /// <summary>
    /// Delete a specific version of a document
    /// </summary>
    [HttpDelete("version/{versionId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteVersion(Guid versionId)
    {
        try
        {
            var userId = GetUserId();
            await _documentService.DeleteVersionAsync(versionId, userId);

            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            Logger.LogWarning(ex, "Version not found for deletion");
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Unauthorized version deletion");
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            Logger.LogWarning(ex, "Invalid version deletion operation");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error deleting version");
            return StatusCode(500, new { error = "An error occurred while deleting the version" });
        }
    }
}
