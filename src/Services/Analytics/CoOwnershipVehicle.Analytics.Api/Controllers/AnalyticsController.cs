using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Analytics.Api.Services;
using CoOwnershipVehicle.Analytics.Api.Services.HttpClients;
using System.Security.Claims;

namespace CoOwnershipVehicle.Analytics.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analyticsService;
    private readonly IReportingService _reportingService;
    private readonly IGroupServiceClient _groupServiceClient;
    private readonly ILogger<AnalyticsController> _logger;

    public AnalyticsController(
        IAnalyticsService analyticsService, 
        IReportingService reportingService,
        IGroupServiceClient groupServiceClient,
        ILogger<AnalyticsController> logger)
    {
        _analyticsService = analyticsService;
        _reportingService = reportingService;
        _groupServiceClient = groupServiceClient;
        _logger = logger;
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

    /// <summary>
    /// Process analytics for a group or vehicle
    /// - Admin: Can process for any group/vehicle
    /// - User: Can only process for groups they are a member of
    /// </summary>
    [HttpPost("process")]
    public async Task<ActionResult> ProcessAnalytics([FromQuery] Guid? groupId, [FromQuery] Guid? vehicleId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var isAdmin = User.IsInRole("Admin") || User.IsInRole("SystemAdmin") || User.IsInRole("Staff");

            // If groupId is provided, verify user has access (unless admin)
            if (groupId.HasValue && !isAdmin)
            {
                var group = await _groupServiceClient.GetGroupDetailsAsync(groupId.Value);
                if (group == null)
                {
                    return NotFound(new { message = "Group not found" });
                }

                // Check if user is a member of the group
                var isMember = group.Members?.Any(m => m.UserId == userId) ?? false;
                if (!isMember)
                {
                    _logger.LogWarning("User {UserId} attempted to process analytics for group {GroupId} without membership", 
                        userId, groupId.Value);
                    return StatusCode(403, new { message = "You are not a member of this group" });
                }
            }

            var result = await _analyticsService.ProcessAnalyticsAsync(groupId, vehicleId);
            if (result)
            {
                _logger.LogInformation("Analytics processed successfully for GroupId: {GroupId}, VehicleId: {VehicleId} by User: {UserId}", 
                    groupId, vehicleId, userId);
                return Ok(new { message = "Analytics processed successfully" });
            }
            return BadRequest(new { message = "Failed to process analytics" });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt to process analytics");
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing analytics");
            return StatusCode(500, new { message = "An error occurred while processing analytics", error = ex.Message });
        }
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid user ID in token");
        }
        return userId;
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

    #region Usage vs Ownership Analytics

    /// <summary>
    /// Compare actual usage to ownership percentages for a group
    /// </summary>
    [HttpGet("usage-vs-ownership/{groupId}")]
    [ProducesResponseType(typeof(UsageVsOwnershipDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<UsageVsOwnershipDto>> GetUsageVsOwnership(
        Guid groupId, 
        [FromQuery] DateTime? startDate = null, 
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var result = await _analyticsService.GetUsageVsOwnershipAsync(groupId, startDate, endDate);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while calculating usage vs ownership analytics", error = ex.Message });
        }
    }

    /// <summary>
    /// Compare members side-by-side with detailed metrics
    /// </summary>
    [HttpGet("member-comparison/{groupId}")]
    [ProducesResponseType(typeof(MemberComparisonDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<MemberComparisonDto>> GetMemberComparison(
        Guid groupId, 
        [FromQuery] DateTime? startDate = null, 
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var result = await _analyticsService.GetMemberComparisonAsync(groupId, startDate, endDate);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while calculating member comparison", error = ex.Message });
        }
    }

    /// <summary>
    /// Get visualization data for charts (pie, bar, timeline, heat map)
    /// </summary>
    [HttpGet("visualization/{groupId}")]
    [ProducesResponseType(typeof(VisualizationDataDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<VisualizationDataDto>> GetVisualizationData(
        Guid groupId, 
        [FromQuery] DateTime? startDate = null, 
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var result = await _analyticsService.GetVisualizationDataAsync(groupId, startDate, endDate);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while generating visualization data", error = ex.Message });
        }
    }

    /// <summary>
    /// Get comprehensive fairness report with recommendations
    /// </summary>
    [HttpGet("fairness-report/{groupId}")]
    [ProducesResponseType(typeof(FairnessReportDto), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<FairnessReportDto>> GetFairnessReport(
        Guid groupId, 
        [FromQuery] DateTime? startDate = null, 
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var result = await _analyticsService.GetFairnessReportAsync(groupId, startDate, endDate);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while generating fairness report", error = ex.Message });
        }
    }

    /// <summary>
    /// Get period comparisons (month, quarter, or year)
    /// </summary>
    [HttpGet("period-comparison/{groupId}")]
    [ProducesResponseType(typeof(List<PeriodComparisonDto>), 200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<List<PeriodComparisonDto>>> GetPeriodComparison(
        Guid groupId, 
        [FromQuery] string comparisonType = "month")
    {
        try
        {
            if (!new[] { "month", "quarter", "year" }.Contains(comparisonType.ToLower()))
            {
                return BadRequest(new { message = "comparisonType must be 'month', 'quarter', or 'year'" });
            }

            var result = await _analyticsService.GetPeriodComparisonsAsync(groupId, comparisonType);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while calculating period comparisons", error = ex.Message });
        }
    }

    /// <summary>
    /// Generate PDF fairness report
    /// </summary>
    [HttpGet("fairness-report/{groupId}/pdf")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<ActionResult> GetFairnessReportPdf(
        Guid groupId, 
        [FromQuery] DateTime? startDate = null, 
        [FromQuery] DateTime? endDate = null)
    {
        try
        {
            var request = new AnalyticsRequestDto
            {
                GroupId = groupId,
                StartDate = startDate,
                EndDate = endDate
            };
            
            var pdfBytes = await _reportingService.GenerateFairnessReportPdfAsync(request, groupId);
            return File(pdfBytes, "application/pdf", $"fairness_report_{groupId}_{DateTime.UtcNow:yyyyMMdd}.pdf");
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "An error occurred while generating PDF report", error = ex.Message });
        }
    }

    #endregion
}
