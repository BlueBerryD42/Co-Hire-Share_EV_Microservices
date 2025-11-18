using Microsoft.EntityFrameworkCore;
using CoOwnershipVehicle.Notification.Api.Data;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Notification.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.SqlClient;
using System.Data;
using Microsoft.Extensions.Configuration;

namespace CoOwnershipVehicle.Notification.Api.Services;

public class NotificationService : INotificationService
{
    private readonly NotificationDbContext _context;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly INotificationTemplateService _templateService;
    private readonly string _connectionString;

    public NotificationService(
        NotificationDbContext context,
        IHubContext<NotificationHub> hubContext,
        INotificationTemplateService templateService,
        IConfiguration configuration)
    {
        _context = context;
        _hubContext = hubContext;
        _templateService = templateService;
        
        // Get connection string directly to avoid EF Core model validation
        var dbParams = new
        {
            Server = configuration["DB_SERVER"] ?? "host.docker.internal",
            Database = configuration["DB_NOTIFICATION"] ?? "CoOwnershipVehicle_Notification",
            User = configuration["DB_USER"] ?? "sa",
            Password = configuration["DB_PASSWORD"] ?? "",
            TrustCert = configuration["DB_TRUST_CERT"] ?? "true",
            MultipleActiveResultSets = configuration["DB_MULTIPLE_ACTIVE_RESULTS"] ?? "true"
        };
        _connectionString = configuration["DB_CONNECTION_STRING"] 
            ?? $"Server={dbParams.Server};Database={dbParams.Database};User Id={dbParams.User};Password={dbParams.Password};TrustServerCertificate={dbParams.TrustCert};MultipleActiveResultSets={dbParams.MultipleActiveResultSets}";
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
        // Note: Group navigation property is ignored because OwnershipGroup is in Group service
        // Use AsNoTracking() to prevent EF Core from trying to load ignored navigation properties
        var notification = await _context.Notifications
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == id);

        return notification != null ? MapToDto(notification) : null;
    }

    public async Task<List<NotificationDto>> GetUserNotificationsAsync(Guid userId, NotificationRequestDto request)
    {
        // Use ADO.NET directly with a new connection to completely bypass EF Core's entity model
        // This prevents any attempt to include navigation properties or use EF Core's compiled queries
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            var whereConditions = new List<string> { "[UserId] = @userId" };
            command.Parameters.Add(new SqlParameter("@userId", userId));

            if (request.GroupId.HasValue)
            {
                whereConditions.Add("[GroupId] = @groupId");
                command.Parameters.Add(new SqlParameter("@groupId", request.GroupId.Value));
            }

            if (!string.IsNullOrEmpty(request.Type))
            {
                // Try to parse as enum name first, then as integer
                int typeValue;
                if (Enum.TryParse<NotificationType>(request.Type, true, out var typeEnum))
                {
                    typeValue = (int)typeEnum;
                }
                else if (int.TryParse(request.Type, out typeValue))
                {
                    // Already an integer
                }
                else
                {
                    // Invalid type - skip this filter
                    // Could also throw an exception or log a warning
                    typeValue = -1; // Use invalid value that won't match anything
                }
                
                if (typeValue >= 0)
                {
                    whereConditions.Add("[Type] = @type");
                    command.Parameters.Add(new SqlParameter("@type", typeValue));
                }
            }

            if (!string.IsNullOrEmpty(request.Priority))
            {
                // Try to parse as enum name first, then as integer
                int priorityValue;
                if (Enum.TryParse<NotificationPriority>(request.Priority, true, out var priorityEnum))
                {
                    priorityValue = (int)priorityEnum;
                }
                else if (int.TryParse(request.Priority, out priorityValue))
                {
                    // Already an integer
                }
                else
                {
                    // Invalid priority - skip this filter
                    priorityValue = -1;
                }
                
                if (priorityValue >= 0)
                {
                    whereConditions.Add("[Priority] = @priority");
                    command.Parameters.Add(new SqlParameter("@priority", priorityValue));
                }
            }

            if (!string.IsNullOrEmpty(request.Status))
            {
                // Try to parse as enum name first, then as integer
                int statusValue;
                if (Enum.TryParse<NotificationStatus>(request.Status, true, out var statusEnum))
                {
                    statusValue = (int)statusEnum;
                }
                else if (int.TryParse(request.Status, out statusValue))
                {
                    // Already an integer
                }
                else
                {
                    // Invalid status - skip this filter
                    statusValue = -1;
                }
                
                if (statusValue >= 0)
                {
                    whereConditions.Add("[Status] = @status");
                    command.Parameters.Add(new SqlParameter("@status", statusValue));
                }
            }

            if (request.StartDate.HasValue)
            {
                whereConditions.Add("[CreatedAt] >= @startDate");
                command.Parameters.Add(new SqlParameter("@startDate", request.StartDate.Value));
            }

            if (request.EndDate.HasValue)
            {
                whereConditions.Add("[CreatedAt] <= @endDate");
                command.Parameters.Add(new SqlParameter("@endDate", request.EndDate.Value));
            }

            var whereClause = string.Join(" AND ", whereConditions);
            var orderBy = "ORDER BY [CreatedAt] DESC";
            var limitClause = request.Limit.HasValue ? $"OFFSET {request.Offset ?? 0} ROWS FETCH NEXT {request.Limit.Value} ROWS ONLY" : "";

            command.CommandText = $@"
                SELECT [Id], [UserId], [GroupId], [Title], [Message], [Type], [Priority], [Status], 
                       [ReadAt], [ScheduledFor], [ActionUrl], [ActionText], [CreatedAt], [UpdatedAt]
                FROM [Notifications]
                WHERE {whereClause}
                {orderBy}
                {limitClause}";

            var results = new List<NotificationDto>();
            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    results.Add(new NotificationDto
                    {
                        Id = reader.GetGuid(0),
                        UserId = reader.GetGuid(1),
                        GroupId = reader.IsDBNull(2) ? null : reader.GetGuid(2),
                        GroupName = "", // Group name not available - fetch via HTTP if needed
                        Title = reader.GetString(3),
                        Message = reader.GetString(4),
                        Type = ((NotificationType)reader.GetInt32(5)).ToString(),
                        Priority = ((NotificationPriority)reader.GetInt32(6)).ToString(),
                        Status = ((NotificationStatus)reader.GetInt32(7)).ToString(),
                        ReadAt = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                        ScheduledFor = reader.GetDateTime(9),
                        ActionUrl = reader.IsDBNull(10) ? null : reader.GetString(10),
                        ActionText = reader.IsDBNull(11) ? null : reader.GetString(11),
                        CreatedAt = reader.GetDateTime(12),
                    });
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            // Log the error - this will appear in container logs
            Console.WriteLine($"[ERROR] GetUserNotificationsAsync failed: {ex.Message}");
            Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"[ERROR] Inner exception: {ex.InnerException.Message}");
            }
            
            // Re-throw to let the controller handle it
            throw;
        }
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
            GroupName = "", // Group navigation property is ignored - fetch group name via HTTP if needed
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
