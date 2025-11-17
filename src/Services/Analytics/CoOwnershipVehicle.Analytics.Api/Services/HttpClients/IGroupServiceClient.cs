using CoOwnershipVehicle.Shared.Contracts.DTOs;

namespace CoOwnershipVehicle.Analytics.Api.Services.HttpClients;

public interface IGroupServiceClient
{
    Task<List<GroupDto>> GetGroupsAsync();
    Task<GroupDetailsDto?> GetGroupDetailsAsync(Guid groupId);
}

