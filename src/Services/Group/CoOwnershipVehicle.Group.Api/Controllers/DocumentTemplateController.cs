using CoOwnershipVehicle.Group.Api.DTOs;
using CoOwnershipVehicle.Group.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoOwnershipVehicle.Group.Api.Controllers;

/// <summary>
/// Controller for managing document templates
/// </summary>
[ApiController]
[Route("api/document/template")]
[Authorize]
public class DocumentTemplateController : BaseAuthenticatedController
{
    private readonly ITemplateService _templateService;

    public DocumentTemplateController(
        ITemplateService templateService,
        ILogger<DocumentTemplateController> logger)
        : base(logger)
    {
        _templateService = templateService;
    }

    /// <summary>
    /// Create a new document template (admin only)
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(TemplateDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TemplateDetailResponse>> CreateTemplate(
        [FromBody] CreateTemplateRequest request)
    {
        try
        {
            var userId = GetUserId();
            Logger.LogInformation("CreateTemplate called - UserId from token: {UserId}", userId);
            Logger.LogInformation("CreateTemplate request - Name: {Name}, Category: {Category}", request.Name, request.Category);

            var result = await _templateService.CreateTemplateAsync(request, userId);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Unauthorized template creation attempt - Error: {Message}", ex.Message);
            return StatusCode(403, new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            Logger.LogWarning(ex, "Invalid template request");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error creating template");
            return StatusCode(500, new { error = "An error occurred while creating the template" });
        }
    }

    /// <summary>
    /// Get all templates with optional filtering
    /// </summary>
    [HttpGet]
    [Route("/api/document/templates")]
    [ProducesResponseType(typeof(List<TemplateListResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TemplateListResponse>>> GetTemplates(
        [FromQuery] TemplateQueryParameters parameters)
    {
        try
        {
            var result = await _templateService.GetTemplatesAsync(parameters);
            return Ok(result);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error retrieving templates");
            return StatusCode(500, new { error = "An error occurred while retrieving templates" });
        }
    }

    /// <summary>
    /// Get template by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TemplateDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TemplateDetailResponse>> GetTemplate(Guid id)
    {
        try
        {
            var result = await _templateService.GetTemplateByIdAsync(id);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            Logger.LogWarning(ex, "Template not found");
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error retrieving template");
            return StatusCode(500, new { error = "An error occurred while retrieving the template" });
        }
    }

    /// <summary>
    /// Generate document from template
    /// </summary>
    [HttpPost("{id}/generate")]
    [ProducesResponseType(typeof(GenerateFromTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<GenerateFromTemplateResponse>> GenerateFromTemplate(
        Guid id, [FromBody] GenerateFromTemplateRequest request)
    {
        try
        {
            var userId = GetUserId();
            request.TemplateId = id; // Ensure template ID matches route
            var result = await _templateService.GenerateFromTemplateAsync(request, userId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            Logger.LogWarning(ex, "Template not found");
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Unauthorized template generation attempt");
            return Forbid();
        }
        catch (ArgumentException ex)
        {
            Logger.LogWarning(ex, "Invalid generation request");
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            Logger.LogWarning(ex, "Template generation failed");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error generating document from template");
            return StatusCode(500, new { error = "An error occurred while generating the document" });
        }
    }

    /// <summary>
    /// Preview template with variable substitution
    /// </summary>
    [HttpPost("{id}/preview")]
    [ProducesResponseType(typeof(TemplatePreviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TemplatePreviewResponse>> PreviewTemplate(
        Guid id, [FromBody] Dictionary<string, string> variables)
    {
        try
        {
            var result = await _templateService.PreviewTemplateAsync(id, variables);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            Logger.LogWarning(ex, "Template not found");
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error previewing template");
            return StatusCode(500, new { error = "An error occurred while previewing the template" });
        }
    }

    /// <summary>
    /// Update template (admin only)
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(TemplateDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TemplateDetailResponse>> UpdateTemplate(
        Guid id, [FromBody] CreateTemplateRequest request)
    {
        try
        {
            var userId = GetUserId();
            var result = await _templateService.UpdateTemplateAsync(id, request, userId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            Logger.LogWarning(ex, "Template not found");
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Unauthorized template update attempt");
            return Forbid();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error updating template");
            return StatusCode(500, new { error = "An error occurred while updating the template" });
        }
    }

    /// <summary>
    /// Delete template (admin only)
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteTemplate(Guid id)
    {
        try
        {
            var userId = GetUserId();
            await _templateService.DeleteTemplateAsync(id, userId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            Logger.LogWarning(ex, "Template not found");
            return NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "Unauthorized template deletion attempt");
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            Logger.LogWarning(ex, "Cannot delete template");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error deleting template");
            return StatusCode(500, new { error = "An error occurred while deleting the template" });
        }
    }
}
