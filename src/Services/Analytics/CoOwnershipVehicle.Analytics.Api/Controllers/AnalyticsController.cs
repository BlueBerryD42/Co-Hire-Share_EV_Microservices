using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Analytics.Api.Services;
using System.Security.Claims;

namespace CoOwnershipVehicle.Analytics.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analyticsService;
    private readonly IReportingService _reportingService;

    public AnalyticsController(IAnalyticsService analyticsService, IReportingService reportingService)
    {
        _analyticsService = analyticsService;
        _reportingService = reportingService;
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<AnalyticsDashboardDto>> GetDashboard([FromQuery] AnalyticsRequestDto request)
    {
        var dashboard = await _analyticsService.GetDashboardAsync(request);
        return Ok(dashboard);
    }

    [HttpGet("snapshots")]
    public async Task<ActionResult<List<AnalyticsSnapshotDto>>> GetSnapshots([FromQuery] AnalyticsRequestDto request)
    {
        var snapshots = await _analyticsService.GetSnapshotsAsync(request);
        return Ok(snapshots);
    }

    [HttpGet("users")]
    public async Task<ActionResult<List<UserAnalyticsDto>>> GetUserAnalytics([FromQuery] AnalyticsRequestDto request)
    {
        var userAnalytics = await _analyticsService.GetUserAnalyticsAsync(request);
        return Ok(userAnalytics);
    }

    [HttpGet("vehicles")]
    public async Task<ActionResult<List<VehicleAnalyticsDto>>> GetVehicleAnalytics([FromQuery] AnalyticsRequestDto request)
    {
        var vehicleAnalytics = await _analyticsService.GetVehicleAnalyticsAsync(request);
        return Ok(vehicleAnalytics);
    }

    [HttpGet("groups")]
    public async Task<ActionResult<List<GroupAnalyticsDto>>> GetGroupAnalytics([FromQuery] AnalyticsRequestDto request)
    {
        var groupAnalytics = await _analyticsService.GetGroupAnalyticsAsync(request);
        return Ok(groupAnalytics);
    }

    [HttpGet("kpi")]
    public async Task<ActionResult<Dictionary<string, object>>> GetKpiMetrics([FromQuery] AnalyticsRequestDto request)
    {
        var kpiMetrics = await _analyticsService.GetKpiMetricsAsync(request);
        return Ok(kpiMetrics);
    }

    [HttpGet("trends")]
    public async Task<ActionResult<List<Dictionary<string, object>>>> GetTrendData([FromQuery] AnalyticsRequestDto request)
    {
        var trendData = await _analyticsService.GetTrendDataAsync(request);
        return Ok(trendData);
    }

    [HttpPost("snapshots")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AnalyticsSnapshotDto>> CreateSnapshot([FromBody] CreateAnalyticsSnapshotDto dto)
    {
        var snapshot = await _analyticsService.CreateSnapshotAsync(dto);
        return CreatedAtAction(nameof(GetSnapshots), new { id = snapshot.Id }, snapshot);
    }

    [HttpPost("process")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> ProcessAnalytics([FromQuery] Guid? groupId, [FromQuery] Guid? vehicleId)
    {
        var result = await _analyticsService.ProcessAnalyticsAsync(groupId, vehicleId);
        if (result)
        {
            return Ok(new { message = "Analytics processed successfully" });
        }
        return BadRequest(new { message = "Failed to process analytics" });
    }

    [HttpGet("reports/{reportType}")]
    public async Task<ActionResult> GetReport(string reportType, [FromQuery] AnalyticsRequestDto request)
    {
        try
        {
            var reportData = await _reportingService.GetReportDataAsync(request, reportType);
            return Ok(reportData);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("reports/{reportType}/pdf")]
    public async Task<ActionResult> GetPdfReport(string reportType, [FromQuery] AnalyticsRequestDto request)
    {
        try
        {
            var pdfBytes = await _reportingService.GeneratePdfReportAsync(request, reportType);
            return File(pdfBytes, "application/pdf", $"{reportType}_report_{DateTime.UtcNow:yyyyMMdd}.pdf");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("reports/{reportType}/excel")]
    public async Task<ActionResult> GetExcelReport(string reportType, [FromQuery] AnalyticsRequestDto request)
    {
        try
        {
            var excelBytes = await _reportingService.GenerateExcelReportAsync(request, reportType);
            return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 
                $"{reportType}_report_{DateTime.UtcNow:yyyyMMdd}.xlsx");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
