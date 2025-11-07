namespace CoOwnershipVehicle.Shared.Contracts.DTOs;

public class NotificationDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime ScheduledFor { get; set; }
    public string? ActionUrl { get; set; }
    public string? ActionText { get; set; }
}

public class CreateNotificationDto
{
    public Guid UserId { get; set; }
    public Guid? GroupId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Priority { get; set; } = "Normal";
    public DateTime? ScheduledFor { get; set; }
    public string? ActionUrl { get; set; }
    public string? ActionText { get; set; }
}

public class CreateBulkNotificationDto
{
    public List<Guid> UserIds { get; set; } = new();
    public Guid? GroupId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Priority { get; set; } = "Normal";
    public DateTime? ScheduledFor { get; set; }
    public string? ActionUrl { get; set; }
    public string? ActionText { get; set; }
}

public class NotificationTemplateDto
{
    public Guid Id { get; set; }
    public string TemplateKey { get; set; } = string.Empty;
    public string TitleTemplate { get; set; } = string.Empty;
    public string MessageTemplate { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? ActionUrlTemplate { get; set; }
    public string? ActionText { get; set; }
}

public class CreateNotificationTemplateDto
{
    public string TemplateKey { get; set; } = string.Empty;
    public string TitleTemplate { get; set; } = string.Empty;
    public string MessageTemplate { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Priority { get; set; } = "Normal";
    public string? ActionUrlTemplate { get; set; }
    public string? ActionText { get; set; }
}

public class NotificationRequestDto
{
    public Guid? UserId { get; set; }
    public Guid? GroupId { get; set; }
    public string? Type { get; set; }
    public string? Priority { get; set; }
    public string? Status { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? Limit { get; set; }
    public int? Offset { get; set; }
}

public class NotificationStatsDto
{
    public Guid UserId { get; set; }
    public int TotalNotifications { get; set; }
    public int UnreadCount { get; set; }
    public int ReadCount { get; set; }
    public int DismissedCount { get; set; }
    public int UrgentCount { get; set; }
    public DateTime? LastNotificationAt { get; set; }
}
