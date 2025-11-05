using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Admin.Api.Services;
using CoOwnershipVehicle.Domain.Entities;
using System.Security.Claims;

namespace CoOwnershipVehicle.Admin.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(IAdminService adminService, ILogger<AdminController> logger)
    {
        _adminService = adminService;
        _logger = logger;
    }

    /// <summary>
    /// \\Description Provide admin tools for monitoring system-wide financial health and generating financial reports. Belongs to Admin Service.
    /// Financial overview: totals, sources, balances, payment KPIs, trends, top spenders, health score.
    /// </summary>
    [HttpGet("financial/overview")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    [ProducesResponseType(typeof(FinancialOverviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<FinancialOverviewDto>> GetFinancialOverview()
    {
        return await ExecuteAdminActionAsync(
            () => _adminService.GetFinancialOverviewAsync(),
            "retrieving financial overview");
    }

    /// <summary>
    /// Get revenue/expense breakdown per group with balances, flags, and compliance rates.
    /// </summary>
    [HttpGet("financial/groups")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    [ProducesResponseType(typeof(FinancialGroupBreakdownDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<FinancialGroupBreakdownDto>> GetFinancialByGroups()
    {
        return await ExecuteAdminActionAsync(
            () => _adminService.GetFinancialByGroupsAsync(),
            "retrieving financial groups breakdown");
    }

    /// <summary>
    /// Payment statistics: success/failure rates, methods, averages, trends, VNPay summary.
    /// </summary>
    [HttpGet("financial/payments")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    [ProducesResponseType(typeof(PaymentStatisticsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PaymentStatisticsDto>> GetPaymentStatistics()
    {
        return await ExecuteAdminActionAsync(
            () => _adminService.GetPaymentStatisticsAsync(),
            "retrieving payment statistics");
    }

    /// <summary>
    /// Expense analysis: totals by type, trends, averages, and optimization hints.
    /// </summary>
    [HttpGet("financial/expenses")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    [ProducesResponseType(typeof(ExpenseAnalysisDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ExpenseAnalysisDto>> GetExpenseAnalysis()
    {
        return await ExecuteAdminActionAsync(
            () => _adminService.GetExpenseAnalysisAsync(),
            "retrieving expense analysis");
    }

    /// <summary>
    /// Financial anomaly detection: unusual transactions, suspicious patterns, negative balances.
    /// </summary>
    [HttpGet("financial/anomalies")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    [ProducesResponseType(typeof(FinancialAnomaliesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<FinancialAnomaliesDto>> GetFinancialAnomalies()
    {
        return await ExecuteAdminActionAsync(
            () => _adminService.GetFinancialAnomaliesAsync(),
            "retrieving financial anomalies");
    }

    /// <summary>
    /// Generate financial report (PDF).
    /// </summary>
    [HttpGet("financial/reports/pdf")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GenerateFinancialPdf([FromQuery] FinancialReportRequestDto request)
    {
        return await ExecuteAdminFileActionAsync(async () =>
        {
            var bytes = await _adminService.GenerateFinancialPdfAsync(request);
            var fileName = $"financial_{request.Type}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";
            return File(bytes, "application/pdf", fileName);
        }, "generating financial PDF report");
    }

    /// <summary>
    /// Generate financial report (Excel).
    /// </summary>
    [HttpGet("financial/reports/excel")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GenerateFinancialExcel([FromQuery] FinancialReportRequestDto request)
    {
        return await ExecuteAdminFileActionAsync(async () =>
        {
            var bytes = await _adminService.GenerateFinancialExcelAsync(request);
            var fileName = $"financial_{request.Type}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }, "generating financial Excel report");
    }

    /// <summary>
    /// Get system-wide dashboard metrics
    /// </summary>
    /// <param name="request">Dashboard request parameters</param>
    /// <returns>Comprehensive dashboard metrics</returns>
    [HttpGet("dashboard")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    [ProducesResponseType(typeof(DashboardMetricsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DashboardMetricsDto>> GetDashboard([FromQuery] DashboardRequestDto request)
    {
        return await ExecuteAdminActionAsync(
            () => _adminService.GetDashboardMetricsAsync(request),
            "Error retrieving dashboard metrics"
        );
    }

    /// <summary>
    /// Get recent activity feed
    /// </summary>
    /// <param name="count">Number of activities to return (default: 20)</param>
    /// <returns>List of recent activities</returns>
    [HttpGet("activity")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    [ProducesResponseType(typeof(List<ActivityFeedItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<ActivityFeedItemDto>>> GetRecentActivity([FromQuery] int count = 20)
    {
        return await ExecuteAdminActionAsync(
            () => _adminService.GetRecentActivityAsync(count),
            "Error retrieving recent activity"
        );
    }

    /// <summary>
    /// Get system alerts and warnings
    /// </summary>
    /// <returns>List of system alerts</returns>
    [HttpGet("alerts")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    [ProducesResponseType(typeof(List<AlertDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<AlertDto>>> GetAlerts()
    {
        return await ExecuteAdminActionAsync(
            () => _adminService.GetAlertsAsync(),
            "Error retrieving alerts"
        );
    }

    /// <summary>
    /// Get system health status
    /// </summary>
    /// <returns>System health information</returns>
    [HttpGet("health")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    [ProducesResponseType(typeof(SystemHealthDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SystemHealthDto>> GetSystemHealth()
    {
        return await ExecuteAdminActionAsync(
            () => _adminService.GetSystemHealthAsync(),
            "Error retrieving system health"
        );
    }

    /// <summary>
    /// Export dashboard to PDF
    /// </summary>
    /// <param name="request">Dashboard request parameters</param>
    /// <returns>PDF file</returns>
    [HttpGet("export/pdf")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ExportToPdf([FromQuery] DashboardRequestDto request)
    {
        return await ExecuteAdminFileActionAsync(
            async () =>
            {
                var pdfBytes = await _adminService.ExportDashboardToPdfAsync(request);
                var fileName = $"dashboard_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";
                return File(pdfBytes, "application/pdf", fileName);
            },
            "Error exporting dashboard to PDF"
        );
    }

    /// <summary>
    /// Export dashboard to Excel
    /// </summary>
    /// <param name="request">Dashboard request parameters</param>
    /// <returns>Excel file</returns>
    [HttpGet("export/excel")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ExportToExcel([FromQuery] DashboardRequestDto request)
    {
        return await ExecuteAdminFileActionAsync(
            async () =>
            {
                var excelBytes = await _adminService.ExportDashboardToExcelAsync(request);
                var fileName = $"dashboard_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
                return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            },
            "Error exporting dashboard to Excel"
        );
    }

    /// <summary>
    /// Check if user has admin access
    /// </summary>
    /// <returns>User access information</returns>
    [HttpGet("access")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult CheckAccess()
    {
        var user = HttpContext.User;
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userRole = user.FindFirst(ClaimTypes.Role)?.Value;
        var isAdmin = user.IsInRole("SystemAdmin") || user.IsInRole("Staff");

        return Ok(new
        {
            UserId = userId,
            Role = userRole,
            HasAdminAccess = isAdmin,
            CanAccessDashboard = isAdmin
        });
    }

    /// <summary>
    /// Get paginated list of users with filtering and search
    /// </summary>
    /// <param name="request">User list request parameters</param>
    /// <returns>Paginated list of users</returns>
    [HttpGet("users")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    [ProducesResponseType(typeof(UserListResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserListResponseDto>> GetUsers([FromQuery] UserListRequestDto request)
    {
        return await ExecuteAdminActionAsync(
            () => _adminService.GetUsersAsync(request),
            "Error retrieving users"
        );
    }

    /// <summary>
    /// Get detailed information about a specific user
    /// </summary>
    /// <param name="id">User ID</param>
    /// <returns>Detailed user information</returns>
    [HttpGet("users/{id}")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    [ProducesResponseType(typeof(UserDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDetailsDto>> GetUserDetails(Guid id)
    {
        try
        {
            var result = await _adminService.GetUserDetailsAsync(id);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user details for user {UserId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving user details" });
        }
    }

    /// <summary>
    /// Update user account status (activate, deactivate, suspend, ban)
    /// </summary>
    /// <param name="id">User ID</param>
    /// <param name="request">Status update request</param>
    /// <returns>Success status</returns>
    [HttpPut("users/{id}/status")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateUserStatus(Guid id, [FromBody] UpdateUserStatusDto request)
    {
        try
        {
            var adminUserId = GetCurrentUserId();
            var result = await _adminService.UpdateUserStatusAsync(id, request, adminUserId);
            
            if (!result)
                return NotFound(new { message = "User not found" });

            return Ok(new { message = "User status updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user status for user {UserId}", id);
            return StatusCode(500, new { message = "An error occurred while updating user status" });
        }
    }

    /// <summary>
    /// Update user role
    /// </summary>
    /// <param name="id">User ID</param>
    /// <param name="request">Role update request</param>
    /// <returns>Success status</returns>
    [HttpPut("users/{id}/role")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateUserRole(Guid id, [FromBody] UpdateUserRoleDto request)
    {
        try
        {
            var adminUserId = GetCurrentUserId();
            var result = await _adminService.UpdateUserRoleAsync(id, request, adminUserId);
            
            if (!result)
                return NotFound(new { message = "User not found" });

            return Ok(new { message = "User role updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user role for user {UserId}", id);
            return StatusCode(500, new { message = "An error occurred while updating user role" });
        }
    }

    /// <summary>
    /// Get users with pending KYC reviews
    /// </summary>
    /// <returns>List of users with pending KYC</returns>
    [HttpGet("users/pending-kyc")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    [ProducesResponseType(typeof(List<PendingKycUserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<PendingKycUserDto>>> GetPendingKycUsers()
    {
        return await ExecuteAdminActionAsync(
            () => _adminService.GetPendingKycUsersAsync(),
            "Error retrieving pending KYC users"
        );
    }

    /// <summary>
    /// Get paginated list of ownership groups with filtering and search
    /// </summary>
    /// <param name="request">Group list request parameters</param>
    /// <returns>Paginated list of groups</returns>
    [HttpGet("groups")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    [ProducesResponseType(typeof(GroupListResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<GroupListResponseDto>> GetGroups([FromQuery] GroupListRequestDto request)
    {
        return await ExecuteAdminActionAsync(
            () => _adminService.GetGroupsAsync(request),
            "Error retrieving groups"
        );
    }

    /// <summary>
    /// Get detailed information about a specific group
    /// </summary>
    /// <param name="id">Group ID</param>
    /// <returns>Detailed group information</returns>
    [HttpGet("groups/{id}")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    [ProducesResponseType(typeof(GroupDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<GroupDetailsDto>> GetGroupDetails(Guid id)
    {
        try
        {
            var result = await _adminService.GetGroupDetailsAsync(id);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving group details for group {GroupId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving group details" });
        }
    }

    /// <summary>
    /// Update group status (activate, deactivate, dissolve)
    /// </summary>
    /// <param name="id">Group ID</param>
    /// <param name="request">Status update request</param>
    /// <returns>Success status</returns>
    [HttpPut("groups/{id}/status")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateGroupStatus(Guid id, [FromBody] UpdateGroupStatusDto request)
    {
        try
        {
            var adminUserId = GetCurrentUserId();
            var result = await _adminService.UpdateGroupStatusAsync(id, request, adminUserId);
            
            if (!result)
                return NotFound(new { message = "Group not found" });

            return Ok(new { message = "Group status updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating group status for group {GroupId}", id);
            return StatusCode(500, new { message = "An error occurred while updating group status" });
        }
    }

    /// <summary>
    /// Get group audit trail
    /// </summary>
    /// <param name="id">Group ID</param>
    /// <param name="request">Audit request parameters</param>
    /// <returns>Group audit trail</returns>
    [HttpGet("groups/{id}/audit")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    [ProducesResponseType(typeof(GroupAuditResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<GroupAuditResponseDto>> GetGroupAuditTrail(Guid id, [FromQuery] GroupAuditRequestDto request)
    {
        try
        {
            var result = await _adminService.GetGroupAuditTrailAsync(id, request);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving group audit trail for group {GroupId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving group audit trail" });
        }
    }

    /// <summary>
    /// Intervene in group operations (freeze, message, appoint admin)
    /// </summary>
    /// <param name="id">Group ID</param>
    /// <param name="request">Intervention request</param>
    /// <returns>Success status</returns>
    [HttpPost("groups/{id}/intervene")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> InterveneInGroup(Guid id, [FromBody] GroupInterventionDto request)
    {
        try
        {
            var adminUserId = GetCurrentUserId();
            var result = await _adminService.InterveneInGroupAsync(id, request, adminUserId);
            
            if (!result)
                return NotFound(new { message = "Group not found" });

            return Ok(new { message = "Group intervention completed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error intervening in group {GroupId}", id);
            return StatusCode(500, new { message = "An error occurred while intervening in group" });
        }
    }

    /// <summary>
    /// Get group health status and recommendations
    /// </summary>
    /// <param name="id">Group ID</param>
    /// <returns>Group health information</returns>
    [HttpGet("groups/{id}/health")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    [ProducesResponseType(typeof(GroupHealthDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<GroupHealthDto>> GetGroupHealth(Guid id)
    {
        try
        {
            var result = await _adminService.GetGroupHealthAsync(id);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving group health for group {GroupId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving group health" });
        }
    }

    private async Task<ActionResult<T>> ExecuteAdminActionAsync<T>(Func<Task<T>> action, string errorMessage)
    {
        try
        {
            var result = await action();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, errorMessage);
            return StatusCode(500, new { message = $"An error occurred while {errorMessage.ToLower()}" });
        }
    }

    private async Task<IActionResult> ExecuteAdminFileActionAsync(Func<Task<IActionResult>> action, string errorMessage)
    {
        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, errorMessage);
            return StatusCode(500, new { message = $"An error occurred while {errorMessage.ToLower()}" });
        }
    }

    // Dispute Management Endpoints
    [HttpPost("disputes")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    public async Task<ActionResult<Guid>> CreateDispute([FromBody] CreateDisputeDto request)
    {
        return await ExecuteAdminActionAsync(async () =>
        {
            var adminUserId = GetCurrentUserId();
            var disputeId = await _adminService.CreateDisputeAsync(request, adminUserId);
            return disputeId;
        }, "creating dispute");
    }

    [HttpGet("disputes")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    public async Task<ActionResult<DisputeListResponseDto>> GetDisputes([FromQuery] DisputeListRequestDto request)
    {
        return await ExecuteAdminActionAsync(async () =>
        {
            return await _adminService.GetDisputesAsync(request);
        }, "retrieving disputes");
    }

    [HttpGet("disputes/{id}")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    public async Task<ActionResult<DisputeDetailsDto>> GetDisputeDetails(Guid id)
    {
        return await ExecuteAdminActionAsync(async () =>
        {
            return await _adminService.GetDisputeDetailsAsync(id);
        }, "retrieving dispute details");
    }

    [HttpPut("disputes/{id}/assign")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    public async Task<ActionResult<object>> AssignDispute(Guid id, [FromBody] AssignDisputeDto request)
    {
        return await ExecuteAdminActionAsync(async () =>
        {
            var adminUserId = GetCurrentUserId();
            var success = await _adminService.AssignDisputeAsync(id, request, adminUserId);
            return new { message = "Dispute assigned successfully" };
        }, "assigning dispute");
    }

    [HttpPost("disputes/{id}/comment")]
    [Authorize(Roles = "SystemAdmin,Staff,GroupAdmin,CoOwner")]
    public async Task<ActionResult<object>> AddDisputeComment(Guid id, [FromBody] AddDisputeCommentDto request)
    {
        return await ExecuteAdminActionAsync(async () =>
        {
            var userId = GetCurrentUserId();
            var success = await _adminService.AddDisputeCommentAsync(id, request, userId);
            return new { message = "Comment added successfully" };
        }, "adding dispute comment");
    }

    [HttpPut("disputes/{id}/resolve")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    public async Task<ActionResult<object>> ResolveDispute(Guid id, [FromBody] ResolveDisputeDto request)
    {
        return await ExecuteAdminActionAsync(async () =>
        {
            var adminUserId = GetCurrentUserId();
            var success = await _adminService.ResolveDisputeAsync(id, request, adminUserId);
            return new { message = "Dispute resolved successfully" };
        }, "resolving dispute");
    }

    [HttpGet("disputes/statistics")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    public async Task<ActionResult<DisputeStatisticsDto>> GetDisputeStatistics()
    {
        return await ExecuteAdminActionAsync(async () =>
        {
            return await _adminService.GetDisputeStatisticsAsync();
        }, "retrieving dispute statistics");
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("Unable to determine current user ID");
        }
        return userId;
    }
}

/// <summary>
/// System health monitoring and metrics controller
/// </summary>
[ApiController]
[Route("api/admin/system")]
[Authorize(Roles = "SystemAdmin,Staff")]
public class SystemMonitoringController : ControllerBase
{
    private readonly ISystemHealthService _healthService;
    private readonly ISystemMetricsService _metricsService;
    private readonly ISystemLogsService _logsService;
    private readonly IAlertService _alertService;
    private readonly ILogger<SystemMonitoringController> _logger;

    public SystemMonitoringController(
        ISystemHealthService healthService,
        ISystemMetricsService metricsService,
        ISystemLogsService logsService,
        IAlertService alertService,
        ILogger<SystemMonitoringController> logger)
    {
        _healthService = healthService;
        _metricsService = metricsService;
        _logsService = logsService;
        _alertService = alertService;
        _logger = logger;
    }

    /// <summary>
    /// System health check - checks status of all microservices and dependencies
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(typeof(SystemHealthCheckDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SystemHealthCheckDto>> GetSystemHealth()
    {
        try
        {
            var health = await _healthService.CheckSystemHealthAsync();
            return Ok(health);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving system health");
            return StatusCode(500, new { message = "An error occurred while checking system health" });
        }
    }

    /// <summary>
    /// System metrics - performance metrics for all services
    /// </summary>
    [HttpGet("metrics")]
    [ProducesResponseType(typeof(SystemMetricsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SystemMetricsDto>> GetSystemMetrics([FromQuery] int? minutes = 15)
    {
        try
        {
            var period = TimeSpan.FromMinutes(minutes ?? 15);
            var metrics = await _metricsService.GetSystemMetricsAsync(period);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving system metrics");
            return StatusCode(500, new { message = "An error occurred while retrieving system metrics" });
        }
    }

    /// <summary>
    /// System logs - centralized log viewing with filtering and search
    /// </summary>
    [HttpGet("logs")]
    [ProducesResponseType(typeof(SystemLogsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SystemLogsResponseDto>> GetSystemLogs([FromQuery] SystemLogsRequestDto request)
    {
        try
        {
            var logs = await _logsService.GetLogsAsync(request);
            return Ok(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving system logs");
            return StatusCode(500, new { message = "An error occurred while retrieving system logs" });
        }
    }

    /// <summary>
    /// Export system logs
    /// </summary>
    [HttpGet("logs/export")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ExportLogs([FromQuery] SystemLogsRequestDto request, [FromQuery] string format = "json")
    {
        try
        {
            var logs = await _logsService.ExportLogsAsync(request, format);
            var contentType = format.ToLower() == "csv" ? "text/csv" : "application/json";
            var fileName = $"system_logs_{DateTime.UtcNow:yyyyMMdd_HHmmss}.{format}";
            
            return File(logs, contentType, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting system logs");
            return StatusCode(500, new { message = "An error occurred while exporting system logs" });
        }
    }

    /// <summary>
    /// Get active system alerts
    /// </summary>
    [HttpGet("alerts")]
    [ProducesResponseType(typeof(List<AlertDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<AlertDto>>> GetSystemAlerts()
    {
        try
        {
            var alerts = await _alertService.GetActiveAlertsAsync();
            return Ok(alerts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving system alerts");
            return StatusCode(500, new { message = "An error occurred while retrieving system alerts" });
        }
    }

    /// <summary>
    /// Trigger alert check (manually trigger alert evaluation)
    /// </summary>
    [HttpPost("alerts/check")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CheckAlerts()
    {
        try
        {
            await _alertService.CheckAndTriggerAlertsAsync();
            return Ok(new { message = "Alert check completed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking alerts");
            return StatusCode(500, new { message = "An error occurred while checking alerts" });
        }
    }
}
