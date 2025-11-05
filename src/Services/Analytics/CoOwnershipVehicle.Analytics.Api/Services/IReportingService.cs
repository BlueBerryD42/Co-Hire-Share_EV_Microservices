using CoOwnershipVehicle.Shared.Contracts.DTOs;

namespace CoOwnershipVehicle.Analytics.Api.Services;

public interface IReportingService
{
    Task<byte[]> GeneratePdfReportAsync(AnalyticsRequestDto request, string reportType);
    Task<byte[]> GenerateExcelReportAsync(AnalyticsRequestDto request, string reportType);
    Task<Dictionary<string, object>> GetReportDataAsync(AnalyticsRequestDto request, string reportType);
    Task<List<Dictionary<string, object>>> GetExportableDataAsync(AnalyticsRequestDto request, string reportType);
    Task<byte[]> GenerateFairnessReportPdfAsync(AnalyticsRequestDto request, Guid groupId);
}
