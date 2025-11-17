using CoOwnershipVehicle.Shared.Contracts.DTOs;

namespace CoOwnershipVehicle.Analytics.Api.Services.HttpClients;

public interface IVehicleServiceClient
{
    Task<List<VehicleDto>> GetVehiclesAsync();
    Task<int> GetVehicleCountAsync();
    Task<VehicleDto?> GetVehicleAsync(Guid vehicleId);
}

