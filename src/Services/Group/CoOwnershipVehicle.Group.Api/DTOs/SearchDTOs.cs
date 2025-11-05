using CoOwnershipVehicle.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace CoOwnershipVehicle.Group.Api.DTOs;

/// <summary>
/// Advanced document search request
/// </summary>
public class AdvancedDocumentSearchRequest
{
    // Basic filters
    public Guid GroupId { get; set; }
    public string? SearchTerm { get; set; }

    // Type filters (multiple)
    public List<DocumentType>? DocumentTypes { get; set; }

    // Signature status filters (multiple)
    public List<SignatureStatus>? SignatureStatuses { get; set; }

    // Date range filters
    public DateTime? UploadedFrom { get; set; }
    public DateTime? UploadedTo { get; set; }

    // Uploader filter
    public List<Guid>? UploaderIds { get; set; }

    // Tag filters
    public List<string>? Tags { get; set; }
    public bool MatchAllTags { get; set; } = false; // true = AND, false = OR

    // Template filter
    public Guid? TemplateId { get; set; }

    // Full-text search in content (if indexed)
    public bool SearchInContent { get; set; } = false;

    // Sorting
    public DocumentSortBy SortBy { get; set; } = DocumentSortBy.UploadedDate;
    public bool SortDescending { get; set; } = true;

    // Pagination
    [Range(1, int.MaxValue, ErrorMessage = "Page must be at least 1")]
    public int Page { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "PageSize must be between 1 and 100")]
    public int PageSize { get; set; } = 20;

    // Include deleted (admin only)
    public bool IncludeDeleted { get; set; } = false;
}

/// <summary>
/// Sort options for documents
/// </summary>
public enum DocumentSortBy
{
    UploadedDate = 0,
    FileName = 1,
    FileSize = 2,
    DocumentType = 3,
    Relevance = 4 // For full-text search
}

/// <summary>
/// Advanced search response
/// </summary>
public class AdvancedDocumentSearchResponse
{
    public List<DocumentSearchResult> Results { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public SearchFacets Facets { get; set; } = new();
}

/// <summary>
/// Document search result with highlights
/// </summary>
public class DocumentSearchResult
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DocumentType Type { get; set; }
    public long FileSize { get; set; }
    public SignatureStatus SignatureStatus { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public string UploaderName { get; set; } = string.Empty;
    public Guid? UploaderId { get; set; }

    // Tags
    public List<string> Tags { get; set; } = new();

    // Template info
    public string? TemplateName { get; set; }

    // Search highlights (if full-text search)
    public List<string>? Highlights { get; set; }

    // Relevance score (if using full-text search)
    public double? RelevanceScore { get; set; }

    // Deleted info
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}

/// <summary>
/// Search facets for filtering UI
/// </summary>
public class SearchFacets
{
    public Dictionary<DocumentType, int> DocumentTypes { get; set; } = new();
    public Dictionary<SignatureStatus, int> SignatureStatuses { get; set; } = new();
    public Dictionary<string, int> Tags { get; set; } = new();
    public Dictionary<string, int> Uploaders { get; set; } = new();
    public Dictionary<string, int> Templates { get; set; } = new();
}

/// <summary>
/// Tag management
/// </summary>
public class CreateTagRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    public string? Description { get; set; }
    public Guid? GroupId { get; set; }
}

public class TagResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    public string? Description { get; set; }
    public int UsageCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AddTagsToDocumentRequest
{
    public List<string> TagNames { get; set; } = new();
}

public class TagSuggestionResponse
{
    public List<string> SuggestedTags { get; set; } = new();
}

/// <summary>
/// Saved search management
/// </summary>
public class CreateSavedSearchRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? GroupId { get; set; }
    public AdvancedDocumentSearchRequest SearchCriteria { get; set; } = new();
    public bool IsDefault { get; set; } = false;
}

public class SavedSearchResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? GroupId { get; set; }
    public int UsageCount { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Bulk operations
/// </summary>
public class BulkDownloadRequest
{
    public List<Guid> DocumentIds { get; set; } = new();
    public string? ZipFileName { get; set; }
}

public class BulkDownloadResponse
{
    public string DownloadUrl { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long TotalSize { get; set; }
    public int DocumentCount { get; set; }
    public DateTime ExpiresAt { get; set; }
}


public class BulkTagRequest
{
    public List<Guid> DocumentIds { get; set; } = new();
    public List<string> TagNames { get; set; } = new();
    public bool RemoveTags { get; set; } = false; // true = remove, false = add
}

public class BulkTagResponse
{
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public List<BulkOperationError> Errors { get; set; } = new();
}

public class BulkOperationError
{
    public Guid DocumentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}

/// <summary>
/// Recent documents view
/// </summary>
public class RecentDocumentsRequest
{
    public Guid? GroupId { get; set; }
    public int Count { get; set; } = 10;
}
