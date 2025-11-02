using CoOwnershipVehicle.Shared.Contracts.DTOs;

namespace CoOwnershipVehicle.Admin.Api.Services;

public interface ISystemLogsService
{
    Task<SystemLogsResponseDto> GetLogsAsync(SystemLogsRequestDto request);
    Task<byte[]> ExportLogsAsync(SystemLogsRequestDto request, string format = "json");
}

