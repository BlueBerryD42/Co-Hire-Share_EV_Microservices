using CoOwnershipVehicle.Shared.Contracts.DTOs;

namespace CoOwnershipVehicle.Admin.Api.Services;

public interface ISystemMetricsService
{
    Task<SystemMetricsDto> GetSystemMetricsAsync(TimeSpan? period = null);
    Task RecordRequestAsync(string serviceName, string endpoint, double responseTimeMs, bool isSuccess);
    Task<ServiceMetricsDto> GetServiceMetricsAsync(string serviceName, TimeSpan? period = null);
}

