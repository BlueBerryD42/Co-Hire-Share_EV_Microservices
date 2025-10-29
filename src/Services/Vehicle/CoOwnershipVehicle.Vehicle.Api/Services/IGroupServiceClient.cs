namespace CoOwnershipVehicle.Vehicle.Api.Services;

public interface IGroupServiceClient
{
    Task<List<GroupServiceGroupDto>> GetUserGroups(string accessToken);
    Task<bool> IsUserInGroupAsync(Guid groupId, Guid userId, string accessToken);
}
