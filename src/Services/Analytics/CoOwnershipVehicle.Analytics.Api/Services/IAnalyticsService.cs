using CoOwnershipVehicle.Shared.Contracts.DTOs;

namespace CoOwnershipVehicle.Analytics.Api.Services;

public interface IAnalyticsService
{
    Task<AnalyticsDashboardDto> GetDashboardAsync(AnalyticsRequestDto request);
    Task<List<AnalyticsSnapshotDto>> GetSnapshotsAsync(AnalyticsRequestDto request);
    Task<List<UserAnalyticsDto>> GetUserAnalyticsAsync(AnalyticsRequestDto request);
    Task<List<VehicleAnalyticsDto>> GetVehicleAnalyticsAsync(AnalyticsRequestDto request);
    Task<List<GroupAnalyticsDto>> GetGroupAnalyticsAsync(AnalyticsRequestDto request);
    Task<AnalyticsSnapshotDto> CreateSnapshotAsync(CreateAnalyticsSnapshotDto dto);
    Task<bool> ProcessAnalyticsAsync(Guid? groupId = null, Guid? vehicleId = null);
    Task<bool> GeneratePeriodicAnalyticsAsync(AnalyticsPeriod period, DateTime? startDate = null);
    Task<Dictionary<string, object>> GetKpiMetricsAsync(AnalyticsRequestDto request);
    Task<List<Dictionary<string, object>>> GetTrendDataAsync(AnalyticsRequestDto request);
}
