namespace CoOwnershipVehicle.Shared.Contracts.DTOs;

public class AnalyticsSnapshotDto
{
    public Guid Id { get; set; }
    public Guid? GroupId { get; set; }
    public Guid? VehicleId { get; set; }
    public DateTime SnapshotDate { get; set; }
    public string Period { get; set; } = string.Empty;
    
    // Usage Statistics
    public decimal TotalDistance { get; set; }
    public int TotalBookings { get; set; }
    public int TotalUsageHours { get; set; }
    public int ActiveUsers { get; set; }
    
    // Financial Statistics
    public decimal TotalRevenue { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetProfit { get; set; }
    public decimal AverageCostPerHour { get; set; }
    public decimal AverageCostPerKm { get; set; }
    
    // Efficiency Metrics
    public decimal UtilizationRate { get; set; }
    public decimal MaintenanceEfficiency { get; set; }
    public decimal UserSatisfactionScore { get; set; }
}

public class UserAnalyticsDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public Guid? GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public string Period { get; set; } = string.Empty;
    
    // Usage Statistics
    public int TotalBookings { get; set; }
    public int TotalUsageHours { get; set; }
    public decimal TotalDistance { get; set; }
    public decimal OwnershipShare { get; set; }
    public decimal UsageShare { get; set; }
    
    // Financial Statistics
    public decimal TotalPaid { get; set; }
    public decimal TotalOwed { get; set; }
    public decimal NetBalance { get; set; }
    
    // Behavior Metrics
    public decimal BookingSuccessRate { get; set; }
    public int Cancellations { get; set; }
    public int NoShows { get; set; }
    public decimal PunctualityScore { get; set; }
}

public class VehicleAnalyticsDto
{
    public Guid Id { get; set; }
    public Guid VehicleId { get; set; }
    public string VehicleModel { get; set; } = string.Empty;
    public string PlateNumber { get; set; } = string.Empty;
    public Guid? GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public string Period { get; set; } = string.Empty;
    
    // Performance Metrics
    public decimal TotalDistance { get; set; }
    public int TotalBookings { get; set; }
    public int TotalUsageHours { get; set; }
    public decimal UtilizationRate { get; set; }
    public decimal AvailabilityRate { get; set; }
    
    // Financial Metrics
    public decimal Revenue { get; set; }
    public decimal MaintenanceCost { get; set; }
    public decimal OperatingCost { get; set; }
    public decimal NetProfit { get; set; }
    public decimal CostPerKm { get; set; }
    public decimal CostPerHour { get; set; }
    
    // Reliability Metrics
    public int MaintenanceEvents { get; set; }
    public int Breakdowns { get; set; }
    public decimal ReliabilityScore { get; set; }
}

public class GroupAnalyticsDto
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public string Period { get; set; } = string.Empty;
    
    // Member Statistics
    public int TotalMembers { get; set; }
    public int ActiveMembers { get; set; }
    public int NewMembers { get; set; }
    public int LeftMembers { get; set; }
    
    // Financial Summary
    public decimal TotalRevenue { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetProfit { get; set; }
    public decimal AverageMemberContribution { get; set; }
    
    // Activity Metrics
    public int TotalBookings { get; set; }
    public int TotalProposals { get; set; }
    public int TotalVotes { get; set; }
    public decimal ParticipationRate { get; set; }
}

public class AnalyticsDashboardDto
{
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public string Period { get; set; } = string.Empty;
    
    // Overall Statistics
    public int TotalGroups { get; set; }
    public int TotalVehicles { get; set; }
    public int TotalUsers { get; set; }
    public int TotalBookings { get; set; }
    
    // Financial Overview
    public decimal TotalRevenue { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetProfit { get; set; }
    
    // Efficiency Metrics
    public decimal AverageUtilizationRate { get; set; }
    public decimal AverageUserSatisfaction { get; set; }
    
    // Top Performers
    public List<VehicleAnalyticsDto> TopVehicles { get; set; } = new();
    public List<UserAnalyticsDto> TopUsers { get; set; } = new();
    public List<GroupAnalyticsDto> TopGroups { get; set; } = new();
    
    // Trends
    public List<AnalyticsSnapshotDto> WeeklyTrends { get; set; } = new();
    public List<AnalyticsSnapshotDto> MonthlyTrends { get; set; } = new();
}

public class CreateAnalyticsSnapshotDto
{
    public Guid? GroupId { get; set; }
    public Guid? VehicleId { get; set; }
    public DateTime SnapshotDate { get; set; } = DateTime.UtcNow;
    public string Period { get; set; } = string.Empty;
}

public class AnalyticsRequestDto
{
    public Guid? GroupId { get; set; }
    public Guid? VehicleId { get; set; }
    public Guid? UserId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Period { get; set; } = "Monthly";
    public int? Limit { get; set; }
    public int? Offset { get; set; }
}
