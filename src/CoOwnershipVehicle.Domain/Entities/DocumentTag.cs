namespace CoOwnershipVehicle.Domain.Entities;

/// <summary>
/// Tags for document organization and search
/// </summary>
public class DocumentTag : BaseEntity
{
    /// <summary>
    /// Tag name (e.g., "insurance-2024", "contract-renewal")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Tag color for UI display (hex code)
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// Tag description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Group this tag belongs to (null = system-wide)
    /// </summary>
    public Guid? GroupId { get; set; }

    /// <summary>
    /// Number of documents using this tag
    /// </summary>
    public int UsageCount { get; set; } = 0;

    /// <summary>
    /// User who created the tag
    /// </summary>
    public Guid CreatedBy { get; set; }

    // Navigation properties
    public OwnershipGroup? Group { get; set; }
    public User Creator { get; set; } = null!;
    public ICollection<DocumentTagMapping> DocumentMappings { get; set; } = new List<DocumentTagMapping>();
}

/// <summary>
/// Many-to-many relationship between Documents and Tags
/// </summary>
public class DocumentTagMapping
{
    public Guid DocumentId { get; set; }
    public Guid TagId { get; set; }
    public DateTime TaggedAt { get; set; }
    public Guid TaggedBy { get; set; }

    // Navigation properties
    public Document Document { get; set; } = null!;
    public DocumentTag Tag { get; set; } = null!;
    public User TaggerUser { get; set; } = null!;
}

/// <summary>
/// Saved search filters for quick access
/// </summary>
public class SavedDocumentSearch : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid UserId { get; set; }
    public Guid? GroupId { get; set; }

    /// <summary>
    /// Serialized search criteria (JSON)
    /// </summary>
    public string SearchCriteriaJson { get; set; } = "{}";

    public int UsageCount { get; set; } = 0;
    public DateTime? LastUsedAt { get; set; }
    public bool IsDefault { get; set; } = false;

    // Navigation properties
    public User User { get; set; } = null!;
    public OwnershipGroup? Group { get; set; }
}
