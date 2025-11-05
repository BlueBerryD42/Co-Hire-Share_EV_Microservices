using CoOwnershipVehicle.Group.Api.DTOs;
using CoOwnershipVehicle.Group.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoOwnershipVehicle.Group.Api.Controllers;

/// <summary>
/// Controller for advanced document search and filtering
/// </summary>
[ApiController]
[Route("api/document/search")]
[Authorize]
public class DocumentSearchController : BaseAuthenticatedController
{
    private readonly IDocumentSearchService _searchService;

    public DocumentSearchController(
        IDocumentSearchService searchService,
        ILogger<DocumentSearchController> logger)
        : base(logger)
    {
        _searchService = searchService;
    }

    /// <summary>
    /// Perform advanced document search with filters
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(AdvancedDocumentSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AdvancedDocumentSearchResponse>> Search(
        [FromBody] AdvancedDocumentSearchRequest request)
    {
        try
        {
            var userId = GetUserId();
            var result = await _searchService.SearchDocumentsAsync(request, userId);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            Logger.LogWarning(ex, "Invalid search parameters");
            return BadRequest(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Unauthorized search attempt");
            return Forbid();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error performing document search");
            return StatusCode(500, new { error = "An error occurred while searching documents" });
        }
    }

    /// <summary>
    /// Get recent documents for the user
    /// </summary>
    [HttpGet("recent")]
    [ProducesResponseType(typeof(List<DocumentSearchResult>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<DocumentSearchResult>>> GetRecent(
        [FromQuery] RecentDocumentsRequest request)
    {
        try
        {
            var userId = GetUserId();
            var result = await _searchService.GetRecentDocumentsAsync(request, userId);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Unauthorized access to recent documents");
            return Forbid();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error retrieving recent documents");
            return StatusCode(500, new { error = "An error occurred while retrieving recent documents" });
        }
    }

    /// <summary>
    /// Get documents shared with the user (external shares)
    /// </summary>
    [HttpGet("shared-with-me")]
    [ProducesResponseType(typeof(List<DocumentSearchResult>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<DocumentSearchResult>>> GetSharedWithMe(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var userId = GetUserId();
            var result = await _searchService.GetSharedWithMeAsync(userId, page, pageSize);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            Logger.LogWarning(ex, "User not found");
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error retrieving shared documents");
            return StatusCode(500, new { error = "An error occurred while retrieving shared documents" });
        }
    }

    #region Tag Management

    /// <summary>
    /// Create a new tag
    /// </summary>
    [HttpPost("tags")]
    [ProducesResponseType(typeof(TagResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TagResponse>> CreateTag([FromBody] CreateTagRequest request)
    {
        try
        {
            var userId = GetUserId();
            var result = await _searchService.CreateTagAsync(request, userId);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Unauthorized tag creation attempt");
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            Logger.LogWarning(ex, "Invalid tag creation");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error creating tag");
            return StatusCode(500, new { error = "An error occurred while creating the tag" });
        }
    }

    /// <summary>
    /// Get all tags for a group or system-wide
    /// </summary>
    [HttpGet("tags")]
    [ProducesResponseType(typeof(List<TagResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TagResponse>>> GetTags([FromQuery] Guid? groupId = null)
    {
        try
        {
            var userId = GetUserId();
            var result = await _searchService.GetTagsAsync(groupId, userId);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Unauthorized tag access");
            return Forbid();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error retrieving tags");
            return StatusCode(500, new { error = "An error occurred while retrieving tags" });
        }
    }

    /// <summary>
    /// Add tags to a document
    /// </summary>
    [HttpPost("{documentId}/tags")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AddTags(
        Guid documentId, [FromBody] AddTagsToDocumentRequest request)
    {
        try
        {
            var userId = GetUserId();
            await _searchService.AddTagsToDocumentAsync(documentId, request, userId);
            return Ok(new { message = "Tags added successfully" });
        }
        catch (KeyNotFoundException ex)
        {
            Logger.LogWarning(ex, "Document not found");
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Unauthorized tag addition");
            return Forbid();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error adding tags");
            return StatusCode(500, new { error = "An error occurred while adding tags" });
        }
    }

    /// <summary>
    /// Remove tag from a document
    /// </summary>
    [HttpDelete("{documentId}/tags/{tagName}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RemoveTag(Guid documentId, string tagName)
    {
        try
        {
            var userId = GetUserId();
            await _searchService.RemoveTagFromDocumentAsync(documentId, tagName, userId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            Logger.LogWarning(ex, "Document or tag not found");
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Unauthorized tag removal");
            return Forbid();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error removing tag");
            return StatusCode(500, new { error = "An error occurred while removing the tag" });
        }
    }

    /// <summary>
    /// Get tag suggestions for a document
    /// </summary>
    [HttpGet("{documentId}/tag-suggestions")]
    [ProducesResponseType(typeof(TagSuggestionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TagSuggestionResponse>> GetTagSuggestions(Guid documentId)
    {
        try
        {
            var userId = GetUserId();
            var result = await _searchService.GetTagSuggestionsAsync(documentId, userId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            Logger.LogWarning(ex, "Document not found");
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Unauthorized access");
            return Forbid();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting tag suggestions");
            return StatusCode(500, new { error = "An error occurred while getting tag suggestions" });
        }
    }

    #endregion

    #region Saved Searches

    /// <summary>
    /// Create a saved search
    /// </summary>
    [HttpPost("saved")]
    [ProducesResponseType(typeof(SavedSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SavedSearchResponse>> CreateSavedSearch(
        [FromBody] CreateSavedSearchRequest request)
    {
        try
        {
            var userId = GetUserId();
            var result = await _searchService.CreateSavedSearchAsync(request, userId);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Unauthorized saved search creation");
            return Forbid();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error creating saved search");
            return StatusCode(500, new { error = "An error occurred while creating the saved search" });
        }
    }

    /// <summary>
    /// Get user's saved searches
    /// </summary>
    [HttpGet("saved")]
    [ProducesResponseType(typeof(List<SavedSearchResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SavedSearchResponse>>> GetSavedSearches(
        [FromQuery] Guid? groupId = null)
    {
        try
        {
            var userId = GetUserId();
            var result = await _searchService.GetSavedSearchesAsync(userId, groupId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error retrieving saved searches");
            return StatusCode(500, new { error = "An error occurred while retrieving saved searches" });
        }
    }

    /// <summary>
    /// Execute a saved search
    /// </summary>
    [HttpPost("saved/{savedSearchId}/execute")]
    [ProducesResponseType(typeof(AdvancedDocumentSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdvancedDocumentSearchResponse>> ExecuteSavedSearch(Guid savedSearchId)
    {
        try
        {
            var userId = GetUserId();
            var result = await _searchService.ExecuteSavedSearchAsync(savedSearchId, userId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            Logger.LogWarning(ex, "Saved search not found");
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            Logger.LogWarning(ex, "Failed to execute saved search");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error executing saved search");
            return StatusCode(500, new { error = "An error occurred while executing the saved search" });
        }
    }

    /// <summary>
    /// Delete a saved search
    /// </summary>
    [HttpDelete("saved/{savedSearchId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSavedSearch(Guid savedSearchId)
    {
        try
        {
            var userId = GetUserId();
            await _searchService.DeleteSavedSearchAsync(savedSearchId, userId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            Logger.LogWarning(ex, "Saved search not found");
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error deleting saved search");
            return StatusCode(500, new { error = "An error occurred while deleting the saved search" });
        }
    }

    #endregion

    #region Bulk Operations

    /// <summary>
    /// Bulk download documents as ZIP
    /// </summary>
    [HttpPost("bulk/download")]
    [ProducesResponseType(typeof(BulkDownloadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BulkDownloadResponse>> BulkDownload(
        [FromBody] BulkDownloadRequest request)
    {
        try
        {
            var userId = GetUserId();
            var result = await _searchService.BulkDownloadAsync(request, userId);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            Logger.LogWarning(ex, "Invalid bulk download request");
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            Logger.LogWarning(ex, "Bulk download failed");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error performing bulk download");
            return StatusCode(500, new { error = "An error occurred while performing bulk download" });
        }
    }

    /// <summary>
    /// Bulk tag or untag documents
    /// </summary>
    [HttpPost("bulk/tag")]
    [ProducesResponseType(typeof(BulkTagResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<BulkTagResponse>> BulkTag([FromBody] BulkTagRequest request)
    {
        try
        {
            var userId = GetUserId();
            var result = await _searchService.BulkTagAsync(request, userId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error performing bulk tag operation");
            return StatusCode(500, new { error = "An error occurred while performing bulk tag operation" });
        }
    }

    /// <summary>
    /// Bulk delete documents
    /// </summary>
    [HttpPost("bulk/delete")]
    [ProducesResponseType(typeof(BulkDeleteResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<BulkDeleteResponse>> BulkDelete([FromBody] BulkDeleteRequest request)
    {
        try
        {
            var userId = GetUserId();
            var result = await _searchService.BulkDeleteAsync(request, userId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error performing bulk delete");
            return StatusCode(500, new { error = "An error occurred while performing bulk delete" });
        }
    }

    #endregion
}
