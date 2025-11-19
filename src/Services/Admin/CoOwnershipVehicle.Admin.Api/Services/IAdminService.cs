using CoOwnershipVehicle.Shared.Contracts.DTOs;

namespace CoOwnershipVehicle.Admin.Api.Services;

public interface IAdminService
{
    Task<DashboardMetricsDto> GetDashboardMetricsAsync(DashboardRequestDto request);
    Task<byte[]> ExportDashboardToPdfAsync(DashboardRequestDto request);
    Task<byte[]> ExportDashboardToExcelAsync(DashboardRequestDto request);
    Task<List<ActivityFeedItemDto>> GetRecentActivityAsync(int count = 20);
    Task<List<AlertDto>> GetAlertsAsync();
    Task<SystemHealthDto> GetSystemHealthAsync();
    
    // User Management Methods
    Task<UserListResponseDto> GetUsersAsync(UserListRequestDto request);
    Task<UserDetailsDto> GetUserDetailsAsync(Guid userId);
    Task<bool> UpdateUserStatusAsync(Guid userId, UpdateUserStatusDto request, Guid adminUserId);
    Task<bool> UpdateUserRoleAsync(Guid userId, UpdateUserRoleDto request, Guid adminUserId);
    Task<PendingKycUserListResponseDto> GetPendingKycUsersAsync(KycDocumentFilterDto? filter = null);
    Task<KycDocumentDto> GetKycDocumentDetailsAsync(Guid documentId);
    Task<byte[]> DownloadKycDocumentAsync(Guid documentId);
    Task<KycDocumentDto> ReviewKycDocumentAsync(Guid documentId, ReviewKycDocumentDto request, Guid adminUserId);
    Task<bool> BulkReviewKycDocumentsAsync(BulkReviewKycDocumentsDto request, Guid adminUserId);
    Task<KycReviewStatisticsDto> GetKycStatisticsAsync();
    Task<bool> UpdateUserKycStatusAsync(Guid userId, UpdateUserKycStatusDto request, Guid adminUserId);
    
    // Group Management Methods
    Task<GroupListResponseDto> GetGroupsAsync(GroupListRequestDto request);
    Task<GroupDetailsDto> GetGroupDetailsAsync(Guid groupId);
    Task<bool> UpdateGroupStatusAsync(Guid groupId, UpdateGroupStatusDto request, Guid adminUserId);
    Task<GroupAuditResponseDto> GetGroupAuditTrailAsync(Guid groupId, GroupAuditRequestDto request);
    Task<bool> InterveneInGroupAsync(Guid groupId, GroupInterventionDto request, Guid adminUserId);
        Task<GroupHealthDto> GetGroupHealthAsync(Guid groupId);
    Task<AuditLogResponseDto> GetAuditLogsAsync(AuditLogRequestDto request);

        // Dispute Management Methods
        Task<Guid> CreateDisputeAsync(CreateDisputeDto request, Guid adminUserId);
        Task<DisputeListResponseDto> GetDisputesAsync(DisputeListRequestDto request);
        Task<DisputeDetailsDto> GetDisputeDetailsAsync(Guid disputeId);
        Task<bool> AssignDisputeAsync(Guid disputeId, AssignDisputeDto request, Guid adminUserId);
        Task<bool> AddDisputeCommentAsync(Guid disputeId, AddDisputeCommentDto request, Guid userId);
        Task<bool> ResolveDisputeAsync(Guid disputeId, ResolveDisputeDto request, Guid adminUserId);
        Task<DisputeStatisticsDto> GetDisputeStatisticsAsync();

    // Financial endpoints
    Task<FinancialOverviewDto> GetFinancialOverviewAsync();
    Task<FinancialGroupBreakdownDto> GetFinancialByGroupsAsync();
    Task<PaymentStatisticsDto> GetPaymentStatisticsAsync();
    Task<ExpenseAnalysisDto> GetExpenseAnalysisAsync();
    Task<FinancialAnomaliesDto> GetFinancialAnomaliesAsync();
    Task<byte[]> GenerateFinancialPdfAsync(FinancialReportRequestDto request);
    Task<byte[]> GenerateFinancialExcelAsync(FinancialReportRequestDto request);

    // Check-In Management Methods
    Task<CheckInListResponseDto> GetCheckInsAsync(CheckInListRequestDto request);
    Task<CheckInSummaryDto> GetCheckInDetailsAsync(Guid checkInId);
    Task<bool> ApproveCheckInAsync(Guid checkInId, ApproveCheckInDto request, Guid adminUserId);
    Task<bool> RejectCheckInAsync(Guid checkInId, RejectCheckInDto request, Guid adminUserId);

    // Maintenance Management Methods
    Task<MaintenanceListResponseDto> GetMaintenanceAsync(MaintenanceListRequestDto request);
    Task<MaintenanceSummaryDto> GetMaintenanceDetailsAsync(Guid maintenanceId);
    Task<bool> CreateMaintenanceAsync(CreateMaintenanceDto request, Guid adminUserId);
    Task<bool> UpdateMaintenanceAsync(Guid maintenanceId, UpdateMaintenanceDto request, Guid adminUserId);
    
    // Vehicle Management
    Task<VehicleListResponseDto> GetVehiclesAsync(VehicleListRequestDto request);
    Task<VehicleDto?> GetVehicleDetailsAsync(Guid vehicleId);
    Task<bool> UpdateVehicleStatusAsync(Guid vehicleId, UpdateVehicleStatusDto request, Guid adminUserId);
}
