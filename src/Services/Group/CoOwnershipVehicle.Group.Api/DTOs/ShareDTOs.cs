using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Group.Api.DTOs;

/// <summary>
/// Request to share a document
/// </summary>
public class CreateShareRequest
{
    public string SharedWith { get; set; } = string.Empty;
    public string? RecipientEmail { get; set; }
    public SharePermissions Permissions { get; set; } = SharePermissions.View;
    public DateTime? ExpiresAt { get; set; }
    public string? Message { get; set; }
    public string? Password { get; set; }
    public int? MaxAccessCount { get; set; }
}

/// <summary>
/// Response after creating a share
/// </summary>
public class CreateShareResponse
{
    public Guid ShareId { get; set; }
    public string ShareToken { get; set; } = string.Empty;
    public string ShareUrl { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; }
    public bool EmailSent { get; set; }
}

/// <summary>
/// Access shared document request
/// </summary>
public class AccessSharedDocumentRequest
{
    public string? Password { get; set; }
}

/// <summary>
/// Response for shared document access
/// </summary>
public class SharedDocumentResponse
{
    public Guid DocumentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public SharePermissions AvailablePermissions { get; set; }
    public string? Message { get; set; }
    public string SharedByName { get; set; } = string.Empty;
    public DateTime SharedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool RequiresPassword { get; set; }
    public int AccessCount { get; set; }
    public int? MaxAccessCount { get; set; }
}

/// <summary>
/// Share analytics response
/// </summary>
public class ShareAnalyticsResponse
{
    public Guid ShareId { get; set; }
    public string ShareToken { get; set; } = string.Empty;
    public string SharedWith { get; set; } = string.Empty;
    public SharePermissions Permissions { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime? RevokedAt { get; set; }
    public int AccessCount { get; set; }
    public int? MaxAccessCount { get; set; }
    public DateTime? FirstAccessedAt { get; set; }
    public DateTime? LastAccessedAt { get; set; }
    public List<ShareAccessLogEntry> AccessLog { get; set; } = new();
}

/// <summary>
/// Share access log entry
/// </summary>
public class ShareAccessLogEntry
{
    public DateTime AccessedAt { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public ShareAccessAction Action { get; set; }
    public bool WasSuccessful { get; set; }
    public string? FailureReason { get; set; }
}

/// <summary>
/// List of shares for a document
/// </summary>
public class DocumentShareListResponse
{
    public Guid DocumentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public List<ShareSummary> Shares { get; set; } = new();
    public int TotalShares { get; set; }
    public int ActiveShares { get; set; }
    public int RevokedShares { get; set; }
    public int ExpiredShares { get; set; }
}

/// <summary>
/// Share summary
/// </summary>
public class ShareSummary
{
    public Guid ShareId { get; set; }
    public string ShareToken { get; set; } = string.Empty;
    public string SharedWith { get; set; } = string.Empty;
    public SharePermissions Permissions { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public bool IsExpired { get; set; }
    public int AccessCount { get; set; }
    public DateTime? LastAccessedAt { get; set; }
}

/// <summary>
/// Revoke share request
/// </summary>
public class RevokeShareRequest
{
    public string? Reason { get; set; }
}
