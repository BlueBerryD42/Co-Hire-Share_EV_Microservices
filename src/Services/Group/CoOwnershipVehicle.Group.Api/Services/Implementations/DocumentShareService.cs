using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Group.Api.Data;
using CoOwnershipVehicle.Group.Api.DTOs;
using CoOwnershipVehicle.Group.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using BCrypt.Net;

namespace CoOwnershipVehicle.Group.Api.Services.Implementations;

public class DocumentShareService : IDocumentShareService
{
    private readonly GroupDbContext _context;
    private readonly IFileStorageService _fileStorage;
    private readonly INotificationService _notificationService;
    private readonly ILogger<DocumentShareService> _logger;

    public DocumentShareService(
        GroupDbContext context,
        IFileStorageService fileStorage,
        INotificationService notificationService,
        ILogger<DocumentShareService> logger)
    {
        _context = context;
        _fileStorage = fileStorage;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<CreateShareResponse> CreateShareAsync(
        Guid documentId, CreateShareRequest request, Guid userId, string baseUrl)
    {
        _logger.LogInformation("Creating share for document {DocumentId} by user {UserId}", documentId, userId);

        // Get document and verify access
        var document = await _context.Documents
            .Include(d => d.Group)
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

        // Generate unique share token
        var shareToken = Guid.NewGuid().ToString("N");

        // Hash password if provided
        string? passwordHash = null;
        if (!string.IsNullOrEmpty(request.Password))
        {
            passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        }

        // Create share
        var share = new DocumentShare
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            ShareToken = shareToken,
            SharedBy = userId,
            SharedWith = request.SharedWith,
            RecipientEmail = request.RecipientEmail,
            Permissions = request.Permissions,
            ExpiresAt = request.ExpiresAt,
            Message = request.Message,
            PasswordHash = passwordHash,
            MaxAccessCount = request.MaxAccessCount,
            AccessCount = 0,
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.DocumentShares.Add(share);
        await _context.SaveChangesAsync();

        // Generate share URL
        var shareUrl = $"{baseUrl}/api/document/shared/{shareToken}";

        // Send email notification if email provided
        bool emailSent = false;
        if (!string.IsNullOrEmpty(request.RecipientEmail))
        {
            try
            {
                emailSent = await SendShareNotificationAsync(share, document, shareUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send share notification email");
            }
        }

        _logger.LogInformation("Share {ShareId} created with token {Token}", share.Id, shareToken);

        return new CreateShareResponse
        {
            ShareId = share.Id,
            ShareToken = shareToken,
            ShareUrl = shareUrl,
            ExpiresAt = share.ExpiresAt,
            EmailSent = emailSent
        };
    }

    public async Task<SharedDocumentResponse> GetSharedDocumentAsync(
        string shareToken, AccessSharedDocumentRequest? request = null,
        string? ipAddress = null, string? userAgent = null)
    {
        _logger.LogInformation("Accessing shared document with token {Token}", shareToken);

        var share = await _context.DocumentShares
            .Include(s => s.Document)
            .Include(s => s.Sharer)
            .FirstOrDefaultAsync(s => s.ShareToken == shareToken);

        if (share == null)
        {
            await LogAccessAttemptAsync(Guid.Empty, shareToken, ShareAccessAction.Viewed,
                false, "Invalid token", ipAddress, userAgent);
            throw new KeyNotFoundException("Share link not found or invalid");
        }

        // Validate share
        var (isValid, errorMessage) = await ValidateShareAsync(share, request?.Password);
        if (!isValid)
        {
            await LogAccessAttemptAsync(share.Id, shareToken, ShareAccessAction.Viewed,
                false, errorMessage, ipAddress, userAgent);
            throw new UnauthorizedAccessException(errorMessage ?? "Access denied");
        }

        // Log successful access
        await LogAccessAttemptAsync(share.Id, shareToken, ShareAccessAction.Viewed,
            true, null, ipAddress, userAgent);

        // Update access statistics
        share.AccessCount++;
        if (share.FirstAccessedAt == null)
        {
            share.FirstAccessedAt = DateTime.UtcNow;
        }
        share.LastAccessedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return new SharedDocumentResponse
        {
            DocumentId = share.Document.Id,
            FileName = share.Document.FileName,
            FileSize = share.Document.FileSize,
            ContentType = share.Document.ContentType,
            AvailablePermissions = share.Permissions,
            Message = share.Message,
            SharedByName = $"{share.Sharer.FirstName} {share.Sharer.LastName}",
            SharedAt = share.CreatedAt,
            ExpiresAt = share.ExpiresAt,
            RequiresPassword = !string.IsNullOrEmpty(share.PasswordHash),
            AccessCount = share.AccessCount,
            MaxAccessCount = share.MaxAccessCount
        };
    }

    public async Task<DocumentDownloadResponse> DownloadSharedDocumentAsync(
        string shareToken, string? password = null, string? ipAddress = null, string? userAgent = null)
    {
        _logger.LogInformation("Downloading shared document with token {Token}", shareToken);

        var share = await _context.DocumentShares
            .Include(s => s.Document)
            .FirstOrDefaultAsync(s => s.ShareToken == shareToken);

        if (share == null)
        {
            throw new KeyNotFoundException("Share link not found or invalid");
        }

        // Validate share
        var (isValid, errorMessage) = await ValidateShareAsync(share, password);
        if (!isValid)
        {
            await LogAccessAttemptAsync(share.Id, shareToken, ShareAccessAction.Downloaded,
                false, errorMessage, ipAddress, userAgent);
            throw new UnauthorizedAccessException(errorMessage ?? "Access denied");
        }

        // Check download permission
        if (!share.Permissions.HasFlag(SharePermissions.Download))
        {
            await LogAccessAttemptAsync(share.Id, shareToken, ShareAccessAction.Downloaded,
                false, "Download not permitted", ipAddress, userAgent);
            throw new UnauthorizedAccessException("Download permission not granted for this share");
        }

        // Log successful download
        await LogAccessAttemptAsync(share.Id, shareToken, ShareAccessAction.Downloaded,
            true, null, ipAddress, userAgent);

        // Update access count
        share.AccessCount++;
        share.LastAccessedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Get file stream
        var fileStream = await _fileStorage.DownloadFileAsync(share.Document.StorageKey);

        return new DocumentDownloadResponse
        {
            DocumentId = share.Document.Id,
            FileName = share.Document.FileName,
            FileSize = share.Document.FileSize,
            ContentType = share.Document.ContentType,
            FileStream = fileStream
        };
    }

    public async Task<DocumentDownloadResponse> PreviewSharedDocumentAsync(
        string shareToken, string? password = null, string? ipAddress = null, string? userAgent = null)
    {
        _logger.LogInformation("Previewing shared document with token {Token}", shareToken);

        var share = await _context.DocumentShares
            .Include(s => s.Document)
            .FirstOrDefaultAsync(s => s.ShareToken == shareToken);

        if (share == null)
        {
            throw new KeyNotFoundException("Share link not found or invalid");
        }

        // Validate share
        var (isValid, errorMessage) = await ValidateShareAsync(share, password);
        if (!isValid)
        {
            throw new UnauthorizedAccessException(errorMessage ?? "Access denied");
        }

        // Check view permission
        if (!share.Permissions.HasFlag(SharePermissions.View))
        {
            throw new UnauthorizedAccessException("View permission not granted for this share");
        }

        // Check if content type is previewable
        var previewableTypes = new[] { "application/pdf", "image/jpeg", "image/png", "image/jpg" };
        if (!previewableTypes.Contains(share.Document.ContentType.ToLowerInvariant()))
        {
            throw new InvalidOperationException($"Preview not supported for content type: {share.Document.ContentType}");
        }

        // Get file stream (no tracking for preview)
        var fileStream = await _fileStorage.DownloadFileAsync(share.Document.StorageKey);

        return new DocumentDownloadResponse
        {
            DocumentId = share.Document.Id,
            FileName = share.Document.FileName,
            FileSize = share.Document.FileSize,
            ContentType = share.Document.ContentType,
            FileStream = fileStream
        };
    }

    public async Task<DocumentShareListResponse> GetDocumentSharesAsync(Guid documentId, Guid userId)
    {
        _logger.LogInformation("Getting shares for document {DocumentId}", documentId);

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

        var shares = await _context.DocumentShares
            .Where(s => s.DocumentId == documentId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        var shareSummaries = shares.Select(s => new ShareSummary
        {
            ShareId = s.Id,
            ShareToken = s.ShareToken,
            SharedWith = s.SharedWith,
            Permissions = s.Permissions,
            CreatedAt = s.CreatedAt,
            ExpiresAt = s.ExpiresAt,
            IsRevoked = s.IsRevoked,
            IsExpired = s.ExpiresAt.HasValue && DateTime.UtcNow > s.ExpiresAt.Value,
            AccessCount = s.AccessCount,
            LastAccessedAt = s.LastAccessedAt
        }).ToList();

        return new DocumentShareListResponse
        {
            DocumentId = documentId,
            FileName = document.FileName,
            Shares = shareSummaries,
            TotalShares = shares.Count,
            ActiveShares = shareSummaries.Count(s => !s.IsRevoked && !s.IsExpired),
            RevokedShares = shareSummaries.Count(s => s.IsRevoked),
            ExpiredShares = shareSummaries.Count(s => s.IsExpired && !s.IsRevoked)
        };
    }

    public async Task<ShareAnalyticsResponse> GetShareAnalyticsAsync(Guid shareId, Guid userId)
    {
        _logger.LogInformation("Getting analytics for share {ShareId}", shareId);

        var share = await _context.DocumentShares
            .Include(s => s.Document)
            .Include(s => s.AccessLog)
            .FirstOrDefaultAsync(s => s.Id == shareId);

        if (share == null)
        {
            throw new KeyNotFoundException($"Share with ID {shareId} not found");
        }

        // Verify user has access to the document
        var hasAccess = await _context.GroupMembers
            .AnyAsync(gm => gm.GroupId == share.Document.GroupId && gm.UserId == userId);

        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("User does not have access to this document");
        }

        var accessLog = share.AccessLog
            .OrderByDescending(a => a.AccessedAt)
            .Take(50)
            .Select(a => new ShareAccessLogEntry
            {
                AccessedAt = a.AccessedAt,
                IpAddress = a.IpAddress,
                UserAgent = a.UserAgent,
                Action = a.Action,
                WasSuccessful = a.WasSuccessful,
                FailureReason = a.FailureReason
            })
            .ToList();

        return new ShareAnalyticsResponse
        {
            ShareId = share.Id,
            ShareToken = share.ShareToken,
            SharedWith = share.SharedWith,
            Permissions = share.Permissions,
            CreatedAt = share.CreatedAt,
            ExpiresAt = share.ExpiresAt,
            IsRevoked = share.IsRevoked,
            RevokedAt = share.RevokedAt,
            AccessCount = share.AccessCount,
            MaxAccessCount = share.MaxAccessCount,
            FirstAccessedAt = share.FirstAccessedAt,
            LastAccessedAt = share.LastAccessedAt,
            AccessLog = accessLog
        };
    }

    public async Task RevokeShareAsync(Guid documentId, Guid shareId, Guid userId)
    {
        _logger.LogInformation("Revoking share {ShareId} for document {DocumentId}", shareId, documentId);

        var share = await _context.DocumentShares
            .Include(s => s.Document)
            .FirstOrDefaultAsync(s => s.Id == shareId && s.DocumentId == documentId);

        if (share == null)
        {
            throw new KeyNotFoundException($"Share with ID {shareId} not found");
        }

        // Verify user has access
        var hasAccess = await _context.GroupMembers
            .AnyAsync(gm => gm.GroupId == share.Document.GroupId && gm.UserId == userId);

        if (!hasAccess && share.SharedBy != userId)
        {
            throw new UnauthorizedAccessException("Only the sharer or group members can revoke shares");
        }

        share.IsRevoked = true;
        share.RevokedAt = DateTime.UtcNow;
        share.RevokedBy = userId;
        share.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Share {ShareId} revoked successfully", shareId);
    }

    public async Task UpdateShareExpirationAsync(Guid shareId, DateTime? expiresAt, Guid userId)
    {
        _logger.LogInformation("Updating expiration for share {ShareId}", shareId);

        var share = await _context.DocumentShares
            .Include(s => s.Document)
            .FirstOrDefaultAsync(s => s.Id == shareId);

        if (share == null)
        {
            throw new KeyNotFoundException($"Share with ID {shareId} not found");
        }

        // Verify user has access
        var hasAccess = await _context.GroupMembers
            .AnyAsync(gm => gm.GroupId == share.Document.GroupId && gm.UserId == userId);

        if (!hasAccess && share.SharedBy != userId)
        {
            throw new UnauthorizedAccessException("Only the sharer or group members can update share expiration");
        }

        share.ExpiresAt = expiresAt;
        share.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Share {ShareId} expiration updated to {ExpiresAt}", shareId, expiresAt);
    }

    public async Task<(bool IsValid, string? ErrorMessage)> ValidateShareTokenAsync(
        string shareToken, string? password = null)
    {
        var share = await _context.DocumentShares
            .FirstOrDefaultAsync(s => s.ShareToken == shareToken);

        if (share == null)
        {
            return (false, "Invalid share token");
        }

        return await ValidateShareAsync(share, password);
    }

    #region Helper Methods

    private async Task<(bool IsValid, string? ErrorMessage)> ValidateShareAsync(
        DocumentShare share, string? password)
    {
        // Check if revoked
        if (share.IsRevoked)
        {
            return (false, "This share link has been revoked");
        }

        // Check expiration
        if (share.ExpiresAt.HasValue && DateTime.UtcNow > share.ExpiresAt.Value)
        {
            return (false, "This share link has expired");
        }

        // Check max access count
        if (share.MaxAccessCount.HasValue && share.AccessCount >= share.MaxAccessCount.Value)
        {
            return (false, "Maximum access count reached for this share link");
        }

        // Check password
        if (!string.IsNullOrEmpty(share.PasswordHash))
        {
            if (string.IsNullOrEmpty(password))
            {
                return (false, "Password required to access this share");
            }

            if (!BCrypt.Net.BCrypt.Verify(password, share.PasswordHash))
            {
                return (false, "Incorrect password");
            }
        }

        return (true, null);
    }

    private async Task LogAccessAttemptAsync(
        Guid shareId, string shareToken, ShareAccessAction action,
        bool wasSuccessful, string? failureReason,
        string? ipAddress, string? userAgent)
    {
        // Only log if we have a valid share ID
        if (shareId == Guid.Empty) return;

        var accessLog = new DocumentShareAccess
        {
            Id = Guid.NewGuid(),
            DocumentShareId = shareId,
            AccessedAt = DateTime.UtcNow,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Action = action,
            WasSuccessful = wasSuccessful,
            FailureReason = failureReason,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.DocumentShareAccesses.Add(accessLog);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log share access attempt");
        }
    }

    private async Task<bool> SendShareNotificationAsync(
        DocumentShare share, Document document, string shareUrl)
    {
        try
        {
            _logger.LogInformation("Sending share notification to {Email}", share.RecipientEmail);

            // Create a notification user object
            var recipient = new User
            {
                Email = share.RecipientEmail!,
                FirstName = share.SharedWith,
                LastName = ""
            };

            // Get sharer details
            var sharer = await _context.Users.FindAsync(share.SharedBy);
            if (sharer == null) return false;

            // Build email content
            var subject = $"Document shared with you: {document.FileName}";
            var message = $@"
Hello {share.SharedWith},

{sharer.FirstName} {sharer.LastName} has shared a document with you:

Document: {document.FileName}
{(share.Message != null ? $"Message: {share.Message}" : "")}

Permissions: {share.Permissions}
{(share.ExpiresAt.HasValue ? $"Expires: {share.ExpiresAt.Value:yyyy-MM-dd HH:mm}" : "No expiration")}

Access the document here:
{shareUrl}

{(!string.IsNullOrEmpty(share.PasswordHash) ? "This link is password-protected." : "")}

Best regards,
Co-Ownership Vehicle System
";

            // Use notification service to send email
            // Note: This is a simplified version - you may need to create a dedicated method
            // in INotificationService for share notifications
            return await _notificationService.SendSignatureReminderAsync(
                recipient, document, shareUrl, ReminderType.Manual, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send share notification");
            return false;
        }
    }

    #endregion
}
