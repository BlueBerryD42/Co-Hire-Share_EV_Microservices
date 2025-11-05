using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Group.Api.Data;
using CoOwnershipVehicle.Group.Api.DTOs;
using CoOwnershipVehicle.Group.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CoOwnershipVehicle.Group.Api.Services.Implementations;

public class DocumentSearchService : IDocumentSearchService
{
    private readonly GroupDbContext _context;
    private readonly IFileStorageService _fileStorage;
    private readonly ILogger<DocumentSearchService> _logger;

    public DocumentSearchService(
        GroupDbContext context,
        IFileStorageService fileStorage,
        ILogger<DocumentSearchService> logger)
    {
        _context = context;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    public async Task<AdvancedDocumentSearchResponse> SearchDocumentsAsync(
        AdvancedDocumentSearchRequest request, Guid userId)
    {
        _logger.LogInformation("Performing advanced document search for user {UserId}", userId);

        // Validate pagination parameters
        if (request.Page < 1)
        {
            throw new ArgumentException("Page must be at least 1", nameof(request.Page));
        }

        if (request.PageSize < 1 || request.PageSize > 100)
        {
            throw new ArgumentException("PageSize must be between 1 and 100", nameof(request.PageSize));
        }

        // Verify user has access to the group
        var hasAccess = await _context.GroupMembers
            .AnyAsync(gm => gm.GroupId == request.GroupId && gm.UserId == userId);

        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("User does not have access to this group");
        }

        // Check if user can view deleted documents
        var isAdmin = await _context.GroupMembers
            .Where(gm => gm.GroupId == request.GroupId && gm.UserId == userId)
            .Select(gm => gm.RoleInGroup)
            .FirstOrDefaultAsync() == GroupRole.Admin;

        if (request.IncludeDeleted && !isAdmin)
        {
            throw new UnauthorizedAccessException("Only admins can view deleted documents");
        }

        // Build base query
        var query = request.IncludeDeleted
            ? _context.Documents.IgnoreQueryFilters()
            : _context.Documents;

        query = query
            .Include(d => d.TagMappings)
                .ThenInclude(tm => tm.Tag)
            .Include(d => d.Template)
            .Where(d => d.GroupId == request.GroupId);

        // Apply filters
        if (request.IncludeDeleted)
        {
            // No filter for deleted
        }
        else
        {
            query = query.Where(d => !d.IsDeleted);
        }

        // Document type filter
        if (request.DocumentTypes != null && request.DocumentTypes.Any())
        {
            query = query.Where(d => request.DocumentTypes.Contains(d.Type));
        }

        // Signature status filter
        if (request.SignatureStatuses != null && request.SignatureStatuses.Any())
        {
            query = query.Where(d => request.SignatureStatuses.Contains(d.SignatureStatus));
        }

        // Date range filter
        if (request.UploadedFrom.HasValue)
        {
            query = query.Where(d => d.CreatedAt >= request.UploadedFrom.Value);
        }

        if (request.UploadedTo.HasValue)
        {
            query = query.Where(d => d.CreatedAt <= request.UploadedTo.Value);
        }

        // Uploader filter
        if (request.UploaderIds != null && request.UploaderIds.Any())
        {
            query = query.Where(d => d.UploadedBy.HasValue && request.UploaderIds.Contains(d.UploadedBy.Value));
        }

        // Template filter
        if (request.TemplateId.HasValue)
        {
            query = query.Where(d => d.TemplateId == request.TemplateId.Value);
        }

        // Tag filter
        if (request.Tags != null && request.Tags.Any())
        {
            if (request.MatchAllTags)
            {
                // AND logic: document must have all specified tags
                foreach (var tag in request.Tags)
                {
                    var tagName = tag;
                    query = query.Where(d => d.TagMappings.Any(tm => tm.Tag.Name == tagName));
                }
            }
            else
            {
                // OR logic: document must have at least one of the specified tags
                query = query.Where(d => d.TagMappings.Any(tm => request.Tags.Contains(tm.Tag.Name)));
            }
        }

        // Search term (filename and description)
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchTerm = request.SearchTerm.ToLower();
            query = query.Where(d =>
                d.FileName.ToLower().Contains(searchTerm) ||
                (d.Description != null && d.Description.ToLower().Contains(searchTerm)));
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync();

        // Apply sorting
        query = request.SortBy switch
        {
            DocumentSortBy.FileName => request.SortDescending
                ? query.OrderByDescending(d => d.FileName)
                : query.OrderBy(d => d.FileName),
            DocumentSortBy.FileSize => request.SortDescending
                ? query.OrderByDescending(d => d.FileSize)
                : query.OrderBy(d => d.FileSize),
            DocumentSortBy.DocumentType => request.SortDescending
                ? query.OrderByDescending(d => d.Type)
                : query.OrderBy(d => d.Type),
            DocumentSortBy.Relevance => query.OrderByDescending(d => d.CreatedAt), // Default to date for now
            _ => request.SortDescending
                ? query.OrderByDescending(d => d.CreatedAt)
                : query.OrderBy(d => d.CreatedAt)
        };

        // Pagination
        var results = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(d => new DocumentSearchResult
            {
                Id = d.Id,
                GroupId = d.GroupId,
                FileName = d.FileName,
                Type = d.Type,
                FileSize = d.FileSize,
                SignatureStatus = d.SignatureStatus,
                Description = d.Description,
                CreatedAt = d.CreatedAt,
                UploaderName = _context.Users
                    .Where(u => u.Id == d.UploadedBy)
                    .Select(u => u.Email)
                    .FirstOrDefault() ?? "Unknown",
                UploaderId = d.UploadedBy,
                Tags = d.TagMappings.Select(tm => tm.Tag.Name).ToList(),
                TemplateName = d.Template != null ? d.Template.Name : null,
                IsDeleted = d.IsDeleted,
                DeletedAt = d.DeletedAt
            })
            .ToListAsync();

        // Build facets (for filter UI)
        var facets = await BuildSearchFacetsAsync(request.GroupId);

        return new AdvancedDocumentSearchResponse
        {
            Results = results,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
            Facets = facets
        };
    }

    public async Task<List<DocumentSearchResult>> GetRecentDocumentsAsync(
        RecentDocumentsRequest request, Guid userId)
    {
        var query = _context.Documents
            .Include(d => d.TagMappings)
                .ThenInclude(tm => tm.Tag)
            .Include(d => d.Template)
            .AsQueryable();

        // Filter by group if specified
        if (request.GroupId.HasValue)
        {
            var hasAccess = await _context.GroupMembers
                .AnyAsync(gm => gm.GroupId == request.GroupId.Value && gm.UserId == userId);

            if (!hasAccess)
            {
                throw new UnauthorizedAccessException("User does not have access to this group");
            }

            query = query.Where(d => d.GroupId == request.GroupId.Value);
        }
        else
        {
            // Get all groups user is a member of
            var userGroupIds = await _context.GroupMembers
                .Where(gm => gm.UserId == userId)
                .Select(gm => gm.GroupId)
                .ToListAsync();

            query = query.Where(d => userGroupIds.Contains(d.GroupId));
        }

        var results = await query
            .OrderByDescending(d => d.CreatedAt)
            .Take(request.Count)
            .Select(d => new DocumentSearchResult
            {
                Id = d.Id,
                GroupId = d.GroupId,
                FileName = d.FileName,
                Type = d.Type,
                FileSize = d.FileSize,
                SignatureStatus = d.SignatureStatus,
                Description = d.Description,
                CreatedAt = d.CreatedAt,
                UploaderName = _context.Users
                    .Where(u => u.Id == d.UploadedBy)
                    .Select(u => u.Email)
                    .FirstOrDefault() ?? "Unknown",
                UploaderId = d.UploadedBy,
                Tags = d.TagMappings.Select(tm => tm.Tag.Name).ToList(),
                TemplateName = d.Template != null ? d.Template.Name : null
            })
            .ToListAsync();

        return results;
    }

    public async Task<List<DocumentSearchResult>> GetSharedWithMeAsync(
        Guid userId, int page = 1, int pageSize = 20)
    {
        // Get all shares where the user's email matches
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            throw new KeyNotFoundException("User not found");
        }

        var shares = await _context.DocumentShares
            .Include(s => s.Document)
                .ThenInclude(d => d.TagMappings)
                    .ThenInclude(tm => tm.Tag)
            .Include(s => s.Document.Template)
            .Where(s => s.RecipientEmail == user.Email && !s.IsRevoked)
            .Where(s => !s.ExpiresAt.HasValue || s.ExpiresAt.Value > DateTime.UtcNow)
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new DocumentSearchResult
            {
                Id = s.Document.Id,
                GroupId = s.Document.GroupId,
                FileName = s.Document.FileName,
                Type = s.Document.Type,
                FileSize = s.Document.FileSize,
                SignatureStatus = s.Document.SignatureStatus,
                Description = s.Document.Description,
                CreatedAt = s.Document.CreatedAt,
                UploaderName = _context.Users
                    .Where(u => u.Id == s.Document.UploadedBy)
                    .Select(u => u.Email)
                    .FirstOrDefault() ?? "Unknown",
                UploaderId = s.Document.UploadedBy,
                Tags = s.Document.TagMappings.Select(tm => tm.Tag.Name).ToList(),
                TemplateName = s.Document.Template != null ? s.Document.Template.Name : null
            })
            .ToListAsync();

        return shares;
    }

    #region Tag Management

    public async Task<TagResponse> CreateTagAsync(CreateTagRequest request, Guid userId)
    {
        _logger.LogInformation("Creating tag '{Name}' by user {UserId}", request.Name, userId);

        // Verify user has access to group if group-scoped
        if (request.GroupId.HasValue)
        {
            var hasAccess = await _context.GroupMembers
                .AnyAsync(gm => gm.GroupId == request.GroupId.Value && gm.UserId == userId);

            if (!hasAccess)
            {
                throw new UnauthorizedAccessException("User does not have access to this group");
            }
        }

        // Check if tag already exists
        var existingTag = await _context.DocumentTags
            .FirstOrDefaultAsync(t => t.Name == request.Name && t.GroupId == request.GroupId);

        if (existingTag != null)
        {
            throw new InvalidOperationException($"Tag '{request.Name}' already exists");
        }

        var tag = new DocumentTag
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Color = request.Color,
            Description = request.Description,
            GroupId = request.GroupId,
            CreatedBy = userId,
            UsageCount = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.DocumentTags.Add(tag);
        await _context.SaveChangesAsync();

        return new TagResponse
        {
            Id = tag.Id,
            Name = tag.Name,
            Color = tag.Color,
            Description = tag.Description,
            UsageCount = tag.UsageCount,
            CreatedAt = tag.CreatedAt
        };
    }

    public async Task<List<TagResponse>> GetTagsAsync(Guid? groupId, Guid userId)
    {
        var query = _context.DocumentTags.AsQueryable();

        if (groupId.HasValue)
        {
            // Verify user has access
            var hasAccess = await _context.GroupMembers
                .AnyAsync(gm => gm.GroupId == groupId.Value && gm.UserId == userId);

            if (!hasAccess)
            {
                throw new UnauthorizedAccessException("User does not have access to this group");
            }

            // Get group-specific and system-wide tags
            query = query.Where(t => t.GroupId == groupId.Value || t.GroupId == null);
        }
        else
        {
            // Get only system-wide tags
            query = query.Where(t => t.GroupId == null);
        }

        var tags = await query
            .OrderByDescending(t => t.UsageCount)
            .ThenBy(t => t.Name)
            .Select(t => new TagResponse
            {
                Id = t.Id,
                Name = t.Name,
                Color = t.Color,
                Description = t.Description,
                UsageCount = t.UsageCount,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync();

        return tags;
    }

    public async Task AddTagsToDocumentAsync(Guid documentId, AddTagsToDocumentRequest request, Guid userId)
    {
        _logger.LogInformation("Adding {TagCount} tags to document {DocumentId}", request.TagNames.Count, documentId);

        var document = await _context.Documents
            .Include(d => d.TagMappings)
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document == null)
        {
            throw new KeyNotFoundException($"Document with ID {documentId} not found");
        }

        // Verify user has access
        var hasAccess = await _context.GroupMembers
            .AnyAsync(gm => gm.GroupId == document.GroupId && gm.UserId == userId);

        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("User does not have access to this document");
        }

        foreach (var tagName in request.TagNames)
        {
            // Find or create tag
            var tag = await _context.DocumentTags
                .FirstOrDefaultAsync(t => t.Name == tagName &&
                    (t.GroupId == document.GroupId || t.GroupId == null));

            if (tag == null)
            {
                // Auto-create tag
                tag = new DocumentTag
                {
                    Id = Guid.NewGuid(),
                    Name = tagName,
                    GroupId = document.GroupId,
                    CreatedBy = userId,
                    UsageCount = 0,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.DocumentTags.Add(tag);
            }

            // Check if already tagged
            var existingMapping = document.TagMappings
                .FirstOrDefault(tm => tm.TagId == tag.Id);

            if (existingMapping == null)
            {
                var mapping = new DocumentTagMapping
                {
                    DocumentId = documentId,
                    TagId = tag.Id,
                    TaggedAt = DateTime.UtcNow,
                    TaggedBy = userId
                };

                _context.DocumentTagMappings.Add(mapping);
                tag.UsageCount++;
            }
        }

        await _context.SaveChangesAsync();
    }

    public async Task RemoveTagFromDocumentAsync(Guid documentId, string tagName, Guid userId)
    {
        _logger.LogInformation("Removing tag '{TagName}' from document {DocumentId}", tagName, documentId);

        var document = await _context.Documents.FindAsync(documentId);
        if (document == null)
        {
            throw new KeyNotFoundException($"Document with ID {documentId} not found");
        }

        // Verify user has access
        var hasAccess = await _context.GroupMembers
            .AnyAsync(gm => gm.GroupId == document.GroupId && gm.UserId == userId);

        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("User does not have access to this document");
        }

        var tag = await _context.DocumentTags
            .FirstOrDefaultAsync(t => t.Name == tagName);

        if (tag == null)
        {
            throw new KeyNotFoundException($"Tag '{tagName}' not found");
        }

        var mapping = await _context.DocumentTagMappings
            .FirstOrDefaultAsync(tm => tm.DocumentId == documentId && tm.TagId == tag.Id);

        if (mapping != null)
        {
            _context.DocumentTagMappings.Remove(mapping);
            tag.UsageCount = Math.Max(0, tag.UsageCount - 1);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<TagSuggestionResponse> GetTagSuggestionsAsync(Guid documentId, Guid userId)
    {
        var document = await _context.Documents.FindAsync(documentId);
        if (document == null)
        {
            throw new KeyNotFoundException($"Document with ID {documentId} not found");
        }

        // Verify user has access
        var hasAccess = await _context.GroupMembers
            .AnyAsync(gm => gm.GroupId == document.GroupId && gm.UserId == userId);

        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("User does not have access to this document");
        }

        var suggestions = GenerateTagSuggestions(document);

        return new TagSuggestionResponse
        {
            SuggestedTags = suggestions
        };
    }

    private List<string> GenerateTagSuggestions(Document document)
    {
        var suggestions = new List<string>();

        // Add type-based tag
        suggestions.Add(document.Type.ToString().ToLower());

        // Extract year from filename or creation date
        var yearMatch = Regex.Match(document.FileName, @"\b(20\d{2})\b");
        if (yearMatch.Success)
        {
            suggestions.Add($"year-{yearMatch.Value}");
        }
        else
        {
            suggestions.Add($"year-{document.CreatedAt.Year}");
        }

        // Add common keywords based on filename
        var fileName = document.FileName.ToLower();
        if (fileName.Contains("insurance")) suggestions.Add("insurance");
        if (fileName.Contains("contract")) suggestions.Add("contract");
        if (fileName.Contains("agreement")) suggestions.Add("agreement");
        if (fileName.Contains("invoice")) suggestions.Add("invoice");
        if (fileName.Contains("receipt")) suggestions.Add("receipt");
        if (fileName.Contains("maintenance")) suggestions.Add("maintenance");
        if (fileName.Contains("repair")) suggestions.Add("repair");

        // Add signature status tag
        if (document.SignatureStatus == SignatureStatus.FullySigned)
        {
            suggestions.Add("signed");
        }

        return suggestions.Distinct().ToList();
    }

    #endregion

    #region Saved Searches

    public async Task<SavedSearchResponse> CreateSavedSearchAsync(CreateSavedSearchRequest request, Guid userId)
    {
        _logger.LogInformation("Creating saved search '{Name}' for user {UserId}", request.Name, userId);

        if (request.GroupId.HasValue)
        {
            var hasAccess = await _context.GroupMembers
                .AnyAsync(gm => gm.GroupId == request.GroupId.Value && gm.UserId == userId);

            if (!hasAccess)
            {
                throw new UnauthorizedAccessException("User does not have access to this group");
            }
        }

        var savedSearch = new SavedDocumentSearch
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            UserId = userId,
            GroupId = request.GroupId,
            SearchCriteriaJson = JsonSerializer.Serialize(request.SearchCriteria),
            UsageCount = 0,
            IsDefault = request.IsDefault,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.SavedDocumentSearches.Add(savedSearch);
        await _context.SaveChangesAsync();

        return new SavedSearchResponse
        {
            Id = savedSearch.Id,
            Name = savedSearch.Name,
            Description = savedSearch.Description,
            GroupId = savedSearch.GroupId,
            UsageCount = savedSearch.UsageCount,
            LastUsedAt = savedSearch.LastUsedAt,
            IsDefault = savedSearch.IsDefault,
            CreatedAt = savedSearch.CreatedAt
        };
    }

    public async Task<List<SavedSearchResponse>> GetSavedSearchesAsync(Guid userId, Guid? groupId = null)
    {
        var query = _context.SavedDocumentSearches
            .Where(s => s.UserId == userId);

        if (groupId.HasValue)
        {
            query = query.Where(s => s.GroupId == groupId.Value);
        }

        var searches = await query
            .OrderByDescending(s => s.IsDefault)
            .ThenByDescending(s => s.LastUsedAt)
            .ThenByDescending(s => s.CreatedAt)
            .Select(s => new SavedSearchResponse
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description,
                GroupId = s.GroupId,
                UsageCount = s.UsageCount,
                LastUsedAt = s.LastUsedAt,
                IsDefault = s.IsDefault,
                CreatedAt = s.CreatedAt
            })
            .ToListAsync();

        return searches;
    }

    public async Task<AdvancedDocumentSearchResponse> ExecuteSavedSearchAsync(Guid savedSearchId, Guid userId)
    {
        var savedSearch = await _context.SavedDocumentSearches
            .FirstOrDefaultAsync(s => s.Id == savedSearchId && s.UserId == userId);

        if (savedSearch == null)
        {
            throw new KeyNotFoundException($"Saved search with ID {savedSearchId} not found");
        }

        // Update usage statistics
        savedSearch.UsageCount++;
        savedSearch.LastUsedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Deserialize and execute search
        var searchCriteria = JsonSerializer.Deserialize<AdvancedDocumentSearchRequest>(savedSearch.SearchCriteriaJson);
        if (searchCriteria == null)
        {
            throw new InvalidOperationException("Failed to deserialize search criteria");
        }

        return await SearchDocumentsAsync(searchCriteria, userId);
    }

    public async Task DeleteSavedSearchAsync(Guid savedSearchId, Guid userId)
    {
        var savedSearch = await _context.SavedDocumentSearches
            .FirstOrDefaultAsync(s => s.Id == savedSearchId && s.UserId == userId);

        if (savedSearch == null)
        {
            throw new KeyNotFoundException($"Saved search with ID {savedSearchId} not found");
        }

        _context.SavedDocumentSearches.Remove(savedSearch);
        await _context.SaveChangesAsync();
    }

    #endregion

    #region Bulk Operations

    public async Task<BulkDownloadResponse> BulkDownloadAsync(BulkDownloadRequest request, Guid userId)
    {
        _logger.LogInformation("Bulk downloading {Count} documents for user {UserId}",
            request.DocumentIds.Count, userId);

        if (request.DocumentIds.Count > 50)
        {
            throw new ArgumentException("Maximum 50 documents can be downloaded at once");
        }

        // Verify user has access to all documents
        var documents = new List<Document>();
        foreach (var docId in request.DocumentIds)
        {
            var doc = await _context.Documents.FindAsync(docId);
            if (doc == null) continue;

            var hasAccess = await _context.GroupMembers
                .AnyAsync(gm => gm.GroupId == doc.GroupId && gm.UserId == userId);

            if (hasAccess)
            {
                documents.Add(doc);
            }
        }

        if (!documents.Any())
        {
            throw new InvalidOperationException("No accessible documents found");
        }

        // Create ZIP file
        var zipFileName = request.ZipFileName ?? $"documents_{DateTime.UtcNow:yyyyMMdd_HHmmss}.zip";
        if (!zipFileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            zipFileName += ".zip";
        }

        using var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
        {
            foreach (var doc in documents)
            {
                try
                {
                    var fileStream = await _fileStorage.DownloadFileAsync(doc.StorageKey);
                    var entry = archive.CreateEntry(doc.FileName, CompressionLevel.Fastest);

                    using var entryStream = entry.Open();
                    await fileStream.CopyToAsync(entryStream);
                    await fileStream.DisposeAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to add document {DocumentId} to ZIP", doc.Id);
                }
            }
        }

        // Upload ZIP to temporary storage
        zipStream.Position = 0;
        var zipStorageKey = $"temp/bulk-downloads/{userId}/{Guid.NewGuid()}.zip";
        await _fileStorage.UploadFileAsync(zipStream, zipStorageKey, "application/zip");

        // Generate download URL with expiration
        var downloadUrl = await _fileStorage.GetSecureUrlAsync(zipStorageKey, TimeSpan.FromHours(24));

        return new BulkDownloadResponse
        {
            DownloadUrl = downloadUrl,
            FileName = zipFileName,
            TotalSize = zipStream.Length,
            DocumentCount = documents.Count,
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        };
    }

    public async Task<BulkTagResponse> BulkTagAsync(BulkTagRequest request, Guid userId)
    {
        _logger.LogInformation("Bulk {Action} tags on {Count} documents",
            request.RemoveTags ? "removing" : "adding", request.DocumentIds.Count);

        var successCount = 0;
        var failedCount = 0;
        var errors = new List<BulkOperationError>();

        foreach (var docId in request.DocumentIds)
        {
            try
            {
                if (request.RemoveTags)
                {
                    foreach (var tagName in request.TagNames)
                    {
                        await RemoveTagFromDocumentAsync(docId, tagName, userId);
                    }
                }
                else
                {
                    await AddTagsToDocumentAsync(docId, new AddTagsToDocumentRequest
                    {
                        TagNames = request.TagNames
                    }, userId);
                }

                successCount++;
            }
            catch (Exception ex)
            {
                failedCount++;
                var doc = await _context.Documents.FindAsync(docId);
                errors.Add(new BulkOperationError
                {
                    DocumentId = docId,
                    FileName = doc?.FileName ?? "Unknown",
                    ErrorMessage = ex.Message
                });
            }
        }

        return new BulkTagResponse
        {
            SuccessCount = successCount,
            FailedCount = failedCount,
            Errors = errors
        };
    }

    public async Task<BulkDeleteResponse> BulkDeleteAsync(BulkDeleteRequest request, Guid userId)
    {
        _logger.LogInformation("Bulk deleting {Count} documents", request.DocumentIds.Count);

        var successCount = 0;
        var failedCount = 0;
        var errors = new List<BulkDeleteResult>();

        foreach (var docId in request.DocumentIds)
        {
            try
            {
                var doc = await _context.Documents
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(d => d.Id == docId);

                if (doc == null)
                {
                    failedCount++;
                    errors.Add(new BulkDeleteResult
                    {
                        DocumentId = docId,
                        Success = false,
                        ErrorMessage = "Document not found"
                    });
                    continue;
                }

                // Check if already deleted
                if (doc.IsDeleted)
                {
                    failedCount++;
                    errors.Add(new BulkDeleteResult
                    {
                        DocumentId = docId,
                        Success = false,
                        ErrorMessage = "Document already deleted"
                    });
                    continue;
                }

                // Check if fully signed
                if (doc.SignatureStatus == SignatureStatus.FullySigned)
                {
                    failedCount++;
                    errors.Add(new BulkDeleteResult
                    {
                        DocumentId = docId,
                        Success = false,
                        ErrorMessage = "Cannot delete fully signed documents"
                    });
                    continue;
                }

                // Check authorization
                var isAdmin = await _context.GroupMembers
                    .AnyAsync(gm => gm.GroupId == doc.GroupId &&
                                   gm.UserId == userId &&
                                   gm.RoleInGroup == GroupRole.Admin);

                var isUploader = doc.UploadedBy == userId;

                if (!isAdmin && !isUploader)
                {
                    failedCount++;
                    errors.Add(new BulkDeleteResult
                    {
                        DocumentId = docId,
                        Success = false,
                        ErrorMessage = "Insufficient permissions"
                    });
                    continue;
                }

                // Perform soft delete
                doc.IsDeleted = true;
                doc.DeletedAt = DateTime.UtcNow;
                doc.DeletedBy = userId;
                doc.UpdatedAt = DateTime.UtcNow;

                successCount++;
                errors.Add(new BulkDeleteResult
                {
                    DocumentId = docId,
                    Success = true
                });
            }
            catch (Exception ex)
            {
                failedCount++;
                errors.Add(new BulkDeleteResult
                {
                    DocumentId = docId,
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        await _context.SaveChangesAsync();

        return new BulkDeleteResponse
        {
            TotalRequested = request.DocumentIds.Count,
            SuccessfullyDeleted = successCount,
            Failed = failedCount,
            Results = errors
        };
    }

    #endregion

    #region Helper Methods

    private async Task<SearchFacets> BuildSearchFacetsAsync(Guid groupId)
    {
        var documents = await _context.Documents
            .Include(d => d.TagMappings)
                .ThenInclude(tm => tm.Tag)
            .Include(d => d.Template)
            .Where(d => d.GroupId == groupId && !d.IsDeleted)
            .ToListAsync();

        var facets = new SearchFacets
        {
            DocumentTypes = documents
                .GroupBy(d => d.Type)
                .ToDictionary(g => g.Key, g => g.Count()),

            SignatureStatuses = documents
                .GroupBy(d => d.SignatureStatus)
                .ToDictionary(g => g.Key, g => g.Count()),

            Tags = documents
                .SelectMany(d => d.TagMappings.Select(tm => tm.Tag.Name))
                .GroupBy(name => name)
                .ToDictionary(g => g.Key, g => g.Count()),

            Uploaders = documents
                .Where(d => d.UploadedBy.HasValue)
                .GroupBy(d => d.UploadedBy!.Value)
                .ToDictionary(
                    g => _context.Users.Find(g.Key)?.Email ?? "Unknown",
                    g => g.Count()),

            Templates = documents
                .Where(d => d.Template != null)
                .GroupBy(d => d.Template!.Name)
                .ToDictionary(g => g.Key, g => g.Count())
        };

        return facets;
    }

    #endregion
}
