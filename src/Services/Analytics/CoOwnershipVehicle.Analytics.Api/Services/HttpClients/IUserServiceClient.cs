using CoOwnershipVehicle.Shared.Contracts.DTOs;

namespace CoOwnershipVehicle.Analytics.Api.Services.HttpClients;

public interface IUserServiceClient
{
    Task<List<UserProfileDto>> GetUsersAsync();
    Task<int> GetUserCountAsync();
}

