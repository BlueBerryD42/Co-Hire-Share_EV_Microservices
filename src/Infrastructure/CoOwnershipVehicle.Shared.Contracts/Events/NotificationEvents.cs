namespace CoOwnershipVehicle.Shared.Contracts.Events;

public class NotificationCreatedEvent
{
    public Guid NotificationId { get; set; }
    public Guid UserId { get; set; }
    public Guid? GroupId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? ActionUrl { get; set; }
    public string? ActionText { get; set; }
}

public class NotificationReadEvent
{
    public Guid NotificationId { get; set; }
    public Guid UserId { get; set; }
    public DateTime ReadAt { get; set; } = DateTime.UtcNow;
}

public class BulkNotificationEvent
{
    public List<Guid> UserIds { get; set; } = new();
    public Guid? GroupId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? ActionUrl { get; set; }
    public string? ActionText { get; set; }
}

public class NotificationTemplateEvent
{
    public string TemplateKey { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public Guid? GroupId { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
