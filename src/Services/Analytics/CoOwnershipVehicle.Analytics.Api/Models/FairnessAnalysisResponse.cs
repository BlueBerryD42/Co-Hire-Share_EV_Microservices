namespace CoOwnershipVehicle.Analytics.Api.Models;

public class FairnessAnalysisResponse
{
	public Guid GroupId { get; set; }
	public DateTime PeriodStart { get; set; }
	public DateTime PeriodEnd { get; set; }
	public decimal GroupFairnessScore { get; set; } // 0-100
	public decimal FairnessIndex { get; set; } // alias for group score or composite
	public decimal GiniCoefficient { get; set; } // 0-1
	public decimal StandardDeviationFromOwnership { get; set; } // percentage points
	public List<MemberFairness> Members { get; set; } = new();
	public FairnessAlerts Alerts { get; set; } = new();
	public FairnessRecommendations Recommendations { get; set; } = new();
	public FairnessVisualization Visualization { get; set; } = new();
	public List<FairnessTrendPoint> Trend { get; set; } = new();
}

public class MemberFairness
{
	public Guid UserId { get; set; }
	public string? UserFirstName { get; set; }
	public string? UserLastName { get; set; }
	public decimal OwnershipPercentage { get; set; } // 0-100
	public decimal UsagePercentage { get; set; } // 0-100
	public decimal FairnessScore { get; set; } // (Usage% / Ownership%) * 100
	public bool IsOverUtilizer { get; set; }
	public bool IsUnderUtilizer { get; set; }
}

public class FairnessAlerts
{
	public bool HasSevereOverUtilizers { get; set; }
	public bool HasSevereUnderUtilizers { get; set; }
	public bool GroupFairnessLow { get; set; }
	public List<Guid> OverUtilizerUserIds { get; set; } = new();
	public List<Guid> UnderUtilizerUserIds { get; set; } = new();
}

public class FairnessRecommendations
{
	public List<string> GroupRecommendations { get; set; } = new();
	public Dictionary<Guid, List<string>> MemberRecommendations { get; set; } = new();
}

public class FairnessVisualization
{
	public List<OwnershipVsUsagePoint> OwnershipVsUsageChart { get; set; } = new();
	public List<FairnessTrendPoint> FairnessTimeline { get; set; } = new();
	public List<MemberComparisonPoint> MemberComparison { get; set; } = new();
}

public class OwnershipVsUsagePoint
{
	public Guid UserId { get; set; }
	public decimal OwnershipPercentage { get; set; }
	public decimal UsagePercentage { get; set; }
}

public class FairnessTrendPoint
{
	public DateTime PeriodStart { get; set; }
	public DateTime PeriodEnd { get; set; }
	public decimal GroupFairnessScore { get; set; }
}

public class MemberComparisonPoint
{
	public Guid UserId { get; set; }
	public decimal FairnessScore { get; set; }
}









