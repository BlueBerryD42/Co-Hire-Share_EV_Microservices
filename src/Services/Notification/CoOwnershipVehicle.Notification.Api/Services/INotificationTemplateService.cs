using CoOwnershipVehicle.Shared.Contracts.DTOs;

namespace CoOwnershipVehicle.Notification.Api.Services;

public interface INotificationTemplateService
{
    Task<NotificationTemplateDto> CreateTemplateAsync(CreateNotificationTemplateDto dto);
    Task<NotificationTemplateDto> GetTemplateByIdAsync(Guid id);
    Task<NotificationTemplateDto> GetTemplateByKeyAsync(string templateKey);
    Task<List<NotificationTemplateDto>> GetAllTemplatesAsync();
    Task<NotificationTemplateDto> UpdateTemplateAsync(Guid id, CreateNotificationTemplateDto dto);
    Task<bool> DeleteTemplateAsync(Guid id);
    Task<bool> ActivateTemplateAsync(Guid id);
    Task<bool> DeactivateTemplateAsync(Guid id);
}
