using CoOwnershipVehicle.Shared.Contracts.DTOs;

namespace CoOwnershipVehicle.Payment.Api.Services.Interfaces;

/// <summary>
/// HTTP client interface for communicating with Group Service
/// Used to fetch group and member information since Payment service doesn't store these entities
/// </summary>
public interface IGroupServiceClient
{
    /// <summary>
    /// Get all groups that the current user is a member of
    /// </summary>
    Task<List<GroupDto>> GetUserGroups(string accessToken);

    /// <summary>
    /// Check if a user is a member of a group
    /// </summary>
    Task<bool> IsUserInGroupAsync(Guid groupId, Guid userId, string accessToken);

    /// <summary>
    /// Get group details including members and their ownership percentages
    /// </summary>
    Task<GroupDetailsDto?> GetGroupDetailsAsync(Guid groupId, string accessToken);
}

