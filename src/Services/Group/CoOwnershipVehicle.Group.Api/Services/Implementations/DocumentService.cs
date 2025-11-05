using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Group.Api.Data;
using CoOwnershipVehicle.Group.Api.DTOs;
using CoOwnershipVehicle.Group.Api.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text.Json;

namespace CoOwnershipVehicle.Group.Api.Services.Implementations;

public class DocumentService : IDocumentService
{
    private readonly GroupDbContext _context;
    private readonly IFileStorageService _fileStorage;
    private readonly IVirusScanService _virusScan;
    private readonly ISigningTokenService _signingTokenService;
    private readonly ICertificateGenerationService _certificateService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<DocumentService> _logger;

    private static readonly string[] AllowedExtensions = { ".pdf", ".docx", ".doc", ".jpg", ".jpeg", ".png" };
    private static readonly string[] AllowedContentTypes =
    {
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/msword",
        "image/jpeg",
        "image/png"
    };
    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50MB

    public DocumentService(
        GroupDbContext context,
        IFileStorageService fileStorage,
        IVirusScanService virusScan,
        ISigningTokenService signingTokenService,
        ICertificateGenerationService certificateService,
        INotificationService notificationService,
        ILogger<DocumentService> logger)
    {
        _context = context;
        _fileStorage = fileStorage;
        _virusScan = virusScan;
        _signingTokenService = signingTokenService;
        _certificateService = certificateService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<DocumentUploadResponse> UploadDocumentAsync(DocumentUploadRequest request, Guid userId)
    {
        // Validate user is member of group
        var membership = await _context.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == request.GroupId && gm.UserId == userId);

        if (membership == null)
        {
            throw new UnauthorizedAccessException("User is not a member of this group");
        }

        // Validate file
        var (isValid, errorMessage) = await ValidateFileAsync(request.File);
        if (!isValid)
        {
            throw new ArgumentException(errorMessage);
        }

        // Copy the file to a memory stream to allow multiple reads
        using var memoryStream = new MemoryStream();
        await request.File.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        // Check for duplicate file
        var fileHash = await ComputeFileHashAsync(memoryStream);

        var existingDocument = await _context.Documents
            .FirstOrDefaultAsync(d => d.GroupId == request.GroupId && d.FileHash == fileHash);

        if (existingDocument != null)
        {
            throw new InvalidOperationException(
                $"A document with the same content already exists: {existingDocument.FileName}");
        }

        // Scan for viruses
        memoryStream.Position = 0;
        var scanResult = await _virusScan.ScanFileAsync(memoryStream, request.File.FileName);

        if (!scanResult.IsClean)
        {
            _logger.LogWarning("Virus detected in file {FileName}: {ThreatName}",
                request.File.FileName, scanResult.ThreatName);
            throw new InvalidOperationException(
                $"File failed security scan: {scanResult.ThreatName ?? "Potential threat detected"}");
        }

        // Generate unique storage key
        var extension = Path.GetExtension(request.File.FileName);
        var storageKey = $"documents/{request.GroupId}/{Guid.NewGuid()}{extension}";

        // Upload file to storage
        memoryStream.Position = 0;
        await _fileStorage.UploadFileAsync(memoryStream, storageKey, request.File.ContentType);

        // Create document record
        var document = new Document
        {
            Id = Guid.NewGuid(),
            GroupId = request.GroupId,
            Type = request.DocumentType,
            StorageKey = storageKey,
            FileName = request.File.FileName,
            FileSize = request.File.Length,
            ContentType = request.File.ContentType,
            FileHash = fileHash,
            SignatureStatus = SignatureStatus.Draft,
            Description = request.Description,
            IsVirusScanned = true,
            VirusScanPassed = scanResult.IsClean,
            UploadedBy = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        // Generate secure URL
        var secureUrl = await _fileStorage.GetSecureUrlAsync(storageKey);

        _logger.LogInformation("Document {DocumentId} uploaded successfully by user {UserId}",
            document.Id, userId);

        var user = await _context.Users.FindAsync(userId);

        return new DocumentUploadResponse
        {
            Id = document.Id,
            GroupId = document.GroupId,
            Type = document.Type,
            FileName = document.FileName,
            FileSize = document.FileSize,
            ContentType = document.ContentType,
            SignatureStatus = document.SignatureStatus,
            Description = document.Description,
            SecureUrl = secureUrl,
            UploadedAt = document.CreatedAt,
            UploadedBy = user?.Email ?? "Unknown"
        };
    }

    public async Task<List<DocumentListResponse>> GetGroupDocumentsAsync(Guid groupId, Guid userId)
    {
        var hasAccess = await _context.GroupMembers
            .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId);

        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("User does not have access to this group");
        }

        var documents = await _context.Documents
            .Where(d => d.GroupId == groupId)
            .Include(d => d.Signatures)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new DocumentListResponse
            {
                Id = d.Id,
                GroupId = d.GroupId,
                Type = d.Type,
                FileName = d.FileName,
                FileSize = d.FileSize,
                SignatureStatus = d.SignatureStatus,
                Description = d.Description,
                CreatedAt = d.CreatedAt,
                SignatureCount = d.Signatures.Count
            })
            .ToListAsync();

        return documents;
    }

    public async Task<PaginatedDocumentResponse> GetGroupDocumentsPaginatedAsync(
        Guid groupId, Guid userId, DocumentQueryParameters parameters)
    {
        // Check if user is a member of the group
        var membership = await _context.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == userId);

        if (membership == null)
        {
            throw new UnauthorizedAccessException("User does not have access to this group");
        }

        // Check if user is admin (required for viewing deleted documents)
        var isAdmin = membership.RoleInGroup == GroupRole.Admin;

        // Validate admin-only parameters
        if ((parameters.IncludeDeleted || parameters.OnlyDeleted) && !isAdmin)
        {
            throw new UnauthorizedAccessException("Only group admins can view deleted documents");
        }

        // Build query - use IgnoreQueryFilters if we need to see deleted documents
        var query = (parameters.IncludeDeleted || parameters.OnlyDeleted)
            ? _context.Documents.IgnoreQueryFilters()
            : _context.Documents;

        query = query
            .Where(d => d.GroupId == groupId)
            .Include(d => d.Signatures)
            .Include(d => d.Downloads)
            .AsQueryable();

        // Apply deleted document filter
        if (parameters.OnlyDeleted)
        {
            query = query.Where(d => d.IsDeleted);
        }
        else if (!parameters.IncludeDeleted)
        {
            // Default: exclude deleted (query filter handles this automatically if not using IgnoreQueryFilters)
            query = query.Where(d => !d.IsDeleted);
        }

        // Apply filters
        if (parameters.DocumentType.HasValue)
        {
            query = query.Where(d => d.Type == parameters.DocumentType.Value);
        }

        if (parameters.SignatureStatus.HasValue)
        {
            query = query.Where(d => d.SignatureStatus == parameters.SignatureStatus.Value);
        }

        // Apply search
        if (!string.IsNullOrWhiteSpace(parameters.SearchTerm))
        {
            var searchTerm = parameters.SearchTerm.ToLower();
            query = query.Where(d =>
                d.FileName.ToLower().Contains(searchTerm) ||
                (d.Description != null && d.Description.ToLower().Contains(searchTerm)));
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync();

        // Apply sorting
        query = parameters.SortBy?.ToLower() switch
        {
            "filename" => parameters.SortDescending
                ? query.OrderByDescending(d => d.FileName)
                : query.OrderBy(d => d.FileName),
            "filesize" => parameters.SortDescending
                ? query.OrderByDescending(d => d.FileSize)
                : query.OrderBy(d => d.FileSize),
            "type" => parameters.SortDescending
                ? query.OrderByDescending(d => d.Type)
                : query.OrderBy(d => d.Type),
            _ => parameters.SortDescending
                ? query.OrderByDescending(d => d.CreatedAt)
                : query.OrderBy(d => d.CreatedAt)
        };

        // Apply pagination
        var documents = await query
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .Select(d => new DocumentListItemResponse
            {
                Id = d.Id,
                GroupId = d.GroupId,
                Type = d.Type,
                FileName = d.FileName,
                FileSize = d.FileSize,
                SignatureStatus = d.SignatureStatus,
                Description = d.Description,
                CreatedAt = d.CreatedAt,
                SignatureCount = d.Signatures.Count,
                SignedCount = d.Signatures.Count(s => s.SignedAt != null),
                UploaderId = d.UploadedBy ?? Guid.Empty,
                UploaderName = _context.Users
                    .Where(u => u.Id == d.UploadedBy)
                    .Select(u => u.Email)
                    .FirstOrDefault() ?? "Unknown",
                DownloadCount = d.Downloads.Count,
                // Soft delete fields
                IsDeleted = d.IsDeleted,
                DeletedAt = d.DeletedAt,
                DeletedBy = d.DeletedBy,
                DeletedByName = d.DeletedBy.HasValue
                    ? _context.Users
                        .Where(u => u.Id == d.DeletedBy)
                        .Select(u => u.FirstName + " " + u.LastName)
                        .FirstOrDefault()
                    : null
            })
            .ToListAsync();

        return new PaginatedDocumentResponse
        {
            Items = documents,
            TotalCount = totalCount,
            Page = parameters.Page,
            PageSize = parameters.PageSize
        };
    }

    public async Task<DocumentDetailResponse> GetDocumentByIdAsync(Guid documentId, Guid userId)
    {
        var document = await _context.Documents
            .Include(d => d.Signatures)
            .ThenInclude(s => s.Signer)
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document == null)
        {
            throw new KeyNotFoundException($"Document with ID {documentId} not found");
        }

        var hasAccess = await _context.GroupMembers
            .AnyAsync(gm => gm.GroupId == document.GroupId && gm.UserId == userId);

        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("User does not have access to this document");
        }

        var secureUrl = await _fileStorage.GetSecureUrlAsync(document.StorageKey);

        return new DocumentDetailResponse
        {
            Id = document.Id,
            GroupId = document.GroupId,
            Type = document.Type,
            FileName = document.FileName,
            FileSize = document.FileSize,
            ContentType = document.ContentType,
            SignatureStatus = document.SignatureStatus,
            Description = document.Description,
            SecureUrl = secureUrl,
            CreatedAt = document.CreatedAt,
            UpdatedAt = document.UpdatedAt,
            PageCount = document.PageCount,
            Author = document.Author,
            IsVirusScanned = document.IsVirusScanned,
            Signatures = document.Signatures.Select(s => new DocumentSignatureResponse
            {
                Id = s.Id,
                SignerId = s.SignerId,
                SignerName = s.Signer?.Email ?? "Unknown",
                SignedAt = s.SignedAt,
                SignatureOrder = s.SignatureOrder,
                Status = s.Status
            }).ToList()
        };
    }

    public async Task DeleteDocumentAsync(Guid documentId, Guid userId)
    {
        // Load document with all its versions
        var document = await _context.Documents
            .IgnoreQueryFilters() // Include soft-deleted documents
            .Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document == null)
        {
            throw new KeyNotFoundException($"Document with ID {documentId} not found");
        }

        // Check if already soft-deleted
        if (document.IsDeleted)
        {
            throw new InvalidOperationException("Document is already deleted");
        }

        // CRITICAL: Prevent deletion of fully signed documents (legal protection)
        if (document.SignatureStatus == SignatureStatus.FullySigned)
        {
            throw new InvalidOperationException(
                "Cannot delete fully signed documents. This document has legal binding signatures and must be preserved.");
        }

        // Check authorization: user must be group admin OR document uploader
        var isAdmin = await _context.GroupMembers
            .AnyAsync(gm => gm.GroupId == document.GroupId &&
                           gm.UserId == userId &&
                           gm.RoleInGroup == GroupRole.Admin);

        var isUploader = document.UploadedBy == userId;

        if (!isAdmin && !isUploader)
        {
            throw new UnauthorizedAccessException("Only group admins or the document uploader can delete documents");
        }

        _logger.LogInformation("Soft deleting document {DocumentId} with {VersionCount} versions",
            documentId, document.Versions.Count);

        // Perform soft delete
        document.IsDeleted = true;
        document.DeletedAt = DateTime.UtcNow;
        document.DeletedBy = userId;
        document.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Document {DocumentId} soft-deleted by user {UserId}. Files remain in storage for recovery.",
            documentId, userId);

        // Send notification to group admins
        try
        {
            var groupAdmins = await _context.GroupMembers
                .Where(gm => gm.GroupId == document.GroupId &&
                            gm.RoleInGroup == GroupRole.Admin)
                .Include(gm => gm.User)
                .Select(gm => gm.User)
                .ToListAsync();

            var deletingUser = await _context.Users.FindAsync(userId);

            if (groupAdmins.Any() && deletingUser != null)
            {
                await _notificationService.SendDocumentDeletedNotificationAsync(
                    groupAdmins, document, deletingUser);

                _logger.LogInformation(
                    "Delete notification sent to {AdminCount} group admins for document {DocumentId}",
                    groupAdmins.Count, documentId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send delete notification for document {DocumentId}. Document was deleted successfully.", documentId);
            // Don't throw - notification failure shouldn't prevent deletion
        }
    }

    public async Task RestoreDocumentAsync(Guid documentId, Guid userId)
    {
        // Load document including soft-deleted ones
        var document = await _context.Documents
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document == null)
        {
            throw new KeyNotFoundException($"Document with ID {documentId} not found");
        }

        if (!document.IsDeleted)
        {
            throw new InvalidOperationException("Document is not deleted");
        }

        var isAdmin = await _context.GroupMembers
            .AnyAsync(gm => gm.GroupId == document.GroupId &&
                           gm.UserId == userId &&
                           gm.RoleInGroup == GroupRole.Admin);

        if (!isAdmin)
        {
            throw new UnauthorizedAccessException("Only group admins can restore documents");
        }

        // Restore document
        document.IsDeleted = false;
        document.DeletedAt = null;
        document.DeletedBy = null;
        document.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Document {DocumentId} restored by user {UserId}", documentId, userId);

        // Send notification to group admins
        try
        {
            var groupAdmins = await _context.GroupMembers
                .Where(gm => gm.GroupId == document.GroupId &&
                            gm.RoleInGroup == GroupRole.Admin)
                .Include(gm => gm.User)
                .Select(gm => gm.User)
                .ToListAsync();

            var restoringUser = await _context.Users.FindAsync(userId);

            if (groupAdmins.Any() && restoringUser != null)
            {
                await _notificationService.SendDocumentRestoredNotificationAsync(
                    groupAdmins, document, restoringUser);

                _logger.LogInformation(
                    "Restore notification sent to {AdminCount} group admins for document {DocumentId}",
                    groupAdmins.Count, documentId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send restore notification for document {DocumentId}. Document was restored successfully.", documentId);
            // Don't throw - notification failure shouldn't prevent restoration
        }
    }

    public async Task PermanentlyDeleteDocumentAsync(Guid documentId, Guid userId)
    {
        // Load document including soft-deleted ones
        var document = await _context.Documents
            .IgnoreQueryFilters()
            .Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document == null)
        {
            throw new KeyNotFoundException($"Document with ID {documentId} not found");
        }

        if (!document.IsDeleted)
        {
            throw new InvalidOperationException("Document must be soft-deleted before permanent deletion. Call DeleteDocument first.");
        }

        var isAdmin = await _context.GroupMembers
            .AnyAsync(gm => gm.GroupId == document.GroupId &&
                           gm.UserId == userId &&
                           gm.RoleInGroup == GroupRole.Admin);

        if (!isAdmin)
        {
            throw new UnauthorizedAccessException("Only group admins can permanently delete documents");
        }

        _logger.LogInformation("Permanently deleting document {DocumentId} with {VersionCount} versions",
            documentId, document.Versions.Count);

        // Delete all version files from storage
        var deletedVersionFiles = 0;
        var failedVersionFiles = new List<string>();

        foreach (var version in document.Versions)
        {
            try
            {
                await _fileStorage.DeleteFileAsync(version.StorageKey);
                deletedVersionFiles++;
                _logger.LogDebug("Deleted version file: {StorageKey}", version.StorageKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete version file {StorageKey}. Continuing with deletion.",
                    version.StorageKey);
                failedVersionFiles.Add(version.StorageKey);
            }
        }

        // Delete the main document file from storage
        try
        {
            await _fileStorage.DeleteFileAsync(document.StorageKey);
            _logger.LogDebug("Deleted main document file: {StorageKey}", document.StorageKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete main document file {StorageKey}. Continuing with deletion.",
                document.StorageKey);
        }

        // Permanently remove document from database (cascade delete will handle versions, signatures, downloads, etc.)
        _context.Documents.Remove(document);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Document {DocumentId} permanently deleted by user {UserId}. Deleted {DeletedCount}/{TotalCount} version files. Failed: {FailedCount}",
            documentId, userId, deletedVersionFiles, document.Versions.Count, failedVersionFiles.Count);

        if (failedVersionFiles.Any())
        {
            _logger.LogWarning("Failed to delete the following version files: {FailedFiles}",
                string.Join(", ", failedVersionFiles));
        }
    }

    public async Task<string> GetDocumentDownloadUrlAsync(Guid documentId, Guid userId)
    {
        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document == null)
        {
            throw new KeyNotFoundException($"Document with ID {documentId} not found");
        }

        var hasAccess = await _context.GroupMembers
            .AnyAsync(gm => gm.GroupId == document.GroupId && gm.UserId == userId);

        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("User does not have access to this document");
        }

        return await _fileStorage.GetSecureUrlAsync(document.StorageKey, TimeSpan.FromHours(1));
    }

    public async Task<DocumentDownloadResponse> DownloadDocumentAsync(
        Guid documentId, Guid userId, string ipAddress, string? userAgent)
    {
        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document == null)
        {
            throw new KeyNotFoundException($"Document with ID {documentId} not found");
        }

        var hasAccess = await _context.GroupMembers
            .AnyAsync(gm => gm.GroupId == document.GroupId && gm.UserId == userId);

        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("User does not have access to this document");
        }

        // Track download
        var download = new DocumentDownload
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            UserId = userId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            DownloadedAt = DateTime.UtcNow
        };

        _context.DocumentDownloads.Add(download);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Document {DocumentId} downloaded by user {UserId} from {IpAddress}",
            documentId, userId, ipAddress);

        // Get file stream
        var fileStream = await _fileStorage.DownloadFileAsync(document.StorageKey);

        return new DocumentDownloadResponse
        {
            DocumentId = document.Id,
            FileName = document.FileName,
            FileSize = document.FileSize,
            ContentType = document.ContentType,
            FileStream = fileStream
        };
    }

    public async Task<DocumentDownloadResponse> PreviewDocumentAsync(Guid documentId, Guid userId)
    {
        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document == null)
        {
            throw new KeyNotFoundException($"Document with ID {documentId} not found");
        }

        var hasAccess = await _context.GroupMembers
            .AnyAsync(gm => gm.GroupId == document.GroupId && gm.UserId == userId);

        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("User does not have access to this document");
        }

        // Check if preview is supported
        var previewableTypes = new[] { "application/pdf", "image/jpeg", "image/png" };
        if (!previewableTypes.Contains(document.ContentType.ToLowerInvariant()))
        {
            throw new InvalidOperationException($"Preview not supported for content type: {document.ContentType}");
        }

        // Get file stream (no download tracking for preview)
        var fileStream = await _fileStorage.DownloadFileAsync(document.StorageKey);

        return new DocumentDownloadResponse
        {
            DocumentId = document.Id,
            FileName = document.FileName,
            FileSize = document.FileSize,
            ContentType = document.ContentType,
            FileStream = fileStream
        };
    }

    public async Task<DownloadTrackingInfo> GetDownloadTrackingInfoAsync(Guid documentId, Guid userId)
    {
        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document == null)
        {
            throw new KeyNotFoundException($"Document with ID {documentId} not found");
        }

        var hasAccess = await _context.GroupMembers
            .AnyAsync(gm => gm.GroupId == document.GroupId && gm.UserId == userId);

        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("User does not have access to this document");
        }

        var downloads = await _context.DocumentDownloads
            .Where(d => d.DocumentId == documentId)
            .Include(d => d.User)
            .OrderByDescending(d => d.DownloadedAt)
            .ToListAsync();

        var lastDownload = downloads.FirstOrDefault();

        return new DownloadTrackingInfo
        {
            DocumentId = documentId,
            FileName = document.FileName,
            TotalDownloads = downloads.Count,
            LastDownloadedAt = lastDownload?.DownloadedAt,
            LastDownloadedBy = lastDownload?.User?.Email,
            RecentDownloads = downloads
                .Take(10)
                .Select(d => new DownloadHistoryItem
                {
                    Id = d.Id,
                    UserId = d.UserId,
                    UserName = d.User?.Email ?? "Unknown",
                    DownloadedAt = d.DownloadedAt,
                    IpAddress = d.IpAddress
                })
                .ToList()
        };
    }

    public async Task<SendForSigningResponse> SendForSigningAsync(
        Guid documentId, SendForSigningRequest request, Guid userId, string baseUrl)
    {
        // Validate document exists
        var document = await _context.Documents
            .Include(d => d.Group)
            .ThenInclude(g => g.Members)
            .Include(d => d.Signatures)
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document == null)
        {
            throw new KeyNotFoundException($"Document with ID {documentId} not found");
        }

        // Validate user is document owner or group admin
        var userMembership = await _context.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == document.GroupId && gm.UserId == userId);

        if (userMembership == null)
        {
            throw new UnauthorizedAccessException("User is not a member of this group");
        }

        var isOwnerOrAdmin = document.UploadedBy == userId || userMembership.RoleInGroup == GroupRole.Admin;
        if (!isOwnerOrAdmin)
        {
            throw new UnauthorizedAccessException("Only document owner or group admin can send documents for signing");
        }

        // Validate document is in Draft status
        if (document.SignatureStatus != SignatureStatus.Draft)
        {
            throw new InvalidOperationException(
                $"Document must be in Draft status to send for signing. Current status: {document.SignatureStatus}");
        }

        // Validate all signers are group members
        var groupMemberIds = await _context.GroupMembers
            .Where(gm => gm.GroupId == document.GroupId)
            .Select(gm => gm.UserId)
            .ToListAsync();

        var invalidSigners = request.SignerIds.Except(groupMemberIds).ToList();
        if (invalidSigners.Any())
        {
            throw new ArgumentException(
                $"The following signer IDs are not group members: {string.Join(", ", invalidSigners)}");
        }

        // Validate due date is in the future
        if (request.DueDate.HasValue && request.DueDate.Value <= DateTime.UtcNow)
        {
            throw new ArgumentException("Due date must be in the future");
        }

        // Remove any existing draft signatures
        var existingSignatures = document.Signatures.ToList();
        _context.DocumentSignatures.RemoveRange(existingSignatures);

        // Create DocumentSignature records for each signer
        var signers = new List<SignerInfo>();
        var tokenExpiresAt = DateTime.UtcNow.AddDays(request.TokenExpirationDays);

        for (int i = 0; i < request.SignerIds.Count; i++)
        {
            var signerId = request.SignerIds[i];
            var signerUser = await _context.Users.FindAsync(signerId);

            if (signerUser == null)
            {
                throw new ArgumentException($"Signer with ID {signerId} not found");
            }

            // Generate signing token
            var signingToken = _signingTokenService.GenerateSigningToken(
                documentId, signerId, request.TokenExpirationDays);

            // Generate signing URL
            var signingUrl = _signingTokenService.GenerateSigningUrl(signingToken, baseUrl);

            // Create signature record
            var signature = new DocumentSignature
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                SignerId = signerId,
                SignatureOrder = request.SigningMode == SigningMode.Sequential ? i + 1 : 0,
                Status = SignatureStatus.SentForSigning,
                SigningToken = signingToken,
                TokenExpiresAt = tokenExpiresAt,
                DueDate = request.DueDate,
                Message = request.Message,
                SigningMode = request.SigningMode,
                IsNotificationSent = false, // Will be updated after notification
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.DocumentSignatures.Add(signature);

            signers.Add(new SignerInfo
            {
                SignerId = signerId,
                SignerName = $"{signerUser.FirstName} {signerUser.LastName}",
                SignerEmail = signerUser.Email,
                SignatureOrder = signature.SignatureOrder,
                Status = SignatureStatus.SentForSigning,
                SigningToken = signingToken,
                SigningUrl = signingUrl,
                TokenExpiresAt = tokenExpiresAt,
                NotificationSent = false
            });
        }

        // Update document status to SentForSigning
        document.SignatureStatus = SignatureStatus.SentForSigning;
        document.UpdatedAt = DateTime.UtcNow;

        // Save all changes
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Document {DocumentId} sent for signing by user {UserId}. Signers: {SignerCount}, Mode: {SigningMode}",
            documentId, userId, request.SignerIds.Count, request.SigningMode);

        // TODO: Publish DocumentSentForSigningEvent (if event bus is available)
        // TODO: Send notifications to signers

        // For sequential mode, mark only first signer for notification
        // For parallel mode, mark all signers for notification
        var signersToNotify = request.SigningMode == SigningMode.Sequential
            ? signers.Take(1).ToList()
            : signers;

        foreach (var signer in signersToNotify)
        {
            // Mark as ready for notification
            _logger.LogInformation(
                "Signer {SignerEmail} should be notified for document {DocumentId}",
                signer.SignerEmail, documentId);

            // In production, you would send actual email/notification here
            // For now, we'll just mark it in the response
            signer.NotificationSent = true;
        }

        return new SendForSigningResponse
        {
            DocumentId = document.Id,
            FileName = document.FileName,
            SignatureStatus = document.SignatureStatus,
            SigningMode = request.SigningMode,
            DueDate = request.DueDate,
            TotalSigners = signers.Count,
            Signers = signers,
            SentAt = DateTime.UtcNow
        };
    }

    public async Task<(bool IsValid, string ErrorMessage)> ValidateFileAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return (false, "File is empty or not provided");
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return (false, $"File size exceeds maximum allowed size of {MaxFileSizeBytes / 1024 / 1024}MB");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            return (false, $"File type '{extension}' is not allowed. Allowed types: {string.Join(", ", AllowedExtensions)}");
        }

        if (!AllowedContentTypes.Contains(file.ContentType.ToLowerInvariant()))
        {
            return (false, $"Content type '{file.ContentType}' is not allowed");
        }

        using var stream = file.OpenReadStream();
        var isValidSignature = await ValidateFileSignatureAsync(stream, extension);
        if (!isValidSignature)
        {
            return (false, "File signature does not match the file extension");
        }

        return (true, string.Empty);
    }

    private async Task<bool> ValidateFileSignatureAsync(Stream stream, string extension)
    {
        var buffer = new byte[8];
        await stream.ReadAsync(buffer, 0, buffer.Length);
        stream.Position = 0;

        return extension switch
        {
            ".pdf" => buffer[0] == 0x25 && buffer[1] == 0x50 && buffer[2] == 0x44 && buffer[3] == 0x46,
            ".png" => buffer[0] == 0x89 && buffer[1] == 0x50 && buffer[2] == 0x4E && buffer[3] == 0x47,
            ".jpg" or ".jpeg" => buffer[0] == 0xFF && buffer[1] == 0xD8 && buffer[2] == 0xFF,
            ".docx" => buffer[0] == 0x50 && buffer[1] == 0x4B,
            ".doc" => buffer[0] == 0xD0 && buffer[1] == 0xCF && buffer[2] == 0x11 && buffer[3] == 0xE0,
            _ => true
        };
    }

    private async Task<string> ComputeFileHashAsync(Stream stream)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(stream);
        stream.Position = 0;
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    public async Task<SignDocumentResponse> SignDocumentAsync(
        Guid documentId,
        SignDocumentRequest request,
        Guid userId,
        string ipAddress,
        string? userAgent)
    {
        // Validate signing token
        var (isValid, tokenDocumentId, signerId) =
            _signingTokenService.ValidateSigningToken(request.SigningToken);

        if (!isValid)
        {
            throw new UnauthorizedAccessException("Invalid or expired signing token");
        }

        // Verify the token is for the correct document
        if (tokenDocumentId != documentId)
        {
            throw new UnauthorizedAccessException("Signing token is not valid for this document");
        }

        // Verify the token is for the current user
        if (signerId != userId)
        {
            throw new UnauthorizedAccessException("Signing token does not match current user");
        }

        // Get document with signatures
        var document = await _context.Documents
            .Include(d => d.Signatures)
            .ThenInclude(s => s.Signer)
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document == null)
        {
            throw new KeyNotFoundException($"Document with ID {documentId} not found");
        }

        // Find the signature record for this user
        var signature = await _context.DocumentSignatures
            .Include(s => s.Signer)
            .FirstOrDefaultAsync(s => s.DocumentId == documentId &&
                                     s.SignerId == userId &&
                                     s.SigningToken == request.SigningToken);

        if (signature == null)
        {
            throw new KeyNotFoundException("Signature record not found for this user and token");
        }

        // Check if already signed
        if (signature.SignedAt != null)
        {
            throw new InvalidOperationException("You have already signed this document");
        }

        // Check if token expired
        if (signature.TokenExpiresAt.HasValue && signature.TokenExpiresAt.Value < DateTime.UtcNow)
        {
            throw new UnauthorizedAccessException("Signing token has expired");
        }

        // For sequential signing, validate it's this user's turn
        if (signature.SigningMode == SigningMode.Sequential)
        {
            var allSignatures = await _context.DocumentSignatures
                .Where(s => s.DocumentId == documentId)
                .OrderBy(s => s.SignatureOrder)
                .ToListAsync();

            var previousSignatures = allSignatures
                .Where(s => s.SignatureOrder < signature.SignatureOrder)
                .ToList();

            if (previousSignatures.Any(s => s.SignedAt == null))
            {
                throw new InvalidOperationException(
                    "Cannot sign yet. Previous signers must complete their signatures first (sequential mode)");
            }
        }

        // Validate signature data (not blank, reasonable size)
        if (string.IsNullOrWhiteSpace(request.SignatureData))
        {
            throw new ArgumentException("Signature data cannot be blank");
        }

        // Check signature data size (base64 or SVG should be reasonable)
        if (request.SignatureData.Length > 5 * 1024 * 1024) // 5MB limit for signature data
        {
            throw new ArgumentException("Signature data is too large");
        }

        // Store signature image in file storage
        var signatureFileName = $"signatures/{documentId}/{userId}_{Guid.NewGuid()}.png";
        byte[] signatureBytes;

        try
        {
            // Assume signature data is base64 encoded
            signatureBytes = Convert.FromBase64String(request.SignatureData);
        }
        catch
        {
            // If not base64, store as-is (could be SVG)
            signatureBytes = System.Text.Encoding.UTF8.GetBytes(request.SignatureData);
        }

        using var signatureStream = new MemoryStream(signatureBytes);
        await _fileStorage.UploadFileAsync(signatureStream, signatureFileName, "image/png");

        // Create signature metadata
        var metadata = new SignatureMetadata
        {
            IpAddress = request.IpAddress ?? ipAddress,
            DeviceInfo = request.DeviceInfo ?? "Unknown",
            UserAgent = userAgent ?? "Unknown",
            GpsCoordinates = request.GpsCoordinates,
            SignedAt = DateTime.UtcNow
        };

        var metadataJson = JsonSerializer.Serialize(metadata);

        // Update signature record
        signature.SignedAt = DateTime.UtcNow;
        signature.SignatureReference = signatureFileName;
        signature.SignatureMetadata = metadataJson;
        signature.Status = SignatureStatus.FullySigned; // Individual signature is complete
        signature.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Check if all signatures are collected
        var allDocSignatures = await _context.DocumentSignatures
            .Include(s => s.Signer)
            .Where(s => s.DocumentId == documentId)
            .ToListAsync();

        var allSigned = allDocSignatures.All(s => s.SignedAt != null);
        var signedCount = allDocSignatures.Count(s => s.SignedAt != null);
        var totalSigners = allDocSignatures.Count;

        string? nextSignerName = null;

        if (allSigned)
        {
            // All signatures collected - update document status
            document.SignatureStatus = SignatureStatus.FullySigned;
            document.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Document {DocumentId} is now fully signed. All {SignerCount} signers have completed.",
                documentId, totalSigners);

            // TODO: Publish DocumentFullySignedEvent
            // TODO: Generate and store signing certificate
        }
        else if (signature.SigningMode == SigningMode.Sequential)
        {
            // Sequential mode - notify next signer
            document.SignatureStatus = SignatureStatus.PartiallySigned;
            document.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var nextSignature = allDocSignatures
                .Where(s => s.SignedAt == null)
                .OrderBy(s => s.SignatureOrder)
                .FirstOrDefault();

            if (nextSignature != null)
            {
                nextSignerName = $"{nextSignature.Signer.FirstName} {nextSignature.Signer.LastName}";
                _logger.LogInformation(
                    "Next signer for document {DocumentId} is {SignerName}",
                    documentId, nextSignerName);
                // TODO: Send notification to next signer
            }
        }
        else
        {
            // Parallel mode - update to partially signed
            document.SignatureStatus = SignatureStatus.PartiallySigned;
            document.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Document {DocumentId} partially signed. {SignedCount}/{TotalCount} signatures collected (parallel mode).",
                documentId, signedCount, totalSigners);
            // TODO: Send reminder notifications to pending signers
        }

        // TODO: Publish SignatureProvidedEvent

        var progressPercentage = (double)signedCount / totalSigners * 100;

        return new SignDocumentResponse
        {
            DocumentId = documentId,
            SignatureId = signature.Id,
            FileName = document.FileName,
            DocumentStatus = document.SignatureStatus,
            SignedAt = signature.SignedAt.Value,
            SignerName = $"{signature.Signer.FirstName} {signature.Signer.LastName}",
            TotalSigners = totalSigners,
            SignedCount = signedCount,
            ProgressPercentage = Math.Round(progressPercentage, 2),
            NextSignerName = nextSignerName,
            IsFullySigned = allSigned,
            Message = allSigned
                ? "Document is now fully signed! Certificate can be generated."
                : $"Signature recorded successfully. {totalSigners - signedCount} signature(s) pending."
        };
    }

    public async Task<DocumentSignatureStatusResponse> GetSignatureStatusAsync(Guid documentId, Guid userId)
    {
        // Get document
        var document = await _context.Documents
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

        // Get all signatures
        var signatures = await _context.DocumentSignatures
            .Include(s => s.Signer)
            .Where(s => s.DocumentId == documentId)
            .OrderBy(s => s.SignatureOrder)
            .ToListAsync();

        if (!signatures.Any())
        {
            throw new InvalidOperationException("No signatures configured for this document");
        }

        var signedCount = signatures.Count(s => s.SignedAt != null);
        var totalSigners = signatures.Count;
        var progressPercentage = (double)signedCount / totalSigners * 100;

        // Determine next signer for sequential mode
        var firstSignature = signatures.First();
        string? nextSignerName = null;
        Guid? nextSignerId = null;

        if (firstSignature.SigningMode == SigningMode.Sequential && document.SignatureStatus != SignatureStatus.FullySigned)
        {
            var nextSignature = signatures
                .Where(s => s.SignedAt == null)
                .OrderBy(s => s.SignatureOrder)
                .FirstOrDefault();

            if (nextSignature != null)
            {
                nextSignerName = $"{nextSignature.Signer.FirstName} {nextSignature.Signer.LastName}";
                nextSignerId = nextSignature.SignerId;
            }
        }

        // Calculate time remaining
        TimeSpan? timeRemaining = null;
        var dueDate = signatures.FirstOrDefault()?.DueDate;
        if (dueDate.HasValue && dueDate.Value > DateTime.UtcNow)
        {
            timeRemaining = dueDate.Value - DateTime.UtcNow;
        }

        return new DocumentSignatureStatusResponse
        {
            DocumentId = documentId,
            FileName = document.FileName,
            Status = document.SignatureStatus,
            SigningMode = firstSignature.SigningMode,
            TotalSigners = totalSigners,
            SignedCount = signedCount,
            ProgressPercentage = Math.Round(progressPercentage, 2),
            DueDate = dueDate,
            TimeRemaining = timeRemaining,
            NextSignerName = nextSignerName,
            NextSignerId = nextSignerId,
            Signatures = signatures.Select(s => new SignatureDetailInfo
            {
                Id = s.Id,
                SignerId = s.SignerId,
                SignerName = $"{s.Signer.FirstName} {s.Signer.LastName}",
                SignerEmail = s.Signer.Email,
                SignatureOrder = s.SignatureOrder,
                Status = s.SignedAt != null ? SignatureStatus.FullySigned : SignatureStatus.SentForSigning,
                SignedAt = s.SignedAt,
                SignaturePreviewUrl = s.SignatureReference != null
                    ? $"/api/document/{documentId}/signature/{s.Id}/preview"
                    : null,
                IsPending = s.SignedAt == null,
                IsCurrentSigner = s.SignerId == nextSignerId
            }).ToList(),
            CreatedAt = document.CreatedAt
        };
    }

    public async Task<SigningCertificateResponse> GetSigningCertificateAsync(Guid documentId, Guid userId, string? baseUrl = null)
    {
        // Get document
        var document = await _context.Documents
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

        // Check if document is fully signed
        if (document.SignatureStatus != SignatureStatus.FullySigned)
        {
            throw new InvalidOperationException(
                $"Certificate can only be generated for fully signed documents. Current status: {document.SignatureStatus}");
        }

        // Get all signatures with signer info
        var signatures = await _context.DocumentSignatures
            .Include(s => s.Signer)
            .Where(s => s.DocumentId == documentId && s.SignedAt != null)
            .OrderBy(s => s.SignatureOrder)
            .ToListAsync();

        if (!signatures.Any())
        {
            throw new InvalidOperationException("No signatures found for this document");
        }

        _logger.LogInformation(
            "Generating signing certificate for document {DocumentId} with {SignatureCount} signatures",
            documentId, signatures.Count);

        // Generate certificate using certificate service
        var certificate = await _certificateService.GenerateCertificateAsync(document, signatures, baseUrl);

        return certificate;
    }

    public async Task<CertificateVerificationResult> VerifyCertificateAsync(string certificateId, string? providedHash = null)
    {
        // Find certificate in database
        var certificate = await _context.SigningCertificates
            .Include(c => c.Document)
            .FirstOrDefaultAsync(c => c.CertificateId == certificateId);

        if (certificate == null)
        {
            throw new KeyNotFoundException($"Certificate with ID '{certificateId}' not found");
        }

        // Deserialize signers
        List<CertificateSignerInfo> signers = new();
        if (!string.IsNullOrEmpty(certificate.SignersJson))
        {
            try
            {
                signers = JsonSerializer.Deserialize<List<CertificateSignerInfo>>(certificate.SignersJson) ?? new();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize signers JSON for certificate {CertificateId}", certificateId);
            }
        }

        // Validate hash if provided
        bool hashMatches = true;
        if (!string.IsNullOrEmpty(providedHash))
        {
            hashMatches = providedHash.Equals(certificate.DocumentHash, StringComparison.Ordinal);
        }

        // Check if expired
        bool isExpired = certificate.ExpiresAt.HasValue && DateTime.UtcNow > certificate.ExpiresAt.Value;

        // Determine if valid
        bool isValid = !certificate.IsRevoked && !isExpired && hashMatches;

        _logger.LogInformation(
            "Certificate {CertificateId} verified. Valid: {IsValid}, HashMatches: {HashMatches}, Revoked: {IsRevoked}, Expired: {IsExpired}",
            certificateId, isValid, hashMatches, certificate.IsRevoked, isExpired);

        return new CertificateVerificationResult
        {
            CertificateId = certificate.CertificateId,
            IsValid = isValid,
            HashMatches = hashMatches,
            IsRevoked = certificate.IsRevoked,
            IsExpired = isExpired,
            DocumentName = certificate.FileName,
            DocumentId = certificate.DocumentId,
            TotalSigners = certificate.TotalSigners,
            GeneratedAt = certificate.GeneratedAt,
            ExpiresAt = certificate.ExpiresAt,
            VerifiedAt = DateTime.UtcNow,
            RevocationReason = certificate.RevocationReason,
            Signers = signers,
            VerificationUrl = $"/api/Document/verify-certificate/{certificateId}"
        };
    }

    #region Version Control

    public async Task<DocumentVersionResponse> UploadNewVersionAsync(Guid documentId, IFormFile file, string? changeDescription, Guid userId)
    {
        _logger.LogInformation("Uploading new version for document {DocumentId} by user {UserId}", documentId, userId);

        // Validate file
        var (isValid, errorMessage) = await ValidateFileAsync(file);
        if (!isValid)
        {
            _logger.LogWarning("Invalid file for version upload: {Error}", errorMessage);
            throw new ArgumentException(errorMessage);
        }

        // Get document and verify permissions
        var document = await _context.Documents
            .Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document == null)
        {
            _logger.LogWarning("Document {DocumentId} not found", documentId);
            throw new KeyNotFoundException($"Document with ID {documentId} not found");
        }

        // Check if user is group member separately to avoid Include issues
        var groupMember = await _context.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == document.GroupId && gm.UserId == userId);

        if (groupMember == null)
        {
            _logger.LogWarning("User {UserId} is not a member of group {GroupId}", userId, document.GroupId);
            throw new UnauthorizedAccessException("You must be a group member to upload new versions");
        }

        if (groupMember.RoleInGroup != GroupRole.Admin && document.UploadedBy != userId)
        {
            _logger.LogWarning("User {UserId} lacks permission to upload new version for document {DocumentId}", userId, documentId);
            throw new UnauthorizedAccessException("Only group admins or the original uploader can upload new versions");
        }

        // Calculate hash and upload file
        string fileHash;
        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        using (var hashStream = new MemoryStream(memoryStream.ToArray()))
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashBytes = await sha256.ComputeHashAsync(hashStream);
            fileHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }

        // Generate storage key
        var fileExtension = Path.GetExtension(file.FileName);
        var storageKey = $"documents/{document.GroupId}/versions/{documentId}_{Guid.NewGuid()}{fileExtension}";

        // Upload to storage
        memoryStream.Position = 0;
        await _fileStorage.UploadFileAsync(memoryStream, storageKey, file.ContentType);

        // If this is the first version being uploaded, create version 0 for the original document first
        if (!document.Versions.Any())
        {
            _logger.LogInformation("Creating version 0 for original document {DocumentId}", documentId);

            var version0 = new DocumentVersion
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                VersionNumber = 0,
                StorageKey = document.StorageKey,
                FileName = document.FileName,
                FileSize = document.FileSize,
                ContentType = document.ContentType,
                FileHash = document.FileHash,
                UploadedBy = document.UploadedBy ?? userId,
                UploadedAt = document.CreatedAt,
                ChangeDescription = "Original document",
                IsCurrent = false, // Will be false since we're uploading a new version
                CreatedAt = document.CreatedAt,
                UpdatedAt = document.CreatedAt
            };

            _context.DocumentVersions.Add(version0);
        }

        // Get next version number
        var maxVersion = document.Versions.Any() ? document.Versions.Max(v => v.VersionNumber) : 0;
        var newVersionNumber = maxVersion + 1;

        // Mark all existing versions as not current
        foreach (var version in document.Versions.Where(v => v.IsCurrent))
        {
            version.IsCurrent = false;
        }

        // Create new version
        var newVersion = new DocumentVersion
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            VersionNumber = newVersionNumber,
            StorageKey = storageKey,
            FileName = file.FileName,
            FileSize = file.Length,
            ContentType = file.ContentType,
            FileHash = fileHash,
            UploadedBy = userId,
            UploadedAt = DateTime.UtcNow,
            ChangeDescription = changeDescription,
            IsCurrent = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.DocumentVersions.Add(newVersion);

        // Update document with new version info
        document.StorageKey = storageKey;
        document.FileName = file.FileName;
        document.FileSize = file.Length;
        document.ContentType = file.ContentType;
        document.FileHash = fileHash;
        document.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "New version {VersionNumber} created for document {DocumentId}",
            newVersionNumber, documentId);

        // TODO: Publish DocumentVersionUpdatedEvent

        // Get uploader name
        var uploader = await _context.Users.FindAsync(userId);
        var uploaderName = uploader != null ? $"{uploader.FirstName} {uploader.LastName}" : "Unknown";

        return new DocumentVersionResponse
        {
            Id = newVersion.Id,
            DocumentId = newVersion.DocumentId,
            VersionNumber = newVersion.VersionNumber,
            FileName = newVersion.FileName,
            FileSize = newVersion.FileSize,
            ContentType = newVersion.ContentType,
            FileHash = newVersion.FileHash,
            UploadedBy = newVersion.UploadedBy,
            UploaderName = uploaderName,
            UploadedAt = newVersion.UploadedAt,
            ChangeDescription = newVersion.ChangeDescription,
            IsCurrent = newVersion.IsCurrent
        };
    }

    public async Task<DocumentVersionListResponse> GetDocumentVersionsAsync(Guid documentId, Guid userId)
    {
        _logger.LogInformation("Getting versions for document {DocumentId} by user {UserId}", documentId, userId);

        // Get document and verify permissions
        var document = await _context.Documents
            .Include(d => d.Versions)
                .ThenInclude(v => v.Uploader)
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document == null)
        {
            _logger.LogWarning("Document {DocumentId} not found", documentId);
            throw new KeyNotFoundException($"Document with ID {documentId} not found");
        }

        // Check if user is group member separately to avoid Include issues
        var isGroupMember = await _context.GroupMembers
            .AnyAsync(gm => gm.GroupId == document.GroupId && gm.UserId == userId);

        if (!isGroupMember)
        {
            _logger.LogWarning("User {UserId} is not a member of group {GroupId}", userId, document.GroupId);
            throw new UnauthorizedAccessException("You must be a group member to view document versions");
        }

        var versions = document.Versions
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new DocumentVersionResponse
            {
                Id = v.Id,
                DocumentId = v.DocumentId,
                VersionNumber = v.VersionNumber,
                FileName = v.FileName,
                FileSize = v.FileSize,
                ContentType = v.ContentType,
                FileHash = v.FileHash,
                UploadedBy = v.UploadedBy,
                UploaderName = $"{v.Uploader.FirstName} {v.Uploader.LastName}",
                UploadedAt = v.UploadedAt,
                ChangeDescription = v.ChangeDescription,
                IsCurrent = v.IsCurrent
            })
            .ToList();

        // If version 0 doesn't exist but there are other versions, add the original document as version 0
        // This handles backwards compatibility for documents uploaded before version tracking
        if (!versions.Any(v => v.VersionNumber == 0) && versions.Any())
        {
            var uploader = await _context.Users.FindAsync(document.UploadedBy);
            var uploaderName = uploader != null ? $"{uploader.FirstName} {uploader.LastName}" : "Unknown";

            var version0 = new DocumentVersionResponse
            {
                Id = Guid.Empty, // Indicates this is a virtual version, not stored in DB
                DocumentId = document.Id,
                VersionNumber = 0,
                FileName = document.FileName,
                FileSize = document.FileSize,
                ContentType = document.ContentType,
                FileHash = document.FileHash,
                UploadedBy = document.UploadedBy ?? Guid.Empty,
                UploaderName = uploaderName,
                UploadedAt = document.CreatedAt,
                ChangeDescription = "Original document",
                IsCurrent = false
            };

            versions.Add(version0);
            versions = versions.OrderByDescending(v => v.VersionNumber).ToList();
        }

        return new DocumentVersionListResponse
        {
            DocumentId = document.Id,
            DocumentName = document.FileName,
            TotalVersions = versions.Count,
            Versions = versions
        };
    }

    public async Task<DocumentDownloadResponse> DownloadVersionAsync(Guid versionId, Guid userId)
    {
        _logger.LogInformation("Downloading version {VersionId} by user {UserId}", versionId, userId);

        // Get version with document
        var version = await _context.DocumentVersions
            .Include(v => v.Document)
            .FirstOrDefaultAsync(v => v.Id == versionId);

        if (version == null)
        {
            _logger.LogWarning("Version {VersionId} not found", versionId);
            throw new KeyNotFoundException($"Document version with ID {versionId} not found");
        }

        // Check if user is group member separately to avoid Include issues
        var isGroupMember = await _context.GroupMembers
            .AnyAsync(gm => gm.GroupId == version.Document.GroupId && gm.UserId == userId);

        if (!isGroupMember)
        {
            _logger.LogWarning("User {UserId} is not a member of group {GroupId}", userId, version.Document.GroupId);
            throw new UnauthorizedAccessException("You must be a group member to download document versions");
        }

        // Get file stream from storage service
        var fileStream = await _fileStorage.DownloadFileAsync(version.StorageKey);
        if (fileStream == null)
        {
            _logger.LogError("Failed to download file for version {VersionId}", versionId);
            throw new InvalidOperationException("Failed to download file");
        }

        _logger.LogInformation("Version {VersionId} downloaded for user {UserId}", versionId, userId);

        return new DocumentDownloadResponse
        {
            DocumentId = version.DocumentId,
            FileName = version.FileName,
            FileSize = version.FileSize,
            ContentType = version.ContentType,
            FileStream = fileStream
        };
    }

    public async Task DeleteVersionAsync(Guid versionId, Guid userId)
    {
        _logger.LogInformation("Deleting version {VersionId} by user {UserId}", versionId, userId);

        // Get version with document
        var version = await _context.DocumentVersions
            .Include(v => v.Document)
            .FirstOrDefaultAsync(v => v.Id == versionId);

        if (version == null)
        {
            _logger.LogWarning("Version {VersionId} not found", versionId);
            throw new KeyNotFoundException($"Document version with ID {versionId} not found");
        }

        // Check if user is group admin or original uploader
        var groupMember = await _context.GroupMembers
            .FirstOrDefaultAsync(gm => gm.GroupId == version.Document.GroupId && gm.UserId == userId);

        if (groupMember == null)
        {
            _logger.LogWarning("User {UserId} is not a member of group {GroupId}", userId, version.Document.GroupId);
            throw new UnauthorizedAccessException("You must be a group member to delete document versions");
        }

        if (groupMember.RoleInGroup != GroupRole.Admin && version.Document.UploadedBy != userId)
        {
            _logger.LogWarning("User {UserId} lacks permission to delete version {VersionId}", userId, versionId);
            throw new UnauthorizedAccessException("Only group admins or the original document uploader can delete versions");
        }

        // Prevent deletion of the current version (version being used by the document)
        if (version.IsCurrent)
        {
            _logger.LogWarning("Attempt to delete current version {VersionId}", versionId);
            throw new InvalidOperationException("Cannot delete the current version. Please upload a new version first or delete the entire document.");
        }

        // Prevent deletion if it's the only version (version 0)
        var totalVersions = await _context.DocumentVersions
            .CountAsync(v => v.DocumentId == version.DocumentId);

        if (totalVersions == 1)
        {
            _logger.LogWarning("Attempt to delete the only version {VersionId}", versionId);
            throw new InvalidOperationException("Cannot delete the only version. Delete the entire document instead.");
        }

        // Delete file from storage
        try
        {
            await _fileStorage.DeleteFileAsync(version.StorageKey);
            _logger.LogDebug("Deleted version file: {StorageKey}", version.StorageKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete version file {StorageKey}. Continuing with database deletion.",
                version.StorageKey);
        }

        // Remove version from database
        _context.DocumentVersions.Remove(version);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Version {VersionId} (version number {VersionNumber}) deleted successfully by user {UserId}",
            versionId, version.VersionNumber, userId);
    }

    #endregion

    #region Notifications & Reminders

    public async Task<SendReminderResponse> SendManualReminderAsync(
        Guid documentId,
        SendReminderRequest request,
        Guid userId,
        string baseUrl)
    {
        _logger.LogInformation("Sending manual reminder for document {DocumentId} by user {UserId}", documentId, userId);

        // Get document with signatures
        var document = await _context.Documents
            .Include(d => d.Signatures)
                .ThenInclude(s => s.Signer)
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document == null)
        {
            throw new KeyNotFoundException($"Document with ID {documentId} not found");
        }

        // Check if user is document owner or group admin
        var isAdmin = await _context.GroupMembers
            .AnyAsync(gm => gm.GroupId == document.GroupId &&
                           gm.UserId == userId &&
                           gm.RoleInGroup == GroupRole.Admin);

        if (document.UploadedBy != userId && !isAdmin)
        {
            throw new UnauthorizedAccessException("Only document owner or group admins can send reminders");
        }

        // Get pending signatures
        var pendingSignatures = document.Signatures
            .Where(s => s.Status == SignatureStatus.SentForSigning)
            .ToList();

        if (!pendingSignatures.Any())
        {
            throw new InvalidOperationException("No pending signatures found for this document");
        }

        // Filter by specific signers if requested
        if (request.SpecificSignerIds != null && request.SpecificSignerIds.Any())
        {
            pendingSignatures = pendingSignatures
                .Where(s => request.SpecificSignerIds.Contains(s.SignerId))
                .ToList();
        }

        var recipients = new List<ReminderRecipient>();
        var remindersSent = 0;

        foreach (var signature in pendingSignatures)
        {
            var signingUrl = $"{baseUrl}/api/document/{documentId}/sign?token={signature.SigningToken}";

            // Send reminder using notification service
            var success = await _notificationService.SendSignatureReminderAsync(
                signature.Signer,
                document,
                signingUrl,
                ReminderType.Manual,
                request.CustomMessage);

            // Record reminder in database
            var reminder = new SignatureReminder
            {
                Id = Guid.NewGuid(),
                DocumentSignatureId = signature.Id,
                ReminderType = ReminderType.Manual,
                SentAt = DateTime.UtcNow,
                SentBy = userId,
                IsManual = true,
                Message = request.CustomMessage,
                Status = success ? ReminderDeliveryStatus.Sent : ReminderDeliveryStatus.Failed,
                DeliveredAt = success ? DateTime.UtcNow : null,
                ErrorMessage = success ? null : "Failed to send email",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.SignatureReminders.Add(reminder);

            recipients.Add(new ReminderRecipient
            {
                SignerId = signature.SignerId,
                SignerName = $"{signature.Signer.FirstName} {signature.Signer.LastName}",
                SignerEmail = signature.Signer.Email,
                Sent = success,
                ErrorMessage = success ? null : "Failed to send email"
            });

            if (success) remindersSent++;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Manual reminders sent for document {DocumentId}. Sent: {Sent}/{Total}",
            documentId, remindersSent, pendingSignatures.Count);

        return new SendReminderResponse
        {
            DocumentId = documentId,
            FileName = document.FileName,
            RemindersSent = remindersSent,
            Recipients = recipients,
            SentAt = DateTime.UtcNow
        };
    }

    public async Task<ReminderHistoryResponse> GetReminderHistoryAsync(Guid documentId, Guid userId)
    {
        _logger.LogInformation("Getting reminder history for document {DocumentId}", documentId);

        // Get document
        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document == null)
        {
            throw new KeyNotFoundException($"Document with ID {documentId} not found");
        }

        // Check if user is group member
        var isGroupMember = await _context.GroupMembers
            .AnyAsync(gm => gm.GroupId == document.GroupId && gm.UserId == userId);

        if (!isGroupMember)
        {
            throw new UnauthorizedAccessException("You must be a group member to view reminder history");
        }

        // Get all reminders for this document
        var reminders = await _context.SignatureReminders
            .Include(r => r.DocumentSignature)
                .ThenInclude(s => s.Signer)
            .Where(r => r.DocumentSignature.DocumentId == documentId)
            .OrderByDescending(r => r.SentAt)
            .ToListAsync();

        var reminderItems = reminders.Select(r => new ReminderHistoryItem
        {
            Id = r.Id,
            SignerId = r.DocumentSignature.SignerId,
            SignerName = $"{r.DocumentSignature.Signer.FirstName} {r.DocumentSignature.Signer.LastName}",
            ReminderType = r.ReminderType,
            SentAt = r.SentAt,
            IsManual = r.IsManual,
            Status = r.Status,
            Message = r.Message
        }).ToList();

        return new ReminderHistoryResponse
        {
            DocumentId = documentId,
            FileName = document.FileName,
            TotalReminders = reminderItems.Count,
            Reminders = reminderItems
        };
    }

    public async Task<bool> SendTestEmailAsync(string email, string subject, string message)
    {
        _logger.LogInformation("Sending test email to {Email}", email);

        try
        {
            // Create a test email body
            var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #2196F3; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
        .content {{ background-color: #f9f9f9; padding: 20px; border-radius: 0 0 5px 5px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h2>🧪 Test Email</h2>
        </div>
        <div class='content'>
            <p><strong>Subject:</strong> {subject}</p>
            <p><strong>Message:</strong></p>
            <p>{message}</p>
            <hr>
            <p><em>This is a test email from the Co-Ownership Vehicle System.</em></p>
            <p><em>If you received this email, your email configuration is working correctly!</em></p>
            <p><strong>Sent at:</strong> {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>
        </div>
    </div>
</body>
</html>";

            // Use the notification service to send email
            // Create a mock user and document for testing
            var testUser = new User
            {
                Email = email,
                FirstName = "Test",
                LastName = "User"
            };

            var testDocument = new Document
            {
                FileName = "Test Document.pdf",
                Id = Guid.NewGuid()
            };

            // For testing, we'll directly send through notification service
            // In production, you might want to create a more generic SendEmail method
            return await _notificationService.SendSignatureReminderAsync(
                testUser,
                testDocument,
                "https://example.com/test",
                ReminderType.Manual,
                message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send test email to {Email}", email);
            return false;
        }
    }

    #endregion
}