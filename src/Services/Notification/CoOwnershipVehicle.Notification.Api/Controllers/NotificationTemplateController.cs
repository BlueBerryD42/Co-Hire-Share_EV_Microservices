using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Notification.Api.Services;
using System.Security.Claims;

namespace CoOwnershipVehicle.Notification.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationTemplateController : ControllerBase
{
    private readonly INotificationTemplateService _templateService;

    public NotificationTemplateController(INotificationTemplateService templateService)
    {
        _templateService = templateService;
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<NotificationTemplateDto>> CreateTemplate([FromBody] CreateNotificationTemplateDto dto)
    {
        var template = await _templateService.CreateTemplateAsync(dto);
        return CreatedAtAction(nameof(GetTemplate), new { id = template.Id }, template);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<NotificationTemplateDto>> GetTemplate(Guid id)
    {
        var template = await _templateService.GetTemplateByIdAsync(id);
        if (template == null)
            return NotFound();

        return Ok(template);
    }

    [HttpGet("key/{templateKey}")]
    public async Task<ActionResult<NotificationTemplateDto>> GetTemplateByKey(string templateKey)
    {
        var template = await _templateService.GetTemplateByKeyAsync(templateKey);
        if (template == null)
            return NotFound();

        return Ok(template);
    }

    [HttpGet]
    public async Task<ActionResult<List<NotificationTemplateDto>>> GetAllTemplates()
    {
        var templates = await _templateService.GetAllTemplatesAsync();
        return Ok(templates);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<NotificationTemplateDto>> UpdateTemplate(Guid id, [FromBody] CreateNotificationTemplateDto dto)
    {
        var template = await _templateService.UpdateTemplateAsync(id, dto);
        if (template == null)
            return NotFound();

        return Ok(template);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> DeleteTemplate(Guid id)
    {
        var result = await _templateService.DeleteTemplateAsync(id);
        if (!result)
            return NotFound();

        return NoContent();
    }

    [HttpPost("{id}/activate")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> ActivateTemplate(Guid id)
    {
        var result = await _templateService.ActivateTemplateAsync(id);
        if (!result)
            return NotFound();

        return Ok(new { message = "Template activated successfully" });
    }

    [HttpPost("{id}/deactivate")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> DeactivateTemplate(Guid id)
    {
        var result = await _templateService.DeactivateTemplateAsync(id);
        if (!result)
            return NotFound();

        return Ok(new { message = "Template deactivated successfully" });
    }
}
