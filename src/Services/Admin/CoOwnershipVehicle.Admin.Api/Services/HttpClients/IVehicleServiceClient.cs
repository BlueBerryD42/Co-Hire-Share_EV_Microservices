using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Admin.Api.Services.HttpClients;

public interface IVehicleServiceClient
{
    Task<List<VehicleDto>> GetVehiclesAsync();
    Task<VehicleDto?> GetVehicleAsync(Guid vehicleId);
    Task<int> GetVehicleCountAsync(VehicleStatus? status = null);
    Task<List<MaintenanceScheduleItemDto>> GetMaintenanceSchedulesAsync(MaintenanceScheduleRequestDto? request = null);
    Task<MaintenanceScheduleItemDto?> GetMaintenanceScheduleAsync(Guid maintenanceId);
    Task<bool> CreateMaintenanceScheduleAsync(CreateMaintenanceDto request);
    Task<bool> UpdateMaintenanceScheduleAsync(Guid maintenanceId, UpdateMaintenanceDto request);
}

