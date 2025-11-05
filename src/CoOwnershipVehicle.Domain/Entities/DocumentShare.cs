namespace CoOwnershipVehicle.Domain.Entities;

/// <summary>
/// External document sharing with controlled permissions
/// </summary>
public class DocumentShare : BaseEntity
{
    /// <summary>
    /// Document being shared
    /// </summary>
    public Guid DocumentId { get; set; }

    /// <summary>
    /// Unique secure share token (used in public URL)
    /// </summary>
    public string ShareToken { get; set; } = string.Empty;

    /// <summary>
    /// User who shared the document
    /// </summary>
    public Guid SharedBy { get; set; }

    /// <summary>
    /// Email or name of recipient (external party)
    /// </summary>
    public string SharedWith { get; set; } = string.Empty;

    /// <summary>
    /// Email address of recipient
    /// </summary>
    public string? RecipientEmail { get; set; }

    /// <summary>
    /// Permissions granted (View, Download, Sign)
    /// </summary>
    public SharePermissions Permissions { get; set; }

    /// <summary>
    /// When the share link expires
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Optional message to recipient
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Number of times the share link has been accessed
    /// </summary>
    public int AccessCount { get; set; } = 0;

    /// <summary>
    /// When the share was first accessed
    /// </summary>
    public DateTime? FirstAccessedAt { get; set; }

    /// <summary>
    /// When the share was last accessed
    /// </summary>
    public DateTime? LastAccessedAt { get; set; }

    /// <summary>
    /// Whether the share has been manually revoked
    /// </summary>
    public bool IsRevoked { get; set; } = false;

    /// <summary>
    /// When the share was revoked
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// User who revoked the share
    /// </summary>
    public Guid? RevokedBy { get; set; }

    /// <summary>
    /// Optional password for protected shares
    /// </summary>
    public string? PasswordHash { get; set; }

    /// <summary>
    /// Maximum number of times the link can be accessed (null = unlimited)
    /// </summary>
    public int? MaxAccessCount { get; set; }

    // Navigation properties
    public Document Document { get; set; } = null!;
    public User Sharer { get; set; } = null!;
    public ICollection<DocumentShareAccess> AccessLog { get; set; } = new List<DocumentShareAccess>();
}

/// <summary>
/// Share permissions flags
/// </summary>
[Flags]
public enum SharePermissions
{
    None = 0,
    View = 1,
    Download = 2,
    Sign = 4,
    ViewAndDownload = View | Download,
    All = View | Download | Sign
}

/// <summary>
/// Log of share access events
/// </summary>
public class DocumentShareAccess : BaseEntity
{
    public Guid DocumentShareId { get; set; }
    public DateTime AccessedAt { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? Location { get; set; } // Optional geolocation
    public ShareAccessAction Action { get; set; }
    public bool WasSuccessful { get; set; }
    public string? FailureReason { get; set; }

    // Navigation
    public DocumentShare DocumentShare { get; set; } = null!;
}

/// <summary>
/// Actions performed on shared documents
/// </summary>
public enum ShareAccessAction
{
    Viewed = 0,
    Downloaded = 1,
    Signed = 2,
    PasswordAttempt = 3,
    Expired = 4,
    Revoked = 5
}
