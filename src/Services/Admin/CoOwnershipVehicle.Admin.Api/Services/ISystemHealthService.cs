using CoOwnershipVehicle.Shared.Contracts.DTOs;

namespace CoOwnershipVehicle.Admin.Api.Services;

public interface ISystemHealthService
{
    Task<SystemHealthCheckDto> CheckSystemHealthAsync();
    Task<ServiceHealthDto> CheckServiceHealthAsync(string serviceName, string baseUrl);
    Task<DependencyHealthDto> CheckDatabaseHealthAsync();
    Task<DependencyHealthDto> CheckRabbitMqHealthAsync();
    Task<DependencyHealthDto> CheckRedisHealthAsync();
    Task<DependencyHealthDto> CheckFileStorageHealthAsync();
}

