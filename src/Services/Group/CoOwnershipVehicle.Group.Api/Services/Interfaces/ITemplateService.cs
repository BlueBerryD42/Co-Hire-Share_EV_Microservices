using CoOwnershipVehicle.Group.Api.DTOs;

namespace CoOwnershipVehicle.Group.Api.Services.Interfaces;

/// <summary>
/// Service for managing document templates
/// </summary>
public interface ITemplateService
{
    /// <summary>
    /// Create a new document template (admin only)
    /// </summary>
    Task<TemplateDetailResponse> CreateTemplateAsync(CreateTemplateRequest request, Guid userId);

    /// <summary>
    /// Get all active templates
    /// </summary>
    Task<List<TemplateListResponse>> GetTemplatesAsync(TemplateQueryParameters parameters);

    /// <summary>
    /// Get template by ID
    /// </summary>
    Task<TemplateDetailResponse> GetTemplateByIdAsync(Guid templateId);

    /// <summary>
    /// Update template (admin only)
    /// </summary>
    Task<TemplateDetailResponse> UpdateTemplateAsync(Guid templateId, CreateTemplateRequest request, Guid userId);

    /// <summary>
    /// Delete template (admin only)
    /// </summary>
    Task DeleteTemplateAsync(Guid templateId, Guid userId);

    /// <summary>
    /// Generate document from template
    /// </summary>
    Task<GenerateFromTemplateResponse> GenerateFromTemplateAsync(GenerateFromTemplateRequest request, Guid userId);

    /// <summary>
    /// Preview template with variable substitution
    /// </summary>
    Task<TemplatePreviewResponse> PreviewTemplateAsync(Guid templateId, Dictionary<string, string> variableValues);

    /// <summary>
    /// Seed pre-built templates (for initial setup)
    /// </summary>
    Task SeedTemplatesAsync();
}
