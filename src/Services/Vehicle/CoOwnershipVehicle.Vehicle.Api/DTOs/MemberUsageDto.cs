using System;
using System.Collections.Generic;

namespace CoOwnershipVehicle.Vehicle.Api.DTOs
{
    /// <summary>
    /// Request DTO for member usage analysis
    /// </summary>
    public class MemberUsageRequest
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    /// <summary>
    /// Complete member usage analysis response
    /// </summary>
    public class MemberUsageResponse
    {
        public Guid VehicleId { get; set; }
        public string VehicleName { get; set; } = string.Empty;
        public string PlateNumber { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalDays { get; set; }

        // Per-member usage breakdown
        public List<MemberUsageBreakdown> MemberUsages { get; set; } = new();

        // Fairness analysis
        public FairnessAnalysis Fairness { get; set; } = new();

        // Visualization data
        public VisualizationData Visualization { get; set; } = new();

        // Usage trends over time
        public List<MemberUsageTrend> Trends { get; set; } = new();
    }

    /// <summary>
    /// Usage breakdown for a single member
    /// </summary>
    public class MemberUsageBreakdown
    {
        public Guid MemberId { get; set; }
        public string MemberName { get; set; } = string.Empty;
        public string MemberEmail { get; set; } = string.Empty;
        public decimal OwnershipPercentage { get; set; }

        // Usage metrics
        public int NumberOfTrips { get; set; }
        public decimal TotalDistanceDriven { get; set; }
        public decimal TotalTimeUsed { get; set; } // Hours
        public decimal PercentageOfTotalUsage { get; set; }
        public decimal AverageTripLength { get; set; } // Kilometers
        public decimal AverageTripDuration { get; set; } // Hours

        // Fairness metrics
        public decimal UsageToOwnershipRatio { get; set; }
        public decimal FairnessDelta { get; set; } // Usage % - Ownership %
        public string UsageStatus { get; set; } = string.Empty; // "Fair", "Overutilizing", "Underutilizing"

        // Preferred patterns
        public List<string> PreferredDaysOfWeek { get; set; } = new();
        public List<int> PreferredHoursOfDay { get; set; } = new();
    }

    /// <summary>
    /// Fairness analysis across all members
    /// </summary>
    public class FairnessAnalysis
    {
        public decimal AverageFairnessScore { get; set; } // 0-100, higher is more fair
        public List<MemberFairnessScore> MemberScores { get; set; } = new();
        public List<Guid> Overutilizers { get; set; } = new();
        public List<Guid> Underutilizers { get; set; } = new();
        public List<string> FairnessRecommendations { get; set; } = new();
    }

    /// <summary>
    /// Fairness score for a member
    /// </summary>
    public class MemberFairnessScore
    {
        public Guid MemberId { get; set; }
        public string MemberName { get; set; } = string.Empty;
        public decimal Score { get; set; } // 0-100
        public string Status { get; set; } = string.Empty;
    }

    /// <summary>
    /// Data for visualization (charts)
    /// </summary>
    public class VisualizationData
    {
        // Pie chart: Usage by member
        public List<ChartDataPoint> UsagePieChart { get; set; } = new();

        // Bar chart: Trips by member
        public List<ChartDataPoint> TripsByMember { get; set; } = new();

        // Bar chart: Distance by member
        public List<ChartDataPoint> DistanceByMember { get; set; } = new();

        // Bar chart: Time by member
        public List<ChartDataPoint> TimeByMember { get; set; } = new();
    }

    /// <summary>
    /// Chart data point
    /// </summary>
    public class ChartDataPoint
    {
        public string Label { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public string Color { get; set; } = string.Empty; // Hex color for chart
    }

    /// <summary>
    /// Usage trend for a member over time
    /// </summary>
    public class MemberUsageTrend
    {
        public Guid MemberId { get; set; }
        public string MemberName { get; set; } = string.Empty;
        public List<MemberTrendDataPoint> DataPoints { get; set; } = new();
        public string TrendDirection { get; set; } = string.Empty; // "Increasing", "Stable", "Decreasing"
    }

    /// <summary>
    /// Trend data point for member usage time series
    /// </summary>
    public class MemberTrendDataPoint
    {
        public DateTime Date { get; set; }
        public string Period { get; set; } = string.Empty; // "2024-11"
        public int Trips { get; set; }
        public decimal Distance { get; set; }
        public decimal Hours { get; set; }
    }
}
