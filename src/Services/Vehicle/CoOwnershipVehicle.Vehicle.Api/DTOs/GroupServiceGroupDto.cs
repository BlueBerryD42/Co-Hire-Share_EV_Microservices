namespace CoOwnershipVehicle.Vehicle.Api.DTOs;

public class GroupServiceGroupDto
{
    public Guid Id { get; set; }
    // We only need the Id for filtering, but other properties could be added if needed.
}

/// <summary>
/// Group member with ownership information (for Vehicle Service use)
/// Microservices architecture: Group Service returns only UserId,
/// Vehicle Service should call Auth Service for user details if needed
/// </summary>
public class GroupMemberWithOwnership
{
    public Guid UserId { get; set; }
    public decimal OwnershipPercentage { get; set; }
    public string Role { get; set; } = string.Empty; // "Member", "Admin"

    // Optional: These fields are populated if needed (for backward compatibility)
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
}

// Note: GroupDetailsDto is defined in CoOwnershipVehicle.Shared.Contracts.DTOs
// This local DTO was removed to avoid ambiguity. Use the shared contract version instead.
