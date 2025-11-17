using CoOwnershipVehicle.Shared.Contracts.DTOs;

namespace CoOwnershipVehicle.Admin.Api.Services.HttpClients;

public interface IGroupServiceClient
{
    Task<List<GroupDto>> GetGroupsAsync(GroupListRequestDto? request = null);
    Task<GroupDetailsDto?> GetGroupDetailsAsync(Guid groupId);
    Task<bool> UpdateGroupStatusAsync(Guid groupId, UpdateGroupStatusDto request);
}

