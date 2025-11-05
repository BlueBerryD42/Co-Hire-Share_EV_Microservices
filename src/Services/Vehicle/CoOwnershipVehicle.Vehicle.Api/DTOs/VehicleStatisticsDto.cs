using System.ComponentModel.DataAnnotations;

namespace CoOwnershipVehicle.Vehicle.Api.DTOs;

/// <summary>
/// Request parameters for vehicle statistics query
/// </summary>
public class VehicleStatisticsRequest
{
    /// <summary>
    /// Start date for statistics period (default: 30 days ago)
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// End date for statistics period (default: now)
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Time grouping: daily, weekly, monthly
    /// </summary>
    [RegularExpression("^(daily|weekly|monthly)$", ErrorMessage = "GroupBy must be 'daily', 'weekly', or 'monthly'")]
    public string GroupBy { get; set; } = "daily";

    /// <summary>
    /// Include benchmark comparisons
    /// </summary>
    public bool IncludeBenchmarks { get; set; } = true;
}

/// <summary>
/// Comprehensive vehicle usage statistics response
/// </summary>
public class VehicleStatisticsResponse
{
    public Guid VehicleId { get; set; }
    public string VehicleName { get; set; } = string.Empty;
    public string PlateNumber { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalDays { get; set; }

    // Basic usage metrics
    public UsageMetrics Usage { get; set; } = new();

    // Time-based analysis
    public UtilizationMetrics Utilization { get; set; } = new();

    // Efficiency metrics
    public EfficiencyMetrics Efficiency { get; set; } = new();

    // Peak usage patterns
    public UsagePatterns Patterns { get; set; } = new();

    // Trends over time
    public List<TrendDataPoint> TrendsOverTime { get; set; } = new();

    // Comparison to previous period
    public PeriodComparison Comparison { get; set; } = new();

    // Benchmark comparisons (optional)
    public BenchmarkData? Benchmarks { get; set; }
}

/// <summary>
/// Basic usage metrics
/// </summary>
public class UsageMetrics
{
    /// <summary>
    /// Total number of trips (bookings)
    /// </summary>
    public int TotalTrips { get; set; }

    /// <summary>
    /// Total distance traveled in kilometers
    /// </summary>
    public decimal TotalDistance { get; set; }

    /// <summary>
    /// Total usage hours
    /// </summary>
    public decimal TotalUsageHours { get; set; }

    /// <summary>
    /// Average trip distance in kilometers
    /// </summary>
    public decimal AverageTripDistance { get; set; }

    /// <summary>
    /// Average trip duration in hours
    /// </summary>
    public decimal AverageTripDuration { get; set; }

    /// <summary>
    /// Most frequent user (userId, name, trip count)
    /// </summary>
    public MostFrequentUser? MostFrequentUser { get; set; }
}

/// <summary>
/// Utilization rate metrics
/// </summary>
public class UtilizationMetrics
{
    /// <summary>
    /// Percentage of time vehicle was in use
    /// </summary>
    public decimal UtilizationRate { get; set; }

    /// <summary>
    /// Total hours vehicle was available
    /// </summary>
    public decimal TotalAvailableHours { get; set; }

    /// <summary>
    /// Total hours vehicle was idle (available but not used)
    /// </summary>
    public decimal IdleHours { get; set; }

    /// <summary>
    /// Total hours vehicle was in maintenance
    /// </summary>
    public decimal MaintenanceHours { get; set; }

    /// <summary>
    /// Total hours vehicle was unavailable (blocked)
    /// </summary>
    public decimal UnavailableHours { get; set; }
}

/// <summary>
/// Efficiency metrics (especially for EVs)
/// </summary>
public class EfficiencyMetrics
{
    /// <summary>
    /// Distance per charge cycle (km) - for EVs
    /// </summary>
    public decimal? DistancePerCharge { get; set; }

    /// <summary>
    /// Average cost per kilometer
    /// </summary>
    public decimal? CostPerKilometer { get; set; }

    /// <summary>
    /// Average cost per hour
    /// </summary>
    public decimal? CostPerHour { get; set; }

    /// <summary>
    /// Total revenue generated
    /// </summary>
    public decimal? TotalRevenue { get; set; }

    /// <summary>
    /// Total costs (maintenance, fuel/charging)
    /// </summary>
    public decimal? TotalCosts { get; set; }

    /// <summary>
    /// Net profit (revenue - costs)
    /// </summary>
    public decimal? NetProfit { get; set; }
}

/// <summary>
/// Usage patterns and peak times
/// </summary>
public class UsagePatterns
{
    /// <summary>
    /// Peak usage hours (0-23)
    /// </summary>
    public List<HourlyUsage> PeakHours { get; set; } = new();

    /// <summary>
    /// Peak usage days of week (0=Sunday, 6=Saturday)
    /// </summary>
    public List<DailyUsage> PeakDays { get; set; } = new();

    /// <summary>
    /// Busiest time of day
    /// </summary>
    public string BusiestHour { get; set; } = string.Empty;

    /// <summary>
    /// Busiest day of week
    /// </summary>
    public string BusiestDay { get; set; } = string.Empty;
}

/// <summary>
/// Hourly usage statistics
/// </summary>
public class HourlyUsage
{
    public int Hour { get; set; } // 0-23
    public int TripCount { get; set; }
    public decimal AverageDuration { get; set; }
}

/// <summary>
/// Daily usage statistics
/// </summary>
public class DailyUsage
{
    public DayOfWeek DayOfWeek { get; set; }
    public string DayName { get; set; } = string.Empty;
    public int TripCount { get; set; }
    public decimal TotalDistance { get; set; }
}

/// <summary>
/// Trend data point for charting
/// </summary>
public class TrendDataPoint
{
    public DateTime Date { get; set; }
    public string Period { get; set; } = string.Empty; // "2025-11-01", "Week 44", "Nov 2025"
    public int TripCount { get; set; }
    public decimal Distance { get; set; }
    public decimal UsageHours { get; set; }
    public decimal UtilizationRate { get; set; }
}

/// <summary>
/// Comparison to previous period
/// </summary>
public class PeriodComparison
{
    public int PreviousTripCount { get; set; }
    public decimal PreviousDistance { get; set; }
    public decimal PreviousUsageHours { get; set; }
    public decimal PreviousUtilizationRate { get; set; }

    // Growth percentages
    public decimal TripCountGrowth { get; set; }
    public decimal DistanceGrowth { get; set; }
    public decimal UsageHoursGrowth { get; set; }
    public decimal UtilizationRateGrowth { get; set; }
}

/// <summary>
/// Benchmark comparison data
/// </summary>
public class BenchmarkData
{
    /// <summary>
    /// Comparison to group average
    /// </summary>
    public GroupComparison GroupAverage { get; set; } = new();

    /// <summary>
    /// Comparison to similar vehicles (same model/year)
    /// </summary>
    public GroupComparison SimilarVehicles { get; set; } = new();

    /// <summary>
    /// Vehicle rank in group (1 = best)
    /// </summary>
    public int GroupRank { get; set; }

    /// <summary>
    /// Total vehicles in group
    /// </summary>
    public int TotalVehiclesInGroup { get; set; }
}

/// <summary>
/// Group comparison metrics
/// </summary>
public class GroupComparison
{
    public decimal AverageTrips { get; set; }
    public decimal AverageDistance { get; set; }
    public decimal AverageUtilizationRate { get; set; }
    public decimal AverageCostPerKm { get; set; }

    // Differences (this vehicle - average)
    public decimal TripsDifference { get; set; }
    public decimal DistanceDifference { get; set; }
    public decimal UtilizationDifference { get; set; }
    public decimal CostPerKmDifference { get; set; }

    // Percentage differences
    public decimal TripsPercentDifference { get; set; }
    public decimal DistancePercentDifference { get; set; }
    public decimal UtilizationPercentDifference { get; set; }
}

/// <summary>
/// Most frequent user information
/// </summary>
public class MostFrequentUser
{
    public Guid UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int TripCount { get; set; }
    public decimal TotalDistance { get; set; }
    public decimal TotalUsageHours { get; set; }
}
