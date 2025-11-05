using CoOwnershipVehicle.Group.Api.DTOs;

namespace CoOwnershipVehicle.Group.Api.Services.Interfaces;

/// <summary>
/// Service for advanced document search and filtering
/// </summary>
public interface IDocumentSearchService
{
    /// <summary>
    /// Perform advanced document search
    /// </summary>
    Task<AdvancedDocumentSearchResponse> SearchDocumentsAsync(AdvancedDocumentSearchRequest request, Guid userId);

    /// <summary>
    /// Get recent documents for a user
    /// </summary>
    Task<List<DocumentSearchResult>> GetRecentDocumentsAsync(RecentDocumentsRequest request, Guid userId);

    /// <summary>
    /// Get documents shared with the user
    /// </summary>
    Task<List<DocumentSearchResult>> GetSharedWithMeAsync(Guid userId, int page = 1, int pageSize = 20);

    // Tag management
    /// <summary>
    /// Create a new tag
    /// </summary>
    Task<TagResponse> CreateTagAsync(CreateTagRequest request, Guid userId);

    /// <summary>
    /// Get all tags for a group or system-wide
    /// </summary>
    Task<List<TagResponse>> GetTagsAsync(Guid? groupId, Guid userId);

    /// <summary>
    /// Add tags to a document
    /// </summary>
    Task AddTagsToDocumentAsync(Guid documentId, AddTagsToDocumentRequest request, Guid userId);

    /// <summary>
    /// Remove tag from a document
    /// </summary>
    Task RemoveTagFromDocumentAsync(Guid documentId, string tagName, Guid userId);

    /// <summary>
    /// Get tag suggestions based on document content
    /// </summary>
    Task<TagSuggestionResponse> GetTagSuggestionsAsync(Guid documentId, Guid userId);

    // Saved searches
    /// <summary>
    /// Create a saved search
    /// </summary>
    Task<SavedSearchResponse> CreateSavedSearchAsync(CreateSavedSearchRequest request, Guid userId);

    /// <summary>
    /// Get user's saved searches
    /// </summary>
    Task<List<SavedSearchResponse>> GetSavedSearchesAsync(Guid userId, Guid? groupId = null);

    /// <summary>
    /// Execute a saved search
    /// </summary>
    Task<AdvancedDocumentSearchResponse> ExecuteSavedSearchAsync(Guid savedSearchId, Guid userId);

    /// <summary>
    /// Delete saved search
    /// </summary>
    Task DeleteSavedSearchAsync(Guid savedSearchId, Guid userId);

    // Bulk operations
    /// <summary>
    /// Bulk download documents as ZIP
    /// </summary>
    Task<BulkDownloadResponse> BulkDownloadAsync(BulkDownloadRequest request, Guid userId);

    /// <summary>
    /// Bulk tag/untag documents
    /// </summary>
    Task<BulkTagResponse> BulkTagAsync(BulkTagRequest request, Guid userId);

    /// <summary>
    /// Bulk delete documents
    /// </summary>
    Task<BulkDeleteResponse> BulkDeleteAsync(BulkDeleteRequest request, Guid userId);
}
