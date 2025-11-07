using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CoOwnershipVehicle.Vehicle.Api.DTOs
{
    /// <summary>
    /// Request DTO for cost analysis with date range and grouping options
    /// </summary>
    public class CostAnalysisRequest
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        [RegularExpression("^(month|quarter|year)$", ErrorMessage = "GroupBy must be 'month', 'quarter', or 'year'")]
        public string GroupBy { get; set; } = "month";
    }

    /// <summary>
    /// Complete cost analysis response
    /// </summary>
    public class CostAnalysisResponse
    {
        public Guid VehicleId { get; set; }
        public string VehicleName { get; set; } = string.Empty;
        public string PlateNumber { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalDays { get; set; }

        // Expense breakdown by category
        public ExpenseBreakdown ExpenseBreakdown { get; set; } = new();

        // Total and aggregated costs
        public TotalCosts TotalCosts { get; set; } = new();

        // Cost per unit metrics
        public CostPerUnit CostPerUnit { get; set; } = new();

        // Cost trends over time
        public List<CostTrendDataPoint> CostTrends { get; set; } = new();

        // Budget comparison
        public BudgetComparison? BudgetComparison { get; set; }

        // Depreciation analysis
        public DepreciationAnalysis Depreciation { get; set; } = new();

        // ROI for co-ownership
        public CoOwnershipROI ROI { get; set; } = new();

        // Cost optimization suggestions
        public List<CostOptimizationSuggestion> Suggestions { get; set; } = new();
    }

    /// <summary>
    /// Breakdown of expenses by category
    /// </summary>
    public class ExpenseBreakdown
    {
        public decimal MaintenanceCosts { get; set; }
        public decimal InsuranceCosts { get; set; }
        public decimal RegistrationLicensingCosts { get; set; }
        public decimal ChargingFuelCosts { get; set; }
        public decimal CleaningCosts { get; set; }
        public decimal RepairCosts { get; set; }
        public decimal ParkingTollsCosts { get; set; }
        public decimal OtherCosts { get; set; }

        public decimal TotalExpenses => MaintenanceCosts + InsuranceCosts +
            RegistrationLicensingCosts + ChargingFuelCosts + CleaningCosts +
            RepairCosts + ParkingTollsCosts + OtherCosts;
    }

    /// <summary>
    /// Total costs and aggregations
    /// </summary>
    public class TotalCosts
    {
        public decimal AllTimeCost { get; set; }
        public decimal PeriodCost { get; set; }
        public decimal AverageMonthlyCost { get; set; }
    }

    /// <summary>
    /// Cost per unit metrics
    /// </summary>
    public class CostPerUnit
    {
        public decimal? CostPerKilometer { get; set; }
        public decimal? CostPerUsageHour { get; set; }
        public decimal? CostPerTrip { get; set; }
    }

    /// <summary>
    /// Cost trend data point for time series
    /// </summary>
    public class CostTrendDataPoint
    {
        public DateTime Date { get; set; }
        public string Period { get; set; } = string.Empty; // "2025-01", "Q1 2025", "2025"
        public decimal TotalCost { get; set; }
        public decimal MaintenanceCost { get; set; }
        public decimal OperationalCost { get; set; } // Charging + parking + tolls
        public decimal OtherCost { get; set; }
    }

    /// <summary>
    /// Budget comparison if budget is set
    /// </summary>
    public class BudgetComparison
    {
        public decimal MonthlyBudget { get; set; }
        public decimal ActualMonthlyAverage { get; set; }
        public decimal BudgetVariance { get; set; } // Actual - Budget
        public decimal BudgetVariancePercent { get; set; } // ((Actual - Budget) / Budget) * 100
        public bool IsOverBudget { get; set; }
    }

    /// <summary>
    /// Depreciation analysis
    /// </summary>
    public class DepreciationAnalysis
    {
        public decimal? PurchasePrice { get; set; }
        public decimal? CurrentEstimatedValue { get; set; }
        public decimal? DepreciationRate { get; set; } // Annual %
        public decimal? TotalDepreciation { get; set; }
        public int VehicleAgeMonths { get; set; }
    }

    /// <summary>
    /// ROI for co-ownership model
    /// </summary>
    public class CoOwnershipROI
    {
        public int NumberOfOwners { get; set; }
        public decimal TotalCostPerOwner { get; set; }
        public decimal EstimatedIndividualOwnershipCost { get; set; }
        public decimal SavingsPerOwner { get; set; }
        public decimal SavingsPercent { get; set; }
    }

    /// <summary>
    /// Cost optimization suggestion
    /// </summary>
    public class CostOptimizationSuggestion
    {
        public string Category { get; set; } = string.Empty;
        public string Issue { get; set; } = string.Empty;
        public string Suggestion { get; set; } = string.Empty;
        public decimal? PotentialSavings { get; set; }
        public string Priority { get; set; } = "Medium"; // Low, Medium, High
    }
}
