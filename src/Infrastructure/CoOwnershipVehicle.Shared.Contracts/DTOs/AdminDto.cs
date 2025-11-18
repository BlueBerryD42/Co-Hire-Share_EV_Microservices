using System.ComponentModel.DataAnnotations;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Domain.Enums;

namespace CoOwnershipVehicle.Shared.Contracts.DTOs;

public class DashboardMetricsDto
{
    public UserMetricsDto Users { get; set; } = new();
    public GroupMetricsDto Groups { get; set; } = new();
    public VehicleMetricsDto Vehicles { get; set; } = new();
    public BookingMetricsDto Bookings { get; set; } = new();
    public RevenueMetricsDto Revenue { get; set; } = new();
    public SystemHealthDto SystemHealth { get; set; } = new();
    public List<ActivityFeedItemDto> RecentActivity { get; set; } = new();
    public List<AlertDto> Alerts { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

public class UserMetricsDto
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int InactiveUsers { get; set; }
    public int PendingKyc { get; set; }
    public int ApprovedKyc { get; set; }
    public int RejectedKyc { get; set; }
    public double UserGrowthPercentage { get; set; }
    public int NewUsersThisMonth { get; set; }
    public int NewUsersThisWeek { get; set; }
}

public class GroupMetricsDto
{
    public int TotalGroups { get; set; }
    public int ActiveGroups { get; set; }
    public int InactiveGroups { get; set; }
    public int DissolvedGroups { get; set; }
    public double GroupGrowthPercentage { get; set; }
    public int NewGroupsThisMonth { get; set; }
    public int NewGroupsThisWeek { get; set; }
}

public class VehicleMetricsDto
{
    public int TotalVehicles { get; set; }
    public int AvailableVehicles { get; set; }
    public int InUseVehicles { get; set; }
    public int MaintenanceVehicles { get; set; }
    public int UnavailableVehicles { get; set; }
    public double VehicleGrowthPercentage { get; set; }
    public int NewVehiclesThisMonth { get; set; }
    public int NewVehiclesThisWeek { get; set; }
}

public class BookingMetricsDto
{
    public int TotalBookings { get; set; }
    public int PendingBookings { get; set; }
    public int ConfirmedBookings { get; set; }
    public int InProgressBookings { get; set; }
    public int CompletedBookings { get; set; }
    public int CancelledBookings { get; set; }
    public int ActiveTrips { get; set; }
    public double BookingGrowthPercentage { get; set; }
    public int NewBookingsThisMonth { get; set; }
    public int NewBookingsThisWeek { get; set; }
}

public class RevenueMetricsDto
{
    public decimal TotalRevenue { get; set; }
    public decimal MonthlyRevenue { get; set; }
    public decimal WeeklyRevenue { get; set; }
    public decimal DailyRevenue { get; set; }
    public double RevenueGrowthPercentage { get; set; }
    public decimal AverageRevenuePerUser { get; set; }
    public decimal AverageRevenuePerGroup { get; set; }
    public decimal AverageRevenuePerVehicle { get; set; }
}

public class SystemHealthDto
{
    public bool DatabaseConnected { get; set; }
    public bool AllServicesHealthy { get; set; }
    public int PendingApprovals { get; set; }
    public int OverdueMaintenance { get; set; }
    public int Disputes { get; set; }
    public int SystemErrors { get; set; }
    public DateTime LastHealthCheck { get; set; } = DateTime.UtcNow;
}

public class ActivityFeedItemDto
{
    public Guid Id { get; set; }
    public string Entity { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? Details { get; set; }
}

public class AlertDto
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }
}

public class DashboardRequestDto
{
    public TimePeriod Period { get; set; } = TimePeriod.Monthly;
    public List<string>? Widgets { get; set; }
    public bool IncludeGrowthMetrics { get; set; } = true;
    public bool IncludeAlerts { get; set; } = true;
}

public enum TimePeriod
{
    Daily = 0,
    Weekly = 1,
    Monthly = 2,
    Quarterly = 3,
    Yearly = 4
}

// User Management DTOs
public class UserListRequestDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public UserRole? Role { get; set; }
    public KycStatus? KycStatus { get; set; }
    public UserAccountStatus? AccountStatus { get; set; }
    public string? SortBy { get; set; } = "CreatedAt";
    public string? SortDirection { get; set; } = "desc";
}

public class UserListResponseDto
{
    public List<UserSummaryDto> Users { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class UserSummaryDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public UserRole Role { get; set; }
    public KycStatus KycStatus { get; set; }
    public UserAccountStatus AccountStatus { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string FullName => $"{FirstName} {LastName}";
}

public class UserDetailsDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public UserRole Role { get; set; }
    public KycStatus KycStatus { get; set; }
    public UserAccountStatus AccountStatus { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public List<GroupMembershipDto> GroupMemberships { get; set; } = new();
    public UserStatisticsDto Statistics { get; set; } = new();
    public List<KycDocumentDto> KycDocuments { get; set; } = new();
    public List<UserActivityDto> RecentActivity { get; set; } = new();
}

public class GroupMembershipDto
{
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
    public bool IsActive { get; set; }
}

public class UserStatisticsDto
{
    public int TotalBookings { get; set; }
    public int CompletedBookings { get; set; }
    public int CancelledBookings { get; set; }
    public decimal TotalPayments { get; set; }
    public int GroupMemberships { get; set; }
    public int ActiveGroupMemberships { get; set; }
}


public class UserActivityDto
{
    public Guid Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class UpdateUserStatusDto
{
    public UserAccountStatus Status { get; set; }
    public string? Reason { get; set; }
}

public class UpdateUserRoleDto
{
    public UserRole Role { get; set; }
    public string? Reason { get; set; }
}

public class UpdateUserKycStatusDto
{
    public KycStatus Status { get; set; }
    public string? Reason { get; set; }
}

public class PendingKycUserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public KycStatus KycStatus { get; set; }
    public DateTime SubmittedAt { get; set; }
    public List<KycDocumentDto> Documents { get; set; } = new();
}

public class KycDocumentFilterDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public KycDocumentStatus? Status { get; set; }
    public KycDocumentType? DocumentType { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? SortBy { get; set; } = "UploadedAt";
    public string? SortDirection { get; set; } = "desc";
}

public class PendingKycUserListResponseDto
{
    public List<PendingKycUserDto> Users { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class BulkReviewKycDocumentsDto
{
    [Required]
    public List<Guid> DocumentIds { get; set; } = new();
    
    [Required]
    public KycDocumentStatus Status { get; set; }
    
    public string? ReviewNotes { get; set; }
}

public class KycReviewStatisticsDto
{
    public int TotalPending { get; set; }
    public int TotalUnderReview { get; set; }
    public int TotalApproved { get; set; }
    public int TotalRejected { get; set; }
    public int TotalRequiresUpdate { get; set; }
    public int ApprovedToday { get; set; }
    public int RejectedToday { get; set; }
    public int ApprovedThisWeek { get; set; }
    public int RejectedThisWeek { get; set; }
    public int ApprovedThisMonth { get; set; }
    public int RejectedThisMonth { get; set; }
    public double AverageReviewTimeHours { get; set; }
    public Dictionary<string, int> DocumentsByType { get; set; } = new();
    public Dictionary<string, int> DocumentsByStatus { get; set; } = new();
}

public enum UserAccountStatus
{
    Active = 0,
    Inactive = 1,
    Suspended = 2,
    Banned = 3
}

// Group Management DTOs
public class GroupListRequestDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public GroupStatus? Status { get; set; }
    public int? MinMemberCount { get; set; }
    public int? MaxMemberCount { get; set; }
    public string? SortBy { get; set; } = "CreatedAt";
    public string? SortDirection { get; set; } = "desc";
}

public class GroupListResponseDto
{
    public List<GroupSummaryDto> Groups { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class GroupSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public GroupStatus Status { get; set; }
    public int MemberCount { get; set; }
    public int VehicleCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal ActivityScore { get; set; }
    public GroupHealthStatus HealthStatus { get; set; }
    public string CreatorName { get; set; } = string.Empty;
}

public class GroupDetailsDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public GroupStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string CreatorName { get; set; } = string.Empty;
    public List<GroupMemberDetailsDto> Members { get; set; } = new();
    public List<GroupVehicleDto> Vehicles { get; set; } = new();
    public GroupFinancialSummaryDto FinancialSummary { get; set; } = new();
    public GroupBookingStatisticsDto BookingStatistics { get; set; } = new();
    public List<GroupActivityDto> RecentActivity { get; set; } = new();
    public List<GroupProposalDto> Proposals { get; set; } = new();
    public List<GroupDisputeDto> Disputes { get; set; } = new();
    public GroupHealthDto Health { get; set; } = new();
}

public class GroupMemberDetailsDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public GroupRole Role { get; set; }
    public decimal SharePercentage { get; set; }
    public DateTime JoinedAt { get; set; }
    public bool IsActive { get; set; }
    public int TotalBookings { get; set; }
    public decimal TotalPayments { get; set; }
}

public class GroupVehicleDto
{
    public Guid Id { get; set; }
    public string Vin { get; set; } = string.Empty;
    public string PlateNumber { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Year { get; set; }
    public string? Color { get; set; }
    public VehicleStatus Status { get; set; }
    public DateTime? LastServiceDate { get; set; }
    public int Odometer { get; set; }
    public int TotalBookings { get; set; }
    public decimal TotalRevenue { get; set; }
}

public class GroupFinancialSummaryDto
{
    public decimal TotalExpenses { get; set; }
    public decimal FundBalance { get; set; }
    public decimal MonthlyExpenses { get; set; }
    public decimal AverageExpensePerMember { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal NetBalance { get; set; }
    public List<GroupExpenseCategoryDto> ExpenseCategories { get; set; } = new();
}

public class GroupExpenseCategoryDto
{
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}

public class GroupBookingStatisticsDto
{
    public int TotalBookings { get; set; }
    public int CompletedBookings { get; set; }
    public int CancelledBookings { get; set; }
    public int ActiveBookings { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal AverageBookingValue { get; set; }
    public int BookingsThisMonth { get; set; }
    public int BookingsLastMonth { get; set; }
    public decimal BookingGrowthPercentage { get; set; }
}

public class GroupActivityDto
{
    public Guid Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class GroupProposalDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ProposalType Type { get; set; }
    public ProposalStatus Status { get; set; }
    public decimal? Amount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime VotingEndDate { get; set; }
    public string CreatorName { get; set; } = string.Empty;
    public int TotalVotes { get; set; }
    public int YesVotes { get; set; }
    public int NoVotes { get; set; }
    public decimal ApprovalPercentage { get; set; }
}

public class GroupDisputeDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DisputeStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public string InitiatorName { get; set; } = string.Empty;
    public string? Resolution { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

public class GroupHealthDto
{
    public GroupHealthStatus Status { get; set; }
    public decimal Score { get; set; }
    public List<string> Issues { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public DateTime LastActivity { get; set; }
    public int DaysInactive { get; set; }
    public bool HasActiveDisputes { get; set; }
    public bool IsUsageBalanced { get; set; }
    public string Recommendation { get; set; } = string.Empty;
}

public class UpdateGroupStatusDto
{
    public GroupStatus Status { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool NotifyMembers { get; set; } = true;
}

public class GroupInterventionDto
{
    public InterventionType Type { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid? TemporaryAdminId { get; set; }
    public DateTime? FreezeUntil { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class GroupAuditRequestDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public string? Search { get; set; }
    public string? Action { get; set; }
    public Guid? UserId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class GroupAuditResponseDto
{
    public List<GroupAuditEntryDto> Entries { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class GroupAuditEntryDto
{
    public Guid Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}

public class AuditLogRequestDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public string? ActionType { get; set; }
    public string? Module { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class AuditLogResponseDto
{
    public List<AuditLogEntryDto> Logs { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class AuditLogEntryDto
{
    public Guid Id { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? IpAddress { get; set; }
}

public enum GroupHealthStatus
{
    Healthy = 0,
    Warning = 1,
    Unhealthy = 2,
    Critical = 3
}

public enum InterventionType
{
    Freeze = 0,
    Message = 1,
    AppointAdmin = 2,
    Unfreeze = 3
}


    // Dispute Management DTOs
    public class CreateDisputeDto
    {
        public Guid GroupId { get; set; }
        public string Subject { get; set; }
        public string Description { get; set; }
        public DisputeCategory Category { get; set; }
        public DisputePriority Priority { get; set; }
        public Guid? ReportedBy { get; set; } // Optional - for admins creating on behalf of users
    }

    public class DisputeListRequestDto
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string? Search { get; set; }
        public DisputeStatus? Status { get; set; }
        public DisputePriority? Priority { get; set; }
        public DisputeCategory? Category { get; set; }
        public Guid? AssignedTo { get; set; }
        public Guid? GroupId { get; set; }
        public string SortBy { get; set; } = "CreatedAt";
        public string SortDirection { get; set; } = "desc";
    }

    public class DisputeListResponseDto
    {
        public List<DisputeSummaryDto> Disputes { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    public class DisputeSummaryDto
    {
        public Guid Id { get; set; }
        public Guid GroupId { get; set; }
        public string GroupName { get; set; }
        public string Subject { get; set; }
        public DisputeCategory Category { get; set; }
        public DisputePriority Priority { get; set; }
        public DisputeStatus Status { get; set; }
        public string ReporterName { get; set; }
        public string? AssignedToName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public int CommentCount { get; set; }
    }

    public class DisputeDetailsDto
    {
        public Guid Id { get; set; }
        public Guid GroupId { get; set; }
        public string GroupName { get; set; }
        public string Subject { get; set; }
        public string Description { get; set; }
        public DisputeCategory Category { get; set; }
        public DisputePriority Priority { get; set; }
        public DisputeStatus Status { get; set; }
        public string ReporterName { get; set; }
        public string ReporterEmail { get; set; }
        public string? AssignedToName { get; set; }
        public string? AssignedToEmail { get; set; }
        public string? Resolution { get; set; }
        public string? ResolverName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public List<DisputeCommentDto> Comments { get; set; } = new();
        public List<DisputeActionDto> Actions { get; set; } = new();
    }

    public class DisputeCommentDto
    {
        public Guid Id { get; set; }
        public string Comment { get; set; }
        public string CommenterName { get; set; }
        public string CommenterEmail { get; set; }
        public bool IsInternal { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class DisputeActionDto
    {
        public string Action { get; set; }
        public string Details { get; set; }
        public string UserName { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class AssignDisputeDto
    {
        public Guid AssignedTo { get; set; }
        public string? Note { get; set; }
    }

    public class AddDisputeCommentDto
    {
        public string Comment { get; set; }
        public bool IsInternal { get; set; } = false;
    }

    public class ResolveDisputeDto
    {
        public string Resolution { get; set; }
        public string? Note { get; set; }
    }

    public class DisputeStatisticsDto
    {
        public int TotalDisputes { get; set; }
        public int OpenDisputes { get; set; }
        public int UnderReviewDisputes { get; set; }
        public int ResolvedDisputes { get; set; }
        public int ClosedDisputes { get; set; }
        public int EscalatedDisputes { get; set; }
        public int UrgentDisputes { get; set; }
        public int HighPriorityDisputes { get; set; }
        public int VehicleDamageDisputes { get; set; }
        public int LateFeesDisputes { get; set; }
        public int UsageDisputes { get; set; }
        public int FinancialDisputes { get; set; }
        public int OtherDisputes { get; set; }
        public double AverageResolutionTimeHours { get; set; }
        public int DisputesResolvedThisMonth { get; set; }
        public int DisputesCreatedThisMonth { get; set; }
    }

    // System Health Monitoring DTOs
    public class SystemHealthCheckDto
    {
        public List<ServiceHealthDto> Services { get; set; } = new();
        public List<DependencyHealthDto> Dependencies { get; set; } = new();
        public SystemHealthStatus OverallStatus { get; set; }
        public DateTime CheckTime { get; set; } = DateTime.UtcNow;
        public double TotalResponseTimeMs { get; set; }
    }

    public class ServiceHealthDto
    {
        public string ServiceName { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = string.Empty;
        public HealthStatus Status { get; set; }
        public double ResponseTimeMs { get; set; }
        public double? ErrorRate { get; set; }
        public DateTime? LastIncidentTimestamp { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CheckTime { get; set; } = DateTime.UtcNow;
    }

    public class DependencyHealthDto
    {
        public string DependencyName { get; set; } = string.Empty;
        public DependencyType Type { get; set; }
        public HealthStatus Status { get; set; }
        public double ResponseTimeMs { get; set; }
        public string? ConnectionString { get; set; }
        public DateTime? LastIncidentTimestamp { get; set; }
        public string? ErrorMessage { get; set; }
        public Dictionary<string, object>? AdditionalInfo { get; set; }
    }

    public class SystemMetricsDto
    {
        public List<ServiceMetricsDto> ServiceMetrics { get; set; } = new();
        public SystemResourceMetricsDto SystemResources { get; set; } = new();
        public DatabaseMetricsDto DatabaseMetrics { get; set; } = new();
        public MessageQueueMetricsDto MessageQueueMetrics { get; set; } = new();
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public TimeSpan CollectionPeriod { get; set; }
    }

    public class ServiceMetricsDto
    {
        public string ServiceName { get; set; } = string.Empty;
        public long RequestCount { get; set; }
        public double AverageResponseTimeMs { get; set; }
        public double P95ResponseTimeMs { get; set; }
        public double P99ResponseTimeMs { get; set; }
        public double ErrorRate { get; set; }
        public long ErrorCount { get; set; }
        public long SuccessCount { get; set; }
        public Dictionary<string, long> EndpointRequestCounts { get; set; } = new();
        public Dictionary<string, double> EndpointAverageResponseTimes { get; set; } = new();
        public Dictionary<string, double> EndpointErrorRates { get; set; } = new();
    }

    public class SystemResourceMetricsDto
    {
        public double CpuUsagePercent { get; set; }
        public long MemoryUsageBytes { get; set; }
        public long MemoryTotalBytes { get; set; }
        public double MemoryUsagePercent { get; set; }
        public long DiskUsageBytes { get; set; }
        public long DiskTotalBytes { get; set; }
        public double DiskUsagePercent { get; set; }
        public long ActiveConnections { get; set; }
        public int ThreadCount { get; set; }
    }

    public class DatabaseMetricsDto
    {
        public long TotalQueries { get; set; }
        public double AverageQueryTimeMs { get; set; }
        public double P95QueryTimeMs { get; set; }
        public long SlowQueries { get; set; }
        public long ActiveConnections { get; set; }
        public long ConnectionPoolSize { get; set; }
        public Dictionary<string, long> QueryCountsByEntity { get; set; } = new();
    }

    public class MessageQueueMetricsDto
    {
        public string QueueName { get; set; } = string.Empty;
        public long QueueDepth { get; set; }
        public long MessagesProcessed { get; set; }
        public double AverageProcessingTimeMs { get; set; }
        public long FailedMessages { get; set; }
        public double FailureRate { get; set; }
    }

    public class SystemLogsRequestDto
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public string? Service { get; set; }
        public string? Level { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? Search { get; set; }
        public string? SortBy { get; set; } = "Timestamp";
        public string? SortDirection { get; set; } = "desc";
    }

    public class SystemLogsResponseDto
    {
        public List<LogEntryDto> Logs { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    public class LogEntryDto
    {
        public Guid Id { get; set; }
        public string Service { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Exception { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object>? Properties { get; set; }
        public string? UserId { get; set; }
        public string? RequestId { get; set; }
        public string? IpAddress { get; set; }
    }

    public enum HealthStatus
    {
        Healthy = 0,
        Degraded = 1,
        Unhealthy = 2,
        Unknown = 3
    }

    public enum SystemHealthStatus
    {
        Healthy = 0,
        Degraded = 1,
        Unhealthy = 2,
        Critical = 3
    }

    public enum DependencyType
    {
        Database = 0,
        RabbitMQ = 1,
        Redis = 2,
        FileStorage = 3,
        ExternalApi = 4
    }

    public enum LogLevel
    {
        Trace = 0,
        Debug = 1,
        Information = 2,
        Warning = 3,
        Error = 4,
        Critical = 5
    }

    // Check-In Management DTOs
    public class CheckInListRequestDto
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string? Status { get; set; }
        public Guid? UserId { get; set; }
        public Guid? VehicleId { get; set; }
        public Guid? BookingId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? SortBy { get; set; } = "CheckInTime";
        public string? SortDirection { get; set; } = "desc";
    }

    public class CheckInListResponseDto
    {
        public List<CheckInSummaryDto> CheckIns { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    public class CheckInSummaryDto
    {
        public Guid Id { get; set; }
        public Guid BookingId { get; set; }
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public Guid? VehicleId { get; set; }
        public string? VehicleMake { get; set; }
        public string? VehicleModel { get; set; }
        public string? VehiclePlateNumber { get; set; }
        public CheckInType Type { get; set; }
        public int OdometerReading { get; set; }
        public DateTime CheckInTime { get; set; }
        public string Status { get; set; } = "Pending";
        public string? Notes { get; set; }
        public bool IsLateReturn { get; set; }
        public double? LateReturnMinutes { get; set; }
        public decimal? LateFeeAmount { get; set; }
        public int? BatteryPercentage { get; set; }
        public List<CheckInPhotoDto> Photos { get; set; } = new();
    }

    public class ApproveCheckInDto
    {
        public string? Notes { get; set; }
    }

    public class RejectCheckInDto
    {
        [Required]
        public string Reason { get; set; } = string.Empty;
    }

    // Maintenance Management DTOs
    public class MaintenanceListRequestDto
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string? Status { get; set; }
        public Guid? VehicleId { get; set; }
        public MaintenanceServiceType? ServiceType { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? SortBy { get; set; } = "ScheduledDate";
        public string? SortDirection { get; set; } = "desc";
    }

    public class MaintenanceListResponseDto
    {
        public List<MaintenanceSummaryDto> Maintenance { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    public class MaintenanceSummaryDto
    {
        public Guid Id { get; set; }
        public Guid VehicleId { get; set; }
        public string? VehicleMake { get; set; }
        public string? VehicleModel { get; set; }
        public string? VehiclePlate { get; set; }
        public MaintenanceServiceType ServiceType { get; set; }
        public string ServiceTypeName { get; set; } = string.Empty;
        public DateTime ScheduledDate { get; set; }
        public DateTime? ServiceCompletedDate { get; set; }
        public string Status { get; set; } = "Scheduled";
        public decimal? Cost { get; set; }
        public decimal? EstimatedCost { get; set; }
        public decimal? ActualCost { get; set; }
        public string? Provider { get; set; }
        public string? Notes { get; set; }
        public int? OdometerReading { get; set; }
        public MaintenancePriority Priority { get; set; }
    }

    public class CreateMaintenanceDto
    {
        [Required]
        public Guid VehicleId { get; set; }
        
        [Required]
        public MaintenanceServiceType ServiceType { get; set; }
        
        [Required]
        public DateTime ScheduledDate { get; set; }
        
        public string? Provider { get; set; }
        
        public decimal? EstimatedCost { get; set; }
        
        public int? EstimatedDurationMinutes { get; set; }
        
        public MaintenancePriority Priority { get; set; } = MaintenancePriority.Medium;
        
        [StringLength(500)]
        public string? Notes { get; set; }
    }

    public class UpdateMaintenanceDto
    {
        public MaintenanceServiceType? ServiceType { get; set; }
        
        public DateTime? ScheduledDate { get; set; }
        
        public DateTime? ServiceCompletedDate { get; set; }
        
        public string? Status { get; set; }
        
        public string? Provider { get; set; }
        
        public decimal? EstimatedCost { get; set; }
        
        public decimal? ActualCost { get; set; }
        
        public int? EstimatedDurationMinutes { get; set; }
        
        public int? ActualDurationMinutes { get; set; }
        
        public int? OdometerReading { get; set; }
        
        public int? OdometerAtService { get; set; }
        
        [StringLength(2000)]
        public string? WorkPerformed { get; set; }
        
        [StringLength(2000)]
        public string? PartsUsed { get; set; }
        
        [StringLength(500)]
        public string? Notes { get; set; }
        
        public MaintenancePriority? Priority { get; set; }
    }

// Vehicle Management DTOs
public class VehicleListRequestDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public VehicleStatus? Status { get; set; }
    public Guid? GroupId { get; set; }
    public string? SortBy { get; set; } = "CreatedAt";
    public string? SortDirection { get; set; } = "desc";
}

public class VehicleListResponseDto
{
    public List<VehicleDto> Vehicles { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class UpdateVehicleStatusDto
{
    public VehicleStatus Status { get; set; }
    public string? Reason { get; set; }
}