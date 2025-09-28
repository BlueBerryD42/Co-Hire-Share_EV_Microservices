using CoOwnershipVehicle.Shared.Contracts.DTOs;

namespace CoOwnershipVehicle.Notification.Api.Services;

public interface INotificationService
{
    Task<NotificationDto> CreateNotificationAsync(CreateNotificationDto dto);
    Task<List<NotificationDto>> CreateBulkNotificationAsync(CreateBulkNotificationDto dto);
    Task<NotificationDto> GetNotificationByIdAsync(Guid id);
    Task<List<NotificationDto>> GetUserNotificationsAsync(Guid userId, NotificationRequestDto request);
    Task<NotificationStatsDto> GetNotificationStatsAsync(Guid userId);
    Task<NotificationDto> MarkAsReadAsync(Guid id, Guid userId);
    Task<bool> MarkAllAsReadAsync(Guid userId);
    Task<bool> DeleteNotificationAsync(Guid id, Guid userId);
    Task<bool> DeleteOldNotificationsAsync(int daysOld = 30);
    Task<NotificationDto> CreateFromTemplateAsync(string templateKey, Guid userId, Guid? groupId, Dictionary<string, object> parameters);
}
