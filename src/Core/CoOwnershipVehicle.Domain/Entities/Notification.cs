using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoOwnershipVehicle.Domain.Entities;

public class Notification : BaseEntity
{
    public Guid UserId { get; set; }
    
    public Guid? GroupId { get; set; }
    
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;
    
    [Required]
    [StringLength(1000)]
    public string Message { get; set; } = string.Empty;
    
    public NotificationType Type { get; set; }
    
    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;

    public NotificationStatus Status { get; set; } = NotificationStatus.Unread;

    public DateTime? ReadAt { get; set; }
    
    public DateTime ScheduledFor { get; set; } = DateTime.UtcNow;
    
    [StringLength(500)]
    public string? ActionUrl { get; set; }
    
    [StringLength(100)]
    public string? ActionText { get; set; }
    
    // Navigation properties
    public virtual User User { get; set; } = null!;
    public virtual OwnershipGroup? Group { get; set; }
}

public class NotificationTemplate : BaseEntity
{
    [Required]
    [StringLength(100)]
    public string TemplateKey { get; set; } = string.Empty;
    
    [Required]
    [StringLength(200)]
    public string TitleTemplate { get; set; } = string.Empty;
    
    [Required]
    [StringLength(1000)]
    public string MessageTemplate { get; set; } = string.Empty;
    
    public NotificationType Type { get; set; }
    
    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;
    
    public bool IsActive { get; set; } = true;
    
    [StringLength(500)]
    public string? ActionUrlTemplate { get; set; }
    
    [StringLength(100)]
    public string? ActionText { get; set; }
}

public enum NotificationType
{
    BookingCreated = 0,
    BookingApproved = 1,
    BookingCancelled = 2,
    BookingReminder = 3,
    PaymentDue = 4,
    PaymentReceived = 5,
    ExpenseAdded = 6,
    ProposalCreated = 7,
    VoteRequired = 8,
    ProposalPassed = 9,
    ProposalRejected = 10,
    VehicleMaintenance = 11,
    GroupInvitation = 12,
    GroupJoined = 13,
    GroupLeft = 14,
    DocumentSigned = 15,
    DocumentExpiring = 16,
    SystemAlert = 17,
    EmergencyBooking = 18,
    OverduePayment = 19,
    MaintenanceReminder = 20,
    Chat = 21
}

public enum NotificationPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Urgent = 3
}

public enum NotificationStatus
{
    Unread = 0,
    Read = 1,
    Dismissed = 2,
    Archived = 3
}
