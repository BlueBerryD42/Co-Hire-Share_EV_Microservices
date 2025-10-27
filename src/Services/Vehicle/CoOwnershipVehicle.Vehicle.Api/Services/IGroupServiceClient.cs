namespace CoOwnershipVehicle.Vehicle.Api.Services;

public interface IGroupServiceClient
{
    Task<List<GroupServiceGroupDto>> GetUserGroups(string accessToken);
}
