using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Vehicle.Api.DTOs;

namespace CoOwnershipVehicle.Vehicle.Api.Services;

public interface IGroupServiceClient
{
    Task<List<GroupServiceGroupDto>> GetUserGroups(string accessToken);
    Task<bool> IsUserInGroupAsync(Guid groupId, Guid userId, string accessToken);

    /// <summary>
    /// Get group details with all members and ownership information
    /// </summary>
    Task<CoOwnershipVehicle.Shared.Contracts.DTOs.GroupDetailsDto?> GetGroupDetailsAsync(Guid groupId, string accessToken);
}
