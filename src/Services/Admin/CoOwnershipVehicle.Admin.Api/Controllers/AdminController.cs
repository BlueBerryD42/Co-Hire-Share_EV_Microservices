using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Admin.Api.Services;
using CoOwnershipVehicle.Domain.Entities;
using System.Security.Claims;
using System.IO;

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
        if (!UserHasAnyRole("SystemAdmin", "Staff"))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        return await ExecuteAdminActionAsync(
            () => _adminService.GetFinancialOverviewAsync(),
            "retrieving financial overview",
            "SystemAdmin", "Staff");
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
        if (!UserHasAnyRole("SystemAdmin", "Staff"))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        return await ExecuteAdminActionAsync(
            () => _adminService.GetFinancialByGroupsAsync(),
            "retrieving financial groups breakdown",
            "SystemAdmin", "Staff");
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
        if (!UserHasAnyRole("SystemAdmin", "Staff"))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        return await ExecuteAdminActionAsync(
            () => _adminService.GetPaymentStatisticsAsync(),
            "retrieving payment statistics",
            "SystemAdmin", "Staff");
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
            "retrieving expense analysis",
            "SystemAdmin", "Staff");
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
            "retrieving financial anomalies",
            "SystemAdmin", "Staff");
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
        }, "generating financial PDF report", "SystemAdmin", "Staff");
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
        }, "generating financial Excel report", "SystemAdmin", "Staff");
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
        if (!UserHasAnyRole("SystemAdmin", "Staff"))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        return await ExecuteAdminActionAsync(
            () => _adminService.GetDashboardMetricsAsync(request),
            "Error retrieving dashboard metrics",
            "SystemAdmin", "Staff"
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
            "Error retrieving recent activity",
            "SystemAdmin", "Staff"
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
            "Error retrieving alerts",
            "SystemAdmin", "Staff"
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
            "Error retrieving system health",
            "SystemAdmin", "Staff"
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
            "Error exporting dashboard to PDF",
            "SystemAdmin", "Staff"
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
            "Error exporting dashboard to Excel",
            "SystemAdmin", "Staff"
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
        if (!UserHasAnyRole("SystemAdmin", "Staff"))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        return await ExecuteAdminActionAsync(
            () => _adminService.GetUsersAsync(request),
            "Error retrieving users",
            "SystemAdmin", "Staff"
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
        if (!UserHasAnyRole("SystemAdmin", "Staff"))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

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
            if (!UserHasAnyRole("SystemAdmin", "Staff"))
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }

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
            if (!UserHasAnyRole("SystemAdmin", "Staff"))
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }

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
    /// <param name="filter">Filter parameters for KYC documents</param>
    /// <returns>Paginated list of users with pending KYC</returns>
    [HttpGet("users/pending-kyc")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    [ProducesResponseType(typeof(PendingKycUserListResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PendingKycUserListResponseDto>> GetPendingKycUsers([FromQuery] KycDocumentFilterDto? filter)
    {
        return await ExecuteAdminActionAsync(
            () => _adminService.GetPendingKycUsersAsync(filter),
            "Error retrieving pending KYC users",
            "SystemAdmin", "Staff"
        );
    }

    /// <summary>
    /// Get KYC document details
    /// </summary>
    /// <param name="documentId">Document ID</param>
    /// <returns>KYC document details</returns>
    [HttpGet("kyc/documents/{documentId:guid}")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    [ProducesResponseType(typeof(KycDocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<KycDocumentDto>> GetKycDocumentDetails(Guid documentId)
    {
        try
        {
            if (!UserHasAnyRole("SystemAdmin", "Staff"))
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }

            var document = await _adminService.GetKycDocumentDetailsAsync(documentId);
            return Ok(document);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving KYC document {DocumentId}", documentId);
            return StatusCode(500, new { message = "An error occurred while retrieving KYC document" });
        }
    }

    /// <summary>
    /// Download KYC document file
    /// </summary>
    /// <param name="documentId">Document ID</param>
    /// <returns>Document file</returns>
    [HttpGet("kyc/documents/{documentId:guid}/download")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DownloadKycDocument(Guid documentId)
    {
        try
        {
            if (!UserHasAnyRole("SystemAdmin", "Staff"))
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }

            var fileBytes = await _adminService.DownloadKycDocumentAsync(documentId);
            var document = await _adminService.GetKycDocumentDetailsAsync(documentId);
            var fileName = document.FileName;
            var contentType = GetContentType(fileName);

            return File(fileBytes, contentType, fileName);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading KYC document {DocumentId}", documentId);
            return StatusCode(500, new { message = "An error occurred while downloading KYC document" });
        }
    }

    /// <summary>
    /// Bulk review multiple KYC documents
    /// </summary>
    /// <param name="request">Bulk review request</param>
    /// <returns>Success status</returns>
    [HttpPost("kyc/documents/bulk-review")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> BulkReviewKycDocuments([FromBody] BulkReviewKycDocumentsDto request)
    {
        try
        {
            if (!UserHasAnyRole("SystemAdmin", "Staff"))
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var adminUserId = GetCurrentUserId();
            var success = await _adminService.BulkReviewKycDocumentsAsync(request, adminUserId);

            if (!success)
                return BadRequest(new { message = "Failed to review documents" });

            return Ok(new { message = "Documents reviewed successfully" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk reviewing KYC documents");
            return StatusCode(500, new { message = "An error occurred while reviewing documents" });
        }
    }

    /// <summary>
    /// Get KYC review statistics
    /// </summary>
    /// <returns>KYC review statistics</returns>
    [HttpGet("kyc/statistics")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    [ProducesResponseType(typeof(KycReviewStatisticsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<KycReviewStatisticsDto>> GetKycStatistics()
    {
        return await ExecuteAdminActionAsync(
            () => _adminService.GetKycStatisticsAsync(),
            "Error retrieving KYC statistics",
            "SystemAdmin", "Staff"
        );
    }

    private string GetContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".tiff" or ".tif" => "image/tiff",
            _ => "application/octet-stream"
        };
    }

    /// <summary>
    /// Review a user's KYC document
    /// </summary>
    [HttpPost("kyc/documents/{documentId:guid}/review")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    [ProducesResponseType(typeof(KycDocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<KycDocumentDto>> ReviewKycDocument(Guid documentId, [FromBody] ReviewKycDocumentDto request)
    {
        try
        {
            if (!UserHasAnyRole("SystemAdmin", "Staff"))
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var adminUserId = GetCurrentUserId();
            var document = await _adminService.ReviewKycDocumentAsync(documentId, request, adminUserId);
            return Ok(document);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reviewing KYC document {DocumentId}", documentId);
            return StatusCode(500, new { message = "An error occurred while reviewing KYC document" });
        }
    }

    /// <summary>
    /// Update user's overall KYC status
    /// </summary>
    [HttpPut("users/{id:guid}/kyc-status")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateUserKycStatus(Guid id, [FromBody] UpdateUserKycStatusDto request)
    {
        try
        {
            if (!UserHasAnyRole("SystemAdmin", "Staff"))
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var adminUserId = GetCurrentUserId();
            var updated = await _adminService.UpdateUserKycStatusAsync(id, request, adminUserId);

            if (!updated)
                return NotFound(new { message = "User not found" });

            return Ok(new { message = "KYC status updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating KYC status for user {UserId}", id);
            return StatusCode(500, new { message = "An error occurred while updating user KYC status" });
        }
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
        if (!UserHasAnyRole("SystemAdmin", "Staff"))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        return await ExecuteAdminActionAsync(
            () => _adminService.GetGroupsAsync(request),
            "Error retrieving groups",
            "SystemAdmin", "Staff"
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
            if (!UserHasAnyRole("SystemAdmin", "Staff"))
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }

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
            if (!UserHasAnyRole("SystemAdmin", "Staff"))
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }

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
            if (!UserHasAnyRole("SystemAdmin", "Staff"))
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }

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
    /// Get system-wide audit logs with filtering and pagination
    /// </summary>
    /// <param name="request">Audit log request parameters</param>
    /// <returns>Paginated audit logs</returns>
    [HttpGet("audit")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    [ProducesResponseType(typeof(AuditLogResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuditLogResponseDto>> GetAuditLogs([FromQuery] AuditLogRequestDto request)
    {
        if (!UserHasAnyRole("SystemAdmin", "Staff"))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        return await ExecuteAdminActionAsync(
            () => _adminService.GetAuditLogsAsync(request),
            "retrieving audit logs",
            "SystemAdmin", "Staff"
        );
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
            if (!UserHasAnyRole("SystemAdmin", "Staff"))
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }

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
            if (!UserHasAnyRole("SystemAdmin", "Staff"))
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }

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

    private async Task<ActionResult<T>> ExecuteAdminActionAsync<T>(Func<Task<T>> action, string errorMessage, params string[] requiredRoles)
    {
        try
        {
            if (requiredRoles.Length > 0 && !UserHasAnyRole(requiredRoles))
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await action();
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, errorMessage);
            return StatusCode(500, new { message = $"An error occurred while {errorMessage.ToLower()}" });
        }
    }

    private async Task<IActionResult> ExecuteAdminFileActionAsync(Func<Task<IActionResult>> action, string errorMessage, params string[] requiredRoles)
    {
        try
        {
            if (requiredRoles.Length > 0 && !UserHasAnyRole(requiredRoles))
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }

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
        }, "creating dispute", "SystemAdmin", "Staff");
    }

    [HttpGet("disputes")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    public async Task<ActionResult<DisputeListResponseDto>> GetDisputes([FromQuery] DisputeListRequestDto request)
    {
        if (!UserHasAnyRole("SystemAdmin", "Staff"))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        return await ExecuteAdminActionAsync(async () =>
        {
            return await _adminService.GetDisputesAsync(request);
        }, "retrieving disputes", "SystemAdmin", "Staff");
    }

    [HttpGet("disputes/{id}")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    public async Task<ActionResult<DisputeDetailsDto>> GetDisputeDetails(Guid id)
    {
        return await ExecuteAdminActionAsync(async () =>
        {
            return await _adminService.GetDisputeDetailsAsync(id);
        }, "retrieving dispute details", "SystemAdmin", "Staff");
    }

    [HttpPut("disputes/{id}/assign")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    public async Task<ActionResult<object>> AssignDispute(Guid id, [FromBody] AssignDisputeDto request)
    {
        if (!UserHasAnyRole("SystemAdmin", "Staff"))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        try
        {
            var adminUserId = GetCurrentUserId();
            var success = await _adminService.AssignDisputeAsync(id, request, adminUserId);
            if (!success)
            {
                return NotFound(new { message = "Dispute not found" });
            }

            return Ok(new { message = "Dispute assigned successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning dispute {DisputeId}", id);
            return StatusCode(500, new { message = "An error occurred while assigning dispute" });
        }
    }

    [HttpPost("disputes/{id}/comment")]
    [Authorize(Roles = "SystemAdmin,Staff,GroupAdmin,CoOwner")]
    public async Task<ActionResult<object>> AddDisputeComment(Guid id, [FromBody] AddDisputeCommentDto request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var success = await _adminService.AddDisputeCommentAsync(id, request, userId);

            if (!success)
            {
                return new NotFoundObjectResult(new { message = "Dispute not found" });
            }

            return new OkObjectResult(new { message = "Comment added successfully" });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding dispute comment for dispute {DisputeId}", id);
            return StatusCode(500, new { message = "An error occurred while adding dispute comment" });
        }
    }

    [HttpPut("disputes/{id}/resolve")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    public async Task<ActionResult<object>> ResolveDispute(Guid id, [FromBody] ResolveDisputeDto request)
    {
        if (!UserHasAnyRole("SystemAdmin", "Staff"))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        try
        {
            var adminUserId = GetCurrentUserId();
            var success = await _adminService.ResolveDisputeAsync(id, request, adminUserId);
            return new OkObjectResult(new { message = "Dispute resolved successfully" });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving dispute {DisputeId}", id);
            return StatusCode(500, new { message = "An error occurred while resolving dispute" });
        }
    }

    [HttpGet("disputes/statistics")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    public async Task<ActionResult<DisputeStatisticsDto>> GetDisputeStatistics()
    {
        return await ExecuteAdminActionAsync(async () =>
        {
            return await _adminService.GetDisputeStatisticsAsync();
        }, "retrieving dispute statistics", "SystemAdmin", "Staff");
    }

    // Check-In Management Endpoints
    /// <summary>
    /// Get paginated list of check-ins with filtering
    /// </summary>
    /// <param name="request">Check-in list request parameters</param>
    /// <returns>Paginated list of check-ins</returns>
    [HttpGet("checkins")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    [ProducesResponseType(typeof(CheckInListResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CheckInListResponseDto>> GetCheckIns([FromQuery] CheckInListRequestDto request)
    {
        if (!UserHasAnyRole("SystemAdmin", "Staff"))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        return await ExecuteAdminActionAsync(
            () => _adminService.GetCheckInsAsync(request),
            "retrieving check-ins",
            "SystemAdmin", "Staff"
        );
    }

    /// <summary>
    /// Get detailed information about a specific check-in
    /// </summary>
    /// <param name="checkInId">Check-in ID</param>
    /// <returns>Detailed check-in information</returns>
    [HttpGet("checkins/{checkInId}")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    [ProducesResponseType(typeof(CheckInSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CheckInSummaryDto>> GetCheckInDetails(Guid checkInId)
    {
        try
        {
            if (!UserHasAnyRole("SystemAdmin", "Staff"))
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await _adminService.GetCheckInDetailsAsync(checkInId);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving check-in details for {CheckInId}", checkInId);
            return StatusCode(500, new { message = "An error occurred while retrieving check-in details" });
        }
    }

    /// <summary>
    /// Approve a check-in
    /// </summary>
    /// <param name="checkInId">Check-in ID</param>
    /// <param name="request">Approve request</param>
    /// <returns>Success status</returns>
    [HttpPost("checkins/{checkInId}/approve")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ApproveCheckIn(Guid checkInId, [FromBody] ApproveCheckInDto request)
    {
        try
        {
            if (!UserHasAnyRole("SystemAdmin", "Staff"))
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }

            var adminUserId = GetCurrentUserId();
            var result = await _adminService.ApproveCheckInAsync(checkInId, request, adminUserId);
            
            if (!result)
                return NotFound(new { message = "Check-in not found" });

            return Ok(new { message = "Check-in approved successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving check-in {CheckInId}", checkInId);
            return StatusCode(500, new { message = "An error occurred while approving check-in" });
        }
    }

    /// <summary>
    /// Reject a check-in
    /// </summary>
    /// <param name="checkInId">Check-in ID</param>
    /// <param name="request">Reject request</param>
    /// <returns>Success status</returns>
    [HttpPost("checkins/{checkInId}/reject")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RejectCheckIn(Guid checkInId, [FromBody] RejectCheckInDto request)
    {
        try
        {
            if (!UserHasAnyRole("SystemAdmin", "Staff"))
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var adminUserId = GetCurrentUserId();
            var result = await _adminService.RejectCheckInAsync(checkInId, request, adminUserId);
            
            if (!result)
                return NotFound(new { message = "Check-in not found" });

            return Ok(new { message = "Check-in rejected successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting check-in {CheckInId}", checkInId);
            return StatusCode(500, new { message = "An error occurred while rejecting check-in" });
        }
    }

    // Maintenance Management Endpoints
    /// <summary>
    /// Get paginated list of maintenance records with filtering
    /// </summary>
    /// <param name="request">Maintenance list request parameters</param>
    /// <returns>Paginated list of maintenance records</returns>
    [HttpGet("maintenance")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    [ProducesResponseType(typeof(MaintenanceListResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<MaintenanceListResponseDto>> GetMaintenance([FromQuery] MaintenanceListRequestDto request)
    {
        if (!UserHasAnyRole("SystemAdmin", "Staff"))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        return await ExecuteAdminActionAsync(
            () => _adminService.GetMaintenanceAsync(request),
            "retrieving maintenance records",
            "SystemAdmin", "Staff"
        );
    }

    /// <summary>
    /// Get detailed information about a specific maintenance record
    /// </summary>
    /// <param name="maintenanceId">Maintenance record ID</param>
    /// <returns>Detailed maintenance information</returns>
    [HttpGet("maintenance/{maintenanceId}")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    [ProducesResponseType(typeof(MaintenanceSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<MaintenanceSummaryDto>> GetMaintenanceDetails(Guid maintenanceId)
    {
        try
        {
            if (!UserHasAnyRole("SystemAdmin", "Staff"))
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }

            var result = await _adminService.GetMaintenanceDetailsAsync(maintenanceId);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance details for {MaintenanceId}", maintenanceId);
            return StatusCode(500, new { message = "An error occurred while retrieving maintenance details" });
        }
    }

    /// <summary>
    /// Create a new maintenance record
    /// </summary>
    /// <param name="request">Create maintenance request</param>
    /// <returns>Success status</returns>
    [HttpPost("maintenance")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateMaintenance([FromBody] CreateMaintenanceDto request)
    {
        try
        {
            if (!UserHasAnyRole("SystemAdmin", "Staff"))
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var adminUserId = GetCurrentUserId();
            var result = await _adminService.CreateMaintenanceAsync(request, adminUserId);
            
            if (!result)
                return BadRequest(new { message = "Failed to create maintenance record" });

            return Ok(new { message = "Maintenance record created successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating maintenance record");
            return StatusCode(500, new { message = "An error occurred while creating maintenance record" });
        }
    }

    /// <summary>
    /// Update a maintenance record
    /// </summary>
    /// <param name="maintenanceId">Maintenance record ID</param>
    /// <param name="request">Update maintenance request</param>
    /// <returns>Success status</returns>
    [HttpPut("maintenance/{maintenanceId}")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateMaintenance(Guid maintenanceId, [FromBody] UpdateMaintenanceDto request)
    {
        try
        {
            if (!UserHasAnyRole("SystemAdmin", "Staff"))
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var adminUserId = GetCurrentUserId();
            var result = await _adminService.UpdateMaintenanceAsync(maintenanceId, request, adminUserId);
            
            if (!result)
                return NotFound(new { message = "Maintenance record not found" });

            return Ok(new { message = "Maintenance record updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating maintenance record {MaintenanceId}", maintenanceId);
            return StatusCode(500, new { message = "An error occurred while updating maintenance record" });
        }
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

    private bool UserHasAnyRole(params string[] roles)
    {
        if (roles == null || roles.Length == 0)
        {
            return true;
        }

        return roles.Any(role => HttpContext.User?.IsInRole(role) == true);
    }

    // Vehicle Management Endpoints
    /// <summary>
    /// Get paginated list of vehicles with filtering
    /// </summary>
    /// <param name="request">Vehicle list request parameters</param>
    /// <returns>Paginated list of vehicles</returns>
    [HttpGet("vehicles")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    [ProducesResponseType(typeof(VehicleListResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<VehicleListResponseDto>> GetVehicles([FromQuery] VehicleListRequestDto request)
    {
        if (!UserHasAnyRole("SystemAdmin", "Staff"))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        return await ExecuteAdminActionAsync(
            () => _adminService.GetVehiclesAsync(request),
            "retrieving vehicles",
            "SystemAdmin", "Staff"
        );
    }

    /// <summary>
    /// Get detailed information about a specific vehicle
    /// </summary>
    /// <param name="vehicleId">Vehicle ID</param>
    /// <returns>Detailed vehicle information</returns>
    [HttpGet("vehicles/{vehicleId}")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    [ProducesResponseType(typeof(VehicleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<VehicleDto>> GetVehicleDetails(Guid vehicleId)
    {
        try
        {
            if (!UserHasAnyRole("SystemAdmin", "Staff"))
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }

            var vehicle = await _adminService.GetVehicleDetailsAsync(vehicleId);
            if (vehicle == null)
                return NotFound(new { message = "Vehicle not found" });

            return Ok(vehicle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving vehicle details for {VehicleId}", vehicleId);
            return StatusCode(500, new { message = "An error occurred while retrieving vehicle details" });
        }
    }

    /// <summary>
    /// Update vehicle status
    /// </summary>
    /// <param name="vehicleId">Vehicle ID</param>
    /// <param name="request">Update vehicle status request</param>
    /// <returns>Success status</returns>
    [HttpPut("vehicles/{vehicleId}/status")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateVehicleStatus(Guid vehicleId, [FromBody] UpdateVehicleStatusDto request)
    {
        try
        {
            if (!UserHasAnyRole("SystemAdmin", "Staff"))
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var adminUserId = GetCurrentUserId();
            var result = await _adminService.UpdateVehicleStatusAsync(vehicleId, request, adminUserId);
            
            if (!result)
                return NotFound(new { message = "Vehicle not found" });

            return Ok(new { message = "Vehicle status updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating vehicle status for {VehicleId}", vehicleId);
            return StatusCode(500, new { message = "An error occurred while updating vehicle status" });
        }
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
