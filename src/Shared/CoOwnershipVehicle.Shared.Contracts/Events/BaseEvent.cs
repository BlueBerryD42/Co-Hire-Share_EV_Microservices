using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Shared.Contracts.Events;

public abstract class BaseEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public string EventType { get; set; } = string.Empty;
    public Guid CorrelationId { get; set; } = Guid.NewGuid();
}

public class UserRegisteredEvent : BaseEvent
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    
    public UserRegisteredEvent()
    {
        EventType = nameof(UserRegisteredEvent);
    }
}

public class UserKycStatusChangedEvent : BaseEvent
{
    public Guid UserId { get; set; }
    public KycStatus OldStatus { get; set; }
    public KycStatus NewStatus { get; set; }
    public string? Reason { get; set; }
    
    public UserKycStatusChangedEvent()
    {
        EventType = nameof(UserKycStatusChangedEvent);
    }
}

public class GroupCreatedEvent : BaseEvent
{
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public Guid CreatedBy { get; set; }
    public List<GroupMemberData> Members { get; set; } = new();
    
    public GroupCreatedEvent()
    {
        EventType = nameof(GroupCreatedEvent);
    }
}

public class GroupMemberData
{
    public Guid UserId { get; set; }
    public decimal SharePercentage { get; set; }
    public GroupRole Role { get; set; }
}

public class GroupSharesUpdatedEvent : BaseEvent
{
    public Guid GroupId { get; set; }
    public List<GroupMemberData> UpdatedShares { get; set; } = new();
    public Guid UpdatedBy { get; set; }
    
    public GroupSharesUpdatedEvent()
    {
        EventType = nameof(GroupSharesUpdatedEvent);
    }
}
