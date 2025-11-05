namespace CoOwnershipVehicle.Domain.Enums;

/// <summary>
/// Priority level for maintenance tasks
/// </summary>
public enum MaintenancePriority
{
    /// <summary>
    /// Low priority - can be scheduled at convenience
    /// </summary>
    Low = 0,

    /// <summary>
    /// Medium priority - should be scheduled soon
    /// </summary>
    Medium = 1,

    /// <summary>
    /// High priority - needs to be scheduled promptly
    /// </summary>
    High = 2,

    /// <summary>
    /// Urgent - requires immediate attention
    /// </summary>
    Urgent = 3
}
