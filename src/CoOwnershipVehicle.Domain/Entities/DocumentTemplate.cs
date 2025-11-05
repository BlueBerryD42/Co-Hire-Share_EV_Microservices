namespace CoOwnershipVehicle.Domain.Entities;

/// <summary>
/// Pre-built contract templates for document generation
/// </summary>
public class DocumentTemplate : BaseEntity
{
    /// <summary>
    /// Template name (e.g., "Vehicle Co-Ownership Agreement")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Template description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Template category
    /// </summary>
    public DocumentTemplateCategory Category { get; set; }

    /// <summary>
    /// Template content (HTML with variable placeholders like {{vehicleModel}})
    /// </summary>
    public string TemplateContent { get; set; } = string.Empty;

    /// <summary>
    /// JSON array of variable definitions
    /// Example: [{"name":"vehicleModel","label":"Vehicle Model","type":"text","required":true}]
    /// </summary>
    public string VariablesJson { get; set; } = "[]";

    /// <summary>
    /// Whether template is active and available for use
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// User who created the template (System Admin)
    /// </summary>
    public Guid CreatedBy { get; set; }

    /// <summary>
    /// Template version number
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Preview image URL (optional)
    /// </summary>
    public string? PreviewImageUrl { get; set; }

    // Navigation properties
    public User Creator { get; set; } = null!;
    public ICollection<Document> GeneratedDocuments { get; set; } = new List<Document>();
}

/// <summary>
/// Document template categories
/// </summary>
public enum DocumentTemplateCategory
{
    Legal = 0,
    Insurance = 1,
    Maintenance = 2,
    Financial = 3,
    Usage = 4,
    Sale = 5,
    Other = 99
}

/// <summary>
/// Template variable definition (for deserialization from VariablesJson)
/// </summary>
public class TemplateVariable
{
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = "text"; // text, number, date, select
    public bool Required { get; set; } = true;
    public string? DefaultValue { get; set; }
    public string? Placeholder { get; set; }
    public List<string>? Options { get; set; } // For select type
    public string? ValidationRegex { get; set; }
    public string? HelpText { get; set; }
}
