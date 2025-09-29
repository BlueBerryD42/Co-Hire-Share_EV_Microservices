using Microsoft.EntityFrameworkCore;
using CoOwnershipVehicle.Data;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Notification.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace CoOwnershipVehicle.Notification.Api.Services;

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly INotificationTemplateService _templateService;

    public NotificationService(
        ApplicationDbContext context,
        IHubContext<NotificationHub> hubContext,
        INotificationTemplateService templateService)
    {
        _context = context;
        _hubContext = hubContext;
        _templateService = templateService;
    }

    public async Task<NotificationDto> CreateNotificationAsync(CreateNotificationDto dto)
    {
        var notification = new CoOwnershipVehicle.Domain.Entities.Notification
        {
            UserId = dto.UserId,
            GroupId = dto.GroupId,
            Title = dto.Title,
            Message = dto.Message,
            Type = Enum.Parse<NotificationType>(dto.Type),
            Priority = Enum.Parse<NotificationPriority>(dto.Priority),
            ScheduledFor = dto.ScheduledFor ?? DateTime.UtcNow,
            ActionUrl = dto.ActionUrl,
            ActionText = dto.ActionText
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        // Send real-time notification
        await SendRealTimeNotification(notification);

        return MapToDto(notification);
    }

    public async Task<List<NotificationDto>> CreateBulkNotificationAsync(CreateBulkNotificationDto dto)
    {
        var notifications = new List<CoOwnershipVehicle.Domain.Entities.Notification>();
        var notificationDtos = new List<NotificationDto>();

        foreach (var userId in dto.UserIds)
        {
            var notification = new CoOwnershipVehicle.Domain.Entities.Notification
            {
                UserId = userId,
                GroupId = dto.GroupId,
                Title = dto.Title,
                Message = dto.Message,
                Type = Enum.Parse<NotificationType>(dto.Type),
                Priority = Enum.Parse<NotificationPriority>(dto.Priority),
                ScheduledFor = dto.ScheduledFor ?? DateTime.UtcNow,
                ActionUrl = dto.ActionUrl,
                ActionText = dto.ActionText
            };

            notifications.Add(notification);
        }

        _context.Notifications.AddRange(notifications);
        await _context.SaveChangesAsync();

        // Send real-time notifications
        foreach (var notification in notifications)
        {
            await SendRealTimeNotification(notification);
            notificationDtos.Add(MapToDto(notification));
        }

        return notificationDtos;
    }

    public async Task<NotificationDto> GetNotificationByIdAsync(Guid id)
    {
        var notification = await _context.Notifications
            .Include(n => n.Group)
            .FirstOrDefaultAsync(n => n.Id == id);

        return notification != null ? MapToDto(notification) : null;
    }

    public async Task<List<NotificationDto>> GetUserNotificationsAsync(Guid userId, NotificationRequestDto request)
    {
        var query = _context.Notifications
            .Include(n => n.Group)
            .Where(n => n.UserId == userId);

        if (request.GroupId.HasValue)
            query = query.Where(n => n.GroupId == request.GroupId);

        if (!string.IsNullOrEmpty(request.Type))
            query = query.Where(n => n.Type.ToString() == request.Type);

        if (!string.IsNullOrEmpty(request.Priority))
            query = query.Where(n => n.Priority.ToString() == request.Priority);

        if (!string.IsNullOrEmpty(request.Status))
            query = query.Where(n => n.Status.ToString() == request.Status);

        if (request.StartDate.HasValue)
            query = query.Where(n => n.CreatedAt >= request.StartDate);

        if (request.EndDate.HasValue)
            query = query.Where(n => n.CreatedAt <= request.EndDate);

        query = query.OrderByDescending(n => n.CreatedAt);

        if (request.Offset.HasValue)
            query = query.Skip(request.Offset.Value);

        if (request.Limit.HasValue)
            query = query.Take(request.Limit.Value);

        var notifications = await query.ToListAsync();
        return notifications.Select(MapToDto).ToList();
    }

    public async Task<NotificationStatsDto> GetNotificationStatsAsync(Guid userId)
    {
        var stats = await _context.Notifications
            .Where(n => n.UserId == userId)
            .GroupBy(n => 1)
            .Select(g => new NotificationStatsDto
            {
                UserId = userId,
                TotalNotifications = g.Count(),
                UnreadCount = g.Count(n => n.Status == NotificationStatus.Unread),
                ReadCount = g.Count(n => n.Status == NotificationStatus.Read),
                DismissedCount = g.Count(n => n.Status == NotificationStatus.Dismissed),
                UrgentCount = g.Count(n => n.Priority == NotificationPriority.Urgent),
                LastNotificationAt = g.Max(n => n.CreatedAt)
            })
            .FirstOrDefaultAsync();

        return stats ?? new NotificationStatsDto { UserId = userId };
    }

    public async Task<NotificationDto> MarkAsReadAsync(Guid id, Guid userId)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

        if (notification != null)
        {
            notification.Status = NotificationStatus.Read;
            notification.ReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Send real-time update
            await _hubContext.Clients.Group($"user_{userId}").SendAsync("NotificationRead", id);
        }

        return notification != null ? MapToDto(notification) : null;
    }

    public async Task<bool> MarkAllAsReadAsync(Guid userId)
    {
        var notifications = await _context.Notifications
            .Where(n => n.UserId == userId && n.Status == NotificationStatus.Unread)
            .ToListAsync();

        foreach (var notification in notifications)
        {
            notification.Status = NotificationStatus.Read;
            notification.ReadAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        // Send real-time update
        await _hubContext.Clients.Group($"user_{userId}").SendAsync("AllNotificationsRead");

        return true;
    }

    public async Task<bool> DeleteNotificationAsync(Guid id, Guid userId)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

        if (notification != null)
        {
            _context.Notifications.Remove(notification);
            await _context.SaveChangesAsync();

            // Send real-time update
            await _hubContext.Clients.Group($"user_{userId}").SendAsync("NotificationDeleted", id);
            return true;
        }

        return false;
    }

    public async Task<bool> DeleteOldNotificationsAsync(int daysOld = 30)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-daysOld);
        var oldNotifications = await _context.Notifications
            .Where(n => n.CreatedAt < cutoffDate && n.Status == NotificationStatus.Read)
            .ToListAsync();

        _context.Notifications.RemoveRange(oldNotifications);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<NotificationDto> CreateFromTemplateAsync(string templateKey, Guid userId, Guid? groupId, Dictionary<string, object> parameters)
    {
        var template = await _templateService.GetTemplateByKeyAsync(templateKey);
        if (template == null)
            throw new ArgumentException($"Template with key '{templateKey}' not found");

        var title = ReplaceParameters(template.TitleTemplate, parameters);
        var message = ReplaceParameters(template.MessageTemplate, parameters);
        var actionUrl = template.ActionUrlTemplate != null ? ReplaceParameters(template.ActionUrlTemplate, parameters) : null;

        var dto = new CreateNotificationDto
        {
            UserId = userId,
            GroupId = groupId,
            Title = title,
            Message = message,
            Type = template.Type.ToString(),
            Priority = template.Priority.ToString(),
            ActionUrl = actionUrl,
            ActionText = template.ActionText
        };

        return await CreateNotificationAsync(dto);
    }

    private async Task SendRealTimeNotification(CoOwnershipVehicle.Domain.Entities.Notification notification)
    {
        var dto = MapToDto(notification);
        
        // Send to user-specific group
        await _hubContext.Clients.Group($"user_{notification.UserId}").SendAsync("NewNotification", dto);
        
        // Send to group-specific group if applicable
        if (notification.GroupId.HasValue)
        {
            await _hubContext.Clients.Group($"group_{notification.GroupId}").SendAsync("NewGroupNotification", dto);
        }
    }

    private static string ReplaceParameters(string template, Dictionary<string, object> parameters)
    {
        var result = template;
        foreach (var param in parameters)
        {
            result = result.Replace($"{{{param.Key}}}", param.Value?.ToString() ?? "");
        }
        return result;
    }

    private static NotificationDto MapToDto(CoOwnershipVehicle.Domain.Entities.Notification notification)
    {
        return new NotificationDto
        {
            Id = notification.Id,
            UserId = notification.UserId,
            GroupId = notification.GroupId,
            GroupName = notification.Group?.Name ?? "",
            Title = notification.Title,
            Message = notification.Message,
            Type = notification.Type.ToString(),
            Priority = notification.Priority.ToString(),
            Status = notification.Status.ToString(),
            CreatedAt = notification.CreatedAt,
            ReadAt = notification.ReadAt,
            ScheduledFor = notification.ScheduledFor,
            ActionUrl = notification.ActionUrl,
            ActionText = notification.ActionText
        };
    }
}
