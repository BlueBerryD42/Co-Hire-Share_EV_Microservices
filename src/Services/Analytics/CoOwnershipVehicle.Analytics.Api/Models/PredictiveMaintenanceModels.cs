namespace CoOwnershipVehicle.Analytics.Api.Models;

public class PredictiveMaintenanceResponse
{
	public Guid VehicleId { get; set; }
	public string VehicleName { get; set; } = string.Empty;
	public decimal HealthScore { get; set; } // 0-100
	public List<PredictedIssue> PredictedIssues { get; set; } = new();
	public List<MaintenanceBundle> SuggestedBundles { get; set; } = new();
	public DateTime GeneratedAt { get; set; }
}

public class PredictedIssue
{
	public string Type { get; set; } = string.Empty; // e.g., "Battery", "Tires", "Brakes"
	public string Name { get; set; } = string.Empty; // e.g., "Battery Degradation", "Tire Wear"
	public string Severity { get; set; } = string.Empty; // "Low", "Medium", "High", "Critical"
	public decimal Likelihood { get; set; } // 0-1
	public string Timeline { get; set; } = string.Empty; // e.g., "2-3 months", "6-12 months"
	public CostRange? CostRange { get; set; }
	public string Recommendation { get; set; } = string.Empty;
}

public class CostRange
{
	public decimal Min { get; set; }
	public decimal Max { get; set; }
}

public class MaintenanceBundle
{
	public string Title { get; set; } = string.Empty;
	public List<string> Services { get; set; } = new();
	public decimal PotentialSavings { get; set; }
}

