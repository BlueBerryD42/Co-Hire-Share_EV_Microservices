using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Group.Api.DTOs;

/// <summary>
/// Request to create a new document template
/// </summary>
public class CreateTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DocumentTemplateCategory Category { get; set; }
    public string TemplateContent { get; set; } = string.Empty;
    public List<TemplateVariable> Variables { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public string? PreviewImageUrl { get; set; }
}

/// <summary>
/// Response for template list
/// </summary>
public class TemplateListResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DocumentTemplateCategory Category { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int VariableCount { get; set; }
    public List<TemplateVariable> Variables { get; set; } = new();
    public bool IsActive { get; set; }
    public int Version { get; set; }
    public string? PreviewImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public int UsageCount { get; set; } // Number of documents generated from this template
}

/// <summary>
/// Request to generate document from template
/// </summary>
public class GenerateFromTemplateRequest
{
    public Guid TemplateId { get; set; }
    public Guid GroupId { get; set; }
    public Dictionary<string, string> VariableValues { get; set; } = new();
    public string? CustomFileName { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Response after generating document from template
/// </summary>
public class GenerateFromTemplateResponse
{
    public Guid DocumentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string DownloadUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Template preview response
/// </summary>
public class TemplatePreviewResponse
{
    public string PreviewHtml { get; set; } = string.Empty;
    public List<string> MissingVariables { get; set; } = new();
}

/// <summary>
/// Template details response
/// </summary>
public class TemplateDetailResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DocumentTemplateCategory Category { get; set; }
    public string TemplateContent { get; set; } = string.Empty;
    public List<TemplateVariable> Variables { get; set; } = new();
    public bool IsActive { get; set; }
    public int Version { get; set; }
    public string? PreviewImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public int UsageCount { get; set; }
}

/// <summary>
/// Query parameters for template list
/// </summary>
public class TemplateQueryParameters
{
    public DocumentTemplateCategory? Category { get; set; }
    public bool? IsActive { get; set; }
    public string? SearchTerm { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
