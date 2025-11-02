using CoOwnershipVehicle.Shared.Contracts.DTOs;
using System.Text.Json;
using System.Text;

namespace CoOwnershipVehicle.Analytics.Api.Services;

public class ReportingService : IReportingService
{
    private readonly IAnalyticsService _analyticsService;
    private readonly ILogger<ReportingService> _logger;

    public ReportingService(IAnalyticsService analyticsService, ILogger<ReportingService> logger)
    {
        _analyticsService = analyticsService;
        _logger = logger;
    }

    public async Task<byte[]> GeneratePdfReportAsync(AnalyticsRequestDto request, string reportType)
    {
        // Placeholder implementation - would integrate with a PDF library like iTextSharp or Puppeteer
        _logger.LogInformation("Generating PDF report for type: {ReportType}", reportType);
        
        var reportData = await GetReportDataAsync(request, reportType);
        var jsonData = JsonSerializer.Serialize(reportData, new JsonSerializerOptions { WriteIndented = true });
        
        // Convert JSON to PDF bytes (simplified - would use actual PDF library)
        return System.Text.Encoding.UTF8.GetBytes($"PDF Report: {reportType}\n\n{jsonData}");
    }

    public async Task<byte[]> GenerateExcelReportAsync(AnalyticsRequestDto request, string reportType)
    {
        // Placeholder implementation - would integrate with EPPlus or ClosedXML
        _logger.LogInformation("Generating Excel report for type: {ReportType}", reportType);
        
        var exportData = await GetExportableDataAsync(request, reportType);
        var jsonData = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
        
        // Convert JSON to Excel bytes (simplified - would use actual Excel library)
        return System.Text.Encoding.UTF8.GetBytes($"Excel Report: {reportType}\n\n{jsonData}");
    }

    public async Task<Dictionary<string, object>> GetReportDataAsync(AnalyticsRequestDto request, string reportType)
    {
        return reportType.ToLower() switch
        {
            "dashboard" => await GetDashboardReportData(request),
            "user" => await GetUserReportData(request),
            "vehicle" => await GetVehicleReportData(request),
            "group" => await GetGroupReportData(request),
            "financial" => await GetFinancialReportData(request),
            "kpi" => await GetKpiReportData(request),
            _ => throw new ArgumentException($"Unknown report type: {reportType}")
        };
    }

    public async Task<List<Dictionary<string, object>>> GetExportableDataAsync(AnalyticsRequestDto request, string reportType)
    {
        var reportData = await GetReportDataAsync(request, reportType);
        
        // Convert report data to exportable format
        var exportData = new List<Dictionary<string, object>>();
        
        if (reportData.ContainsKey("data") && reportData["data"] is IEnumerable<object> dataList)
        {
            foreach (var item in dataList)
            {
                if (item is Dictionary<string, object> dict)
                {
                    exportData.Add(dict);
                }
            }
        }
        
        return exportData;
    }

    private async Task<Dictionary<string, object>> GetDashboardReportData(AnalyticsRequestDto request)
    {
        var dashboard = await _analyticsService.GetDashboardAsync(request);
        
        return new Dictionary<string, object>
        {
            ["reportType"] = "Dashboard",
            ["generatedAt"] = dashboard.GeneratedAt,
            ["period"] = $"{dashboard.PeriodStart:yyyy-MM-dd} to {dashboard.PeriodEnd:yyyy-MM-dd}",
            ["summary"] = new Dictionary<string, object>
            {
                ["totalGroups"] = dashboard.TotalGroups,
                ["totalVehicles"] = dashboard.TotalVehicles,
                ["totalUsers"] = dashboard.TotalUsers,
                ["totalBookings"] = dashboard.TotalBookings,
                ["totalRevenue"] = dashboard.TotalRevenue,
                ["totalExpenses"] = dashboard.TotalExpenses,
                ["netProfit"] = dashboard.NetProfit,
                ["averageUtilizationRate"] = dashboard.AverageUtilizationRate
            },
            ["data"] = dashboard
        };
    }

    private async Task<Dictionary<string, object>> GetUserReportData(AnalyticsRequestDto request)
    {
        var userAnalytics = await _analyticsService.GetUserAnalyticsAsync(request);
        
        return new Dictionary<string, object>
        {
            ["reportType"] = "User Analytics",
            ["generatedAt"] = DateTime.UtcNow,
            ["period"] = $"{request.StartDate:yyyy-MM-dd} to {request.EndDate:yyyy-MM-dd}",
            ["data"] = userAnalytics.Select(u => new Dictionary<string, object>
            {
                ["userId"] = u.UserId,
                ["userName"] = u.UserName,
                ["groupName"] = u.GroupName,
                ["totalBookings"] = u.TotalBookings,
                ["totalUsageHours"] = u.TotalUsageHours,
                ["totalDistance"] = u.TotalDistance,
                ["ownershipShare"] = u.OwnershipShare,
                ["usageShare"] = u.UsageShare,
                ["totalPaid"] = u.TotalPaid,
                ["totalOwed"] = u.TotalOwed,
                ["netBalance"] = u.NetBalance,
                ["bookingSuccessRate"] = u.BookingSuccessRate,
                ["punctualityScore"] = u.PunctualityScore
            }).ToList()
        };
    }

    private async Task<Dictionary<string, object>> GetVehicleReportData(AnalyticsRequestDto request)
    {
        var vehicleAnalytics = await _analyticsService.GetVehicleAnalyticsAsync(request);
        
        return new Dictionary<string, object>
        {
            ["reportType"] = "Vehicle Analytics",
            ["generatedAt"] = DateTime.UtcNow,
            ["period"] = $"{request.StartDate:yyyy-MM-dd} to {request.EndDate:yyyy-MM-dd}",
            ["data"] = vehicleAnalytics.Select(v => new Dictionary<string, object>
            {
                ["vehicleId"] = v.VehicleId,
                ["vehicleModel"] = v.VehicleModel,
                ["plateNumber"] = v.PlateNumber,
                ["groupName"] = v.GroupName,
                ["totalBookings"] = v.TotalBookings,
                ["totalUsageHours"] = v.TotalUsageHours,
                ["utilizationRate"] = v.UtilizationRate,
                ["revenue"] = v.Revenue,
                ["maintenanceCost"] = v.MaintenanceCost,
                ["netProfit"] = v.NetProfit,
                ["costPerKm"] = v.CostPerKm,
                ["costPerHour"] = v.CostPerHour,
                ["reliabilityScore"] = v.ReliabilityScore
            }).ToList()
        };
    }

    private async Task<Dictionary<string, object>> GetGroupReportData(AnalyticsRequestDto request)
    {
        var groupAnalytics = await _analyticsService.GetGroupAnalyticsAsync(request);
        
        return new Dictionary<string, object>
        {
            ["reportType"] = "Group Analytics",
            ["generatedAt"] = DateTime.UtcNow,
            ["period"] = $"{request.StartDate:yyyy-MM-dd} to {request.EndDate:yyyy-MM-dd}",
            ["data"] = groupAnalytics.Select(g => new Dictionary<string, object>
            {
                ["groupId"] = g.GroupId,
                ["groupName"] = g.GroupName,
                ["totalMembers"] = g.TotalMembers,
                ["activeMembers"] = g.ActiveMembers,
                ["totalRevenue"] = g.TotalRevenue,
                ["totalExpenses"] = g.TotalExpenses,
                ["netProfit"] = g.NetProfit,
                ["totalBookings"] = g.TotalBookings,
                ["totalProposals"] = g.TotalProposals,
                ["participationRate"] = g.ParticipationRate
            }).ToList()
        };
    }

    private async Task<Dictionary<string, object>> GetFinancialReportData(AnalyticsRequestDto request)
    {
        var kpiMetrics = await _analyticsService.GetKpiMetricsAsync(request);
        var trendData = await _analyticsService.GetTrendDataAsync(request);
        
        return new Dictionary<string, object>
        {
            ["reportType"] = "Financial Report",
            ["generatedAt"] = DateTime.UtcNow,
            ["period"] = $"{request.StartDate:yyyy-MM-dd} to {request.EndDate:yyyy-MM-dd}",
            ["kpiMetrics"] = kpiMetrics,
            ["trendData"] = trendData
        };
    }

    private async Task<Dictionary<string, object>> GetKpiReportData(AnalyticsRequestDto request)
    {
        var kpiMetrics = await _analyticsService.GetKpiMetricsAsync(request);
        
        return new Dictionary<string, object>
        {
            ["reportType"] = "KPI Report",
            ["generatedAt"] = DateTime.UtcNow,
            ["period"] = $"{request.StartDate:yyyy-MM-dd} to {request.EndDate:yyyy-MM-dd}",
            ["kpiMetrics"] = kpiMetrics
        };
    }

    public async Task<byte[]> GenerateFairnessReportPdfAsync(AnalyticsRequestDto request, Guid groupId)
    {
        _logger.LogInformation("Generating fairness PDF report for GroupId: {GroupId}", groupId);
        
        try
        {
            var report = await _analyticsService.GetFairnessReportAsync(
                groupId, 
                request.StartDate, 
                request.EndDate);

            // Convert report to PDF bytes
            // Note: This is a simplified implementation
            // In production, you would use a library like iTextSharp, QuestPDF, or PuppeteerSharp
            var reportContent = BuildFairnessReportText(report);
            var pdfBytes = Encoding.UTF8.GetBytes(reportContent);

            // For a proper PDF, you would use:
            // using var ms = new MemoryStream();
            // using var writer = new PdfWriter(ms);
            // using var pdf = new PdfDocument(writer);
            // using var document = new Document(pdf);
            // ... add content ...
            // document.Close();
            // return ms.ToArray();

            _logger.LogInformation("Successfully generated fairness PDF report for GroupId: {GroupId}", groupId);
            return pdfBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating fairness PDF report for GroupId: {GroupId}", groupId);
            throw;
        }
    }

    private string BuildFairnessReportText(FairnessReportDto report)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("=".PadRight(80, '='));
        sb.AppendLine($"FAIRNESS REPORT - {report.GroupName}");
        sb.AppendLine("=".PadRight(80, '='));
        sb.AppendLine($"Generated: {report.ReportDate:yyyy-MM-dd HH:mm:ss UTC}");
        sb.AppendLine($"Period: {report.PeriodStart:yyyy-MM-dd} to {report.PeriodEnd:yyyy-MM-dd}");
        sb.AppendLine();
        
        sb.AppendLine($"Overall Fairness Score: {report.OverallFairnessScore:F1}/100");
        sb.AppendLine($"Assessment: {report.OverallAssessment}");
        sb.AppendLine();
        
        sb.AppendLine("MEMBER BREAKDOWN");
        sb.AppendLine("-".PadRight(80, '-'));
        foreach (var member in report.MemberBreakdown)
        {
            sb.AppendLine($"Member: {member.MemberName}");
            sb.AppendLine($"  Ownership: {member.OwnershipPercentage:F1}%");
            sb.AppendLine($"  Usage: {member.OverallUsagePercentage:F1}%");
            sb.AppendLine($"  Difference: {member.UsageDifference:+#.##;-#.##}%");
            sb.AppendLine($"  Fair Share: {member.FairShareIndicator}");
            sb.AppendLine($"  Fairness Score: {member.FairnessScore:F1}/100");
            sb.AppendLine();
        }
        
        sb.AppendLine("RECOMMENDATIONS");
        sb.AppendLine("-".PadRight(80, '-'));
        foreach (var rec in report.Recommendations)
        {
            sb.AppendLine($"[{rec.Priority}] {rec.Title}");
            sb.AppendLine($"  {rec.Description}");
            sb.AppendLine($"  Impact: {rec.Impact}");
            sb.AppendLine();
        }
        
        sb.AppendLine("=".PadRight(80, '='));
        
        return sb.ToString();
    }
}
