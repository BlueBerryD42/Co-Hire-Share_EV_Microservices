using Microsoft.EntityFrameworkCore;
using CoOwnershipVehicle.Data;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Shared.Contracts.DTOs;

namespace CoOwnershipVehicle.Notification.Api.Services;

public class NotificationTemplateService : INotificationTemplateService
{
    private readonly ApplicationDbContext _context;

    public NotificationTemplateService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<NotificationTemplateDto> CreateTemplateAsync(CreateNotificationTemplateDto dto)
    {
        var template = new NotificationTemplate
        {
            TemplateKey = dto.TemplateKey,
            TitleTemplate = dto.TitleTemplate,
            MessageTemplate = dto.MessageTemplate,
            Type = Enum.Parse<NotificationType>(dto.Type),
            Priority = Enum.Parse<NotificationPriority>(dto.Priority),
            ActionUrlTemplate = dto.ActionUrlTemplate,
            ActionText = dto.ActionText
        };

        _context.NotificationTemplates.Add(template);
        await _context.SaveChangesAsync();

        return MapToDto(template);
    }

    public async Task<NotificationTemplateDto> GetTemplateByIdAsync(Guid id)
    {
        var template = await _context.NotificationTemplates.FindAsync(id);
        return template != null ? MapToDto(template) : null;
    }

    public async Task<NotificationTemplateDto> GetTemplateByKeyAsync(string templateKey)
    {
        var template = await _context.NotificationTemplates
            .FirstOrDefaultAsync(t => t.TemplateKey == templateKey && t.IsActive);
        return template != null ? MapToDto(template) : null;
    }

    public async Task<List<NotificationTemplateDto>> GetAllTemplatesAsync()
    {
        var templates = await _context.NotificationTemplates
            .OrderBy(t => t.TemplateKey)
            .ToListAsync();
        
        return templates.Select(MapToDto).ToList();
    }

    public async Task<NotificationTemplateDto> UpdateTemplateAsync(Guid id, CreateNotificationTemplateDto dto)
    {
        var template = await _context.NotificationTemplates.FindAsync(id);
        if (template == null) return null;

        template.TemplateKey = dto.TemplateKey;
        template.TitleTemplate = dto.TitleTemplate;
        template.MessageTemplate = dto.MessageTemplate;
        template.Type = Enum.Parse<NotificationType>(dto.Type);
        template.Priority = Enum.Parse<NotificationPriority>(dto.Priority);
        template.ActionUrlTemplate = dto.ActionUrlTemplate;
        template.ActionText = dto.ActionText;

        await _context.SaveChangesAsync();
        return MapToDto(template);
    }

    public async Task<bool> DeleteTemplateAsync(Guid id)
    {
        var template = await _context.NotificationTemplates.FindAsync(id);
        if (template == null) return false;

        _context.NotificationTemplates.Remove(template);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ActivateTemplateAsync(Guid id)
    {
        var template = await _context.NotificationTemplates.FindAsync(id);
        if (template == null) return false;

        template.IsActive = true;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeactivateTemplateAsync(Guid id)
    {
        var template = await _context.NotificationTemplates.FindAsync(id);
        if (template == null) return false;

        template.IsActive = false;
        await _context.SaveChangesAsync();
        return true;
    }

    private static NotificationTemplateDto MapToDto(NotificationTemplate template)
    {
        return new NotificationTemplateDto
        {
            Id = template.Id,
            TemplateKey = template.TemplateKey,
            TitleTemplate = template.TitleTemplate,
            MessageTemplate = template.MessageTemplate,
            Type = template.Type.ToString(),
            Priority = template.Priority.ToString(),
            IsActive = template.IsActive,
            ActionUrlTemplate = template.ActionUrlTemplate,
            ActionText = template.ActionText
        };
    }
}
