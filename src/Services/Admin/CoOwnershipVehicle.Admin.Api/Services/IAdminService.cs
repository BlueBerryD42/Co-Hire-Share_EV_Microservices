using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Domain.Entities;

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
    Task<List<PendingKycUserDto>> GetPendingKycUsersAsync();
    Task<KycDocumentDto> ReviewKycDocumentAsync(Guid documentId, ReviewKycDocumentDto request, Guid adminUserId);
    Task<bool> UpdateUserKycStatusAsync(Guid userId, UpdateUserKycStatusDto request, Guid adminUserId);
    
    // Group Management Methods
    Task<GroupListResponseDto> GetGroupsAsync(GroupListRequestDto request);
    Task<GroupDetailsDto> GetGroupDetailsAsync(Guid groupId);
    Task<bool> UpdateGroupStatusAsync(Guid groupId, UpdateGroupStatusDto request, Guid adminUserId);
    Task<GroupAuditResponseDto> GetGroupAuditTrailAsync(Guid groupId, GroupAuditRequestDto request);
    Task<bool> InterveneInGroupAsync(Guid groupId, GroupInterventionDto request, Guid adminUserId);
        Task<GroupHealthDto> GetGroupHealthAsync(Guid groupId);

    // Vehicle Management Methods
    Task<VehicleListResponseDto> GetVehiclesAsync(VehicleListRequestDto request);
    Task<VehicleSummaryDto> GetVehicleDetailsAsync(Guid vehicleId);
    Task<bool> UpdateVehicleStatusAsync(Guid vehicleId, VehicleStatus status, Guid adminUserId);

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
}
