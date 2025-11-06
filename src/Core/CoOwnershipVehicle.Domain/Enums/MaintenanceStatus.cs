namespace CoOwnershipVehicle.Domain.Enums;

/// <summary>
/// Status of a maintenance schedule or record
/// </summary>
public enum MaintenanceStatus
{
    /// <summary>
    /// Maintenance is scheduled but not yet started
    /// </summary>
    Scheduled = 0,

    /// <summary>
    /// Maintenance is currently in progress
    /// </summary>
    InProgress = 1,

    /// <summary>
    /// Maintenance has been completed
    /// </summary>
    Completed = 2,

    /// <summary>
    /// Maintenance has been cancelled
    /// </summary>
    Cancelled = 3,

    /// <summary>
    /// Scheduled maintenance is overdue
    /// </summary>
    Overdue = 4
}
