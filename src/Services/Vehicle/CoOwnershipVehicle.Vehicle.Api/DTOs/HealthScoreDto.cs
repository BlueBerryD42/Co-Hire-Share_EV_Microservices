using System.ComponentModel.DataAnnotations;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Vehicle.Api.DTOs;

/// <summary>
/// Request parameters for health score calculation
/// </summary>
public class HealthScoreRequest
{
    /// <summary>
    /// Whether to include historical data (default: true)
    /// </summary>
    public bool IncludeHistory { get; set; } = true;

    /// <summary>
    /// Whether to include benchmark comparison (default: true)
    /// </summary>
    public bool IncludeBenchmark { get; set; } = true;

    /// <summary>
    /// Number of months for historical trend (default: 6)
    /// </summary>
    [Range(1, 24)]
    public int HistoryMonths { get; set; } = 6;
}

/// <summary>
/// Complete vehicle health score response
/// </summary>
public class HealthScoreResponse
{
    public Guid VehicleId { get; set; }
    public string VehicleName { get; set; } = string.Empty;
    public string PlateNumber { get; set; } = string.Empty;
    public DateTime CalculatedAt { get; set; }

    /// <summary>
    /// Overall health score (0-100)
    /// </summary>
    public decimal OverallScore { get; set; }

    /// <summary>
    /// Health category: Excellent, Good, Fair, Poor, Critical
    /// </summary>
    public HealthCategory Category { get; set; }

    /// <summary>
    /// Color indicator for UI
    /// </summary>
    public string ColorIndicator { get; set; } = string.Empty;

    /// <summary>
    /// Breakdown of score by component
    /// </summary>
    public ScoreBreakdown Breakdown { get; set; } = new();

    /// <summary>
    /// Factors positively affecting the score
    /// </summary>
    public List<HealthFactor> PositiveFactors { get; set; } = new();

    /// <summary>
    /// Factors negatively affecting the score
    /// </summary>
    public List<HealthFactor> NegativeFactors { get; set; } = new();

    /// <summary>
    /// Actionable recommendations to improve score
    /// </summary>
    public List<Recommendation> Recommendations { get; set; } = new();

    /// <summary>
    /// Active health alerts
    /// </summary>
    public List<HealthAlert> Alerts { get; set; } = new();

    /// <summary>
    /// Historical score trend (optional)
    /// </summary>
    public List<HistoricalScore>? HistoricalTrend { get; set; }

    /// <summary>
    /// Benchmark comparison (optional)
    /// </summary>
    public BenchmarkComparison? Benchmark { get; set; }

    /// <summary>
    /// Predicted future health trend
    /// </summary>
    public FuturePrediction? Prediction { get; set; }
}

/// <summary>
/// Health score breakdown by component
/// </summary>
public class ScoreBreakdown
{
    /// <summary>
    /// Maintenance adherence score (0-30 points, 30% weight)
    /// </summary>
    public ComponentScore MaintenanceAdherence { get; set; } = new();

    /// <summary>
    /// Odometer vs age score (0-20 points, 20% weight)
    /// </summary>
    public ComponentScore OdometerVsAge { get; set; } = new();

    /// <summary>
    /// Damage reports score (0-20 points, 20% weight)
    /// </summary>
    public ComponentScore DamageReports { get; set; } = new();

    /// <summary>
    /// Service frequency score (0-15 points, 15% weight)
    /// </summary>
    public ComponentScore ServiceFrequency { get; set; } = new();

    /// <summary>
    /// Vehicle age score (0-10 points, 10% weight)
    /// </summary>
    public ComponentScore VehicleAge { get; set; } = new();

    /// <summary>
    /// Recent inspection results score (0-5 points, 5% weight)
    /// </summary>
    public ComponentScore InspectionResults { get; set; } = new();
}

/// <summary>
/// Individual component score details
/// </summary>
public class ComponentScore
{
    /// <summary>
    /// Component name
    /// </summary>
    public string ComponentName { get; set; } = string.Empty;

    /// <summary>
    /// Points earned (out of max points)
    /// </summary>
    public decimal Points { get; set; }

    /// <summary>
    /// Maximum possible points for this component
    /// </summary>
    public decimal MaxPoints { get; set; }

    /// <summary>
    /// Percentage contribution to total score
    /// </summary>
    public decimal Weight { get; set; }

    /// <summary>
    /// Description of how this was calculated
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Status: Excellent, Good, Fair, Poor
    /// </summary>
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Factor affecting health score
/// </summary>
public class HealthFactor
{
    public string Description { get; set; } = string.Empty;
    public decimal Impact { get; set; }
    public string Category { get; set; } = string.Empty;
}

/// <summary>
/// Actionable recommendation
/// </summary>
public class Recommendation
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Priority Priority { get; set; }
    public decimal PotentialScoreIncrease { get; set; }
    public string ActionType { get; set; } = string.Empty; // "maintenance", "inspection", "repair"
}

/// <summary>
/// Health alert
/// </summary>
public class HealthAlert
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public AlertSeverity Severity { get; set; }
    public DateTime CreatedAt { get; set; }
    public string AlertType { get; set; } = string.Empty; // "score_drop", "overdue_maintenance", "damage"
}

/// <summary>
/// Historical score data point
/// </summary>
public class HistoricalScore
{
    public DateTime Date { get; set; }
    public decimal Score { get; set; }
    public HealthCategory Category { get; set; }
    public string Note { get; set; } = string.Empty;
}

/// <summary>
/// Benchmark comparison to similar vehicles
/// </summary>
public class BenchmarkComparison
{
    /// <summary>
    /// Average score of similar vehicles (same model, similar age)
    /// </summary>
    public decimal AverageScore { get; set; }

    /// <summary>
    /// This vehicle's percentile (0-100, higher is better)
    /// </summary>
    public decimal Percentile { get; set; }

    /// <summary>
    /// Number of vehicles in comparison group
    /// </summary>
    public int ComparisonGroupSize { get; set; }

    /// <summary>
    /// Comparison summary
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Top performer score
    /// </summary>
    public decimal TopPerformerScore { get; set; }

    /// <summary>
    /// Comparison criteria
    /// </summary>
    public BenchmarkCriteria Criteria { get; set; } = new();
}

/// <summary>
/// Benchmark criteria
/// </summary>
public class BenchmarkCriteria
{
    public string Model { get; set; } = string.Empty;
    public int? MinYear { get; set; }
    public int? MaxYear { get; set; }
    public int? MinOdometer { get; set; }
    public int? MaxOdometer { get; set; }
}

/// <summary>
/// Future health prediction
/// </summary>
public class FuturePrediction
{
    /// <summary>
    /// Predicted score in 1 month
    /// </summary>
    public decimal OneMonthPrediction { get; set; }

    /// <summary>
    /// Predicted score in 3 months
    /// </summary>
    public decimal ThreeMonthPrediction { get; set; }

    /// <summary>
    /// Predicted score in 6 months
    /// </summary>
    public decimal SixMonthPrediction { get; set; }

    /// <summary>
    /// Trend direction: improving, stable, declining
    /// </summary>
    public string Trend { get; set; } = string.Empty;

    /// <summary>
    /// Confidence level (0-100)
    /// </summary>
    public decimal Confidence { get; set; }

    /// <summary>
    /// Key factors affecting prediction
    /// </summary>
    public List<string> KeyFactors { get; set; } = new();
}

/// <summary>
/// Simplified health score for vehicle list view
/// </summary>
public class VehicleHealthSummary
{
    public Guid VehicleId { get; set; }
    public decimal OverallScore { get; set; }
    public HealthCategory Category { get; set; }
    public string ColorIndicator { get; set; } = string.Empty;
    public int AlertCount { get; set; }
    public DateTime LastCalculated { get; set; }
}

// Enums
// Note: HealthCategory is defined in CoOwnershipVehicle.Domain.Entities

public enum Priority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

public enum AlertSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2,
    Critical = 3
}
