using CoOwnershipVehicle.Domain.Entities;

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