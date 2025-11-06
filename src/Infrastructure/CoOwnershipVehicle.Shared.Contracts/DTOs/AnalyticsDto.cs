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

// Usage vs Ownership DTOs
public class MemberUsageMetricsDto
{
    public Guid MemberId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public decimal OwnershipPercentage { get; set; }
    
    // Usage by different metrics
    public UsageMetricsDto ByTrips { get; set; } = new();
    public UsageMetricsDto ByDistance { get; set; } = new();
    public UsageMetricsDto ByTime { get; set; } = new();
    public UsageMetricsDto ByCost { get; set; } = new();
    
    // Fairness indicators
    public decimal OverallUsagePercentage { get; set; }
    public decimal UsageDifference { get; set; } // Positive = over-utilizing, Negative = under-utilizing
    public string FairShareIndicator { get; set; } = string.Empty; // "Fair", "Over", "Under"
    public decimal FairnessScore { get; set; } // 0-100
}

public class UsageMetricsDto
{
    public decimal ActualUsagePercentage { get; set; }
    public decimal ExpectedUsagePercentage { get; set; }
    public decimal Difference { get; set; }
    public decimal Value { get; set; } // Total trips, distance, hours, or cost
}

public class UsageVsOwnershipDto
{
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    
    // Member-level metrics
    public List<MemberUsageMetricsDto> Members { get; set; } = new();
    
    // Group-level metrics
    public GroupFairnessMetricsDto GroupMetrics { get; set; } = new();
    
    // Historical trends
    public List<PeriodComparisonDto> HistoricalTrends { get; set; } = new();
}

public class GroupFairnessMetricsDto
{
    public decimal OverallFairnessScore { get; set; } // 0-100
    public decimal DistributionBalance { get; set; } // 0-1, higher is more balanced
    public decimal UsageConcentration { get; set; } // 0-1, higher = few members using most
    
    // Gini coefficient for usage distribution (0 = perfect equality, 1 = maximum inequality)
    public decimal GiniCoefficient { get; set; }
    
    // Top users
    public int TopUsersCount { get; set; }
    public decimal TopUsersUsagePercentage { get; set; }
    
    // Recommendations
    public List<string> Recommendations { get; set; } = new();
}

public class PeriodComparisonDto
{
    public string PeriodLabel { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal AverageFairnessScore { get; set; }
    public decimal AverageUsageBalance { get; set; }
    public int ActiveMembers { get; set; }
}

public class MemberComparisonDto
{
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    
    public List<MemberComparisonItemDto> Members { get; set; } = new();
}

public class MemberComparisonItemDto
{
    public Guid MemberId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public decimal OwnershipPercentage { get; set; }
    
    // Usage patterns
    public int TotalTrips { get; set; }
    public decimal TotalDistance { get; set; }
    public int TotalHours { get; set; }
    public decimal TotalCost { get; set; }
    
    // Usage percentages
    public decimal UsagePercentageByTrips { get; set; }
    public decimal UsagePercentageByDistance { get; set; }
    public decimal UsagePercentageByTime { get; set; }
    public decimal UsagePercentageByCost { get; set; }
    
    // Efficiency metrics
    public decimal DistancePerTrip { get; set; }
    public decimal HoursPerTrip { get; set; }
    public decimal CostPerTrip { get; set; }
    public decimal CostPerHour { get; set; }
    public decimal CostPerKm { get; set; }
    
    // Activity level
    public string ActivityLevel { get; set; } = string.Empty; // "Active", "Moderate", "Inactive"
    public int DaysSinceLastBooking { get; set; }
    public decimal BookingFrequency { get; set; } // Bookings per week
    
    // Fairness indicators
    public decimal FairnessScore { get; set; }
    public string FairShareStatus { get; set; } = string.Empty;
}

public class VisualizationDataDto
{
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    
    // Pie chart data - usage distribution by member
    public List<ChartDataPointDto> UsageDistributionByMember { get; set; } = new();
    
    // Bar chart data - ownership vs usage comparison
    public List<ComparisonBarDataDto> OwnershipVsUsage { get; set; } = new();
    
    // Timeline chart data - usage trends
    public List<TimelineDataPointDto> UsageTrends { get; set; } = new();
    
    // Heat map data - usage by time/day
    public List<HeatMapDataPointDto> UsageHeatMap { get; set; } = new();
}

public class ChartDataPointDto
{
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public decimal Percentage { get; set; }
}

public class ComparisonBarDataDto
{
    public string MemberName { get; set; } = string.Empty;
    public decimal OwnershipPercentage { get; set; }
    public decimal UsagePercentage { get; set; }
    public string Metric { get; set; } = string.Empty; // "Trips", "Distance", "Time", "Cost"
}

public class TimelineDataPointDto
{
    public DateTime Date { get; set; }
    public decimal TotalUsage { get; set; }
    public decimal AverageFairnessScore { get; set; }
    public int ActiveMembers { get; set; }
}

public class HeatMapDataPointDto
{
    public string DayOfWeek { get; set; } = string.Empty;
    public int Hour { get; set; }
    public decimal UsageValue { get; set; }
    public int BookingCount { get; set; }
}

public class FairnessReportDto
{
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public DateTime ReportDate { get; set; } = DateTime.UtcNow;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    
    // Summary
    public decimal OverallFairnessScore { get; set; }
    public string OverallAssessment { get; set; } = string.Empty;
    
    // Member breakdown
    public List<MemberUsageMetricsDto> MemberBreakdown { get; set; } = new();
    
    // Historical comparison
    public List<PeriodComparisonDto> HistoricalComparison { get; set; } = new();
    
    // Recommendations
    public List<RecommendationDto> Recommendations { get; set; } = new();
    
    // Visualization data
    public VisualizationDataDto VisualizationData { get; set; } = new();
}

public class RecommendationDto
{
    public string Category { get; set; } = string.Empty; // "Ownership", "Usage", "Cost", "Activity"
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty; // "High", "Medium", "Low"
    public string Impact { get; set; } = string.Empty; // Expected impact description
}