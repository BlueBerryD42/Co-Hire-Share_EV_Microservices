namespace CoOwnershipVehicle.Analytics.Api.Models;

public class CostOptimizationResponse
{
	public Guid GroupId { get; set; }
	public string GroupName { get; set; } = string.Empty;
	public DateTime PeriodStart { get; set; }
	public DateTime PeriodEnd { get; set; }
	public DateTime GeneratedAt { get; set; }
	
	// Cost Analysis
	public CostAnalysisSummary Summary { get; set; } = new();
	public List<HighCostArea> HighCostAreas { get; set; } = new();
	
	// Efficiency Metrics
	public CostEfficiencyMetrics EfficiencyMetrics { get; set; } = new();
	public BenchmarkComparisons Benchmarks { get; set; } = new();
	
	// Recommendations
	public List<CostRecommendation> Recommendations { get; set; } = new();
	
	// Predictions
	public CostPrediction Predictions { get; set; } = new();
	
	// Alerts
	public List<SpendingAlert> Alerts { get; set; } = new();
	
	// ROI Calculations
	public List<ROICalculation> ROICalculations { get; set; } = new();
	
	public bool InsufficientData { get; set; }
}

public class CostAnalysisSummary
{
	public decimal TotalExpenses { get; set; }
	public decimal AverageMonthlyExpenses { get; set; }
	public int TotalExpenseCount { get; set; }
	public Dictionary<string, decimal> ExpensesByType { get; set; } = new();
	public Dictionary<string, decimal> ExpensesByMonth { get; set; } = new();
}

public class HighCostArea
{
	public string Category { get; set; } = string.Empty;
	public decimal TotalAmount { get; set; }
	public int Count { get; set; }
	public decimal AverageAmount { get; set; }
	public string? ProviderName { get; set; }
	public string Description { get; set; } = string.Empty;
	public bool IsRecurring { get; set; }
	public decimal PercentageOfTotal { get; set; }
}

public class CostEfficiencyMetrics
{
	public decimal CostPerKilometer { get; set; }
	public decimal CostPerTrip { get; set; }
	public decimal CostPerMember { get; set; }
	public decimal CostPerHour { get; set; }
	public int TotalKilometers { get; set; }
	public int TotalTrips { get; set; }
	public int TotalMembers { get; set; }
	public int TotalHours { get; set; }
}

public class BenchmarkComparisons
{
	public BenchmarkComparison VehicleComparison { get; set; } = new();
	public BenchmarkComparison GroupComparison { get; set; } = new();
	public BenchmarkComparison IndustryComparison { get; set; } = new();
}

public class BenchmarkComparison
{
	public string BenchmarkName { get; set; } = string.Empty;
	public decimal BenchmarkCostPerKm { get; set; }
	public decimal YourCostPerKm { get; set; }
	public decimal BenchmarkCostPerTrip { get; set; }
	public decimal YourCostPerTrip { get; set; }
	public decimal VariancePercentage { get; set; }
	public string Status { get; set; } = string.Empty; // "Above Average", "Average", "Below Average"
}

public class CostRecommendation
{
	public string Title { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public decimal EstimatedSavings { get; set; }
	public decimal EstimatedSavingsPercentage { get; set; }
	public string Category { get; set; } = string.Empty;
	public string Priority { get; set; } = string.Empty; // "High", "Medium", "Low"
	public string? ProviderName { get; set; }
	public string? ActionRequired { get; set; }
}

public class CostPrediction
{
	public decimal NextMonthPrediction { get; set; }
	public decimal NextQuarterPrediction { get; set; }
	public decimal ConfidenceScore { get; set; } // 0-1
	public List<UpcomingExpense> UpcomingExpenses { get; set; } = new();
	public List<MonthlyPrediction> MonthlyForecast { get; set; } = new();
}

public class UpcomingExpense
{
	public string Description { get; set; } = string.Empty;
	public decimal EstimatedAmount { get; set; }
	public DateTime ExpectedDate { get; set; }
	public string Category { get; set; } = string.Empty;
	public string Reason { get; set; } = string.Empty;
}

public class MonthlyPrediction
{
	public DateTime Month { get; set; }
	public decimal PredictedAmount { get; set; }
	public decimal Confidence { get; set; }
}

public class SpendingAlert
{
	public string Type { get; set; } = string.Empty; // "BudgetExceeded", "UnusualSpike", "RecurringOvercharge"
	public string Title { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public decimal Amount { get; set; }
	public decimal? BudgetThreshold { get; set; }
	public DateTime AlertDate { get; set; }
	public string Severity { get; set; } = string.Empty; // "High", "Medium", "Low"
}

public class ROICalculation
{
	public string Title { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public decimal InvestmentAmount { get; set; }
	public decimal ExpectedSavings { get; set; }
	public decimal ExpectedSavingsPerYear { get; set; }
	public decimal PaybackPeriodMonths { get; set; }
	public decimal ROIPercentage { get; set; }
	public string Scenario { get; set; } = string.Empty; // "Maintenance vs Replacement", "Lease vs Own", "Upgrade"
}

