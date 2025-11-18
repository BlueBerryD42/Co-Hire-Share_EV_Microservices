namespace CoOwnershipVehicle.Analytics.Api.Models;

public class UsagePredictionResponse
{
	public Guid GroupId { get; set; }
	public DateTime GeneratedAt { get; set; }
	public DateTime HistoryStart { get; set; }
	public DateTime HistoryEnd { get; set; }
	public bool InsufficientHistory { get; set; }
	public List<DayPrediction> Next30Days { get; set; } = new();
	public List<PeakHourPrediction> PeakHours { get; set; } = new();
	public List<MemberTimeSlotLikelihood> MemberLikelyUsage { get; set; } = new();
	public List<PredictionInsight> Insights { get; set; } = new();
	public List<PredictionAnomaly> Anomalies { get; set; } = new();
	public List<BottleneckPrediction> Bottlenecks { get; set; } = new();
	public List<PredictionRecommendation> Recommendations { get; set; } = new();
}

public class DayPrediction
{
	public DateTime Date { get; set; }
	public decimal ExpectedUsageHours { get; set; }
	public decimal Confidence { get; set; } // 0-1
}

public class PeakHourPrediction
{
	public int Hour { get; set; } // 0-23
	public decimal RelativeLoad { get; set; } // 0-1
	public decimal Confidence { get; set; } // 0-1
}

public class MemberTimeSlotLikelihood
{
	public Guid UserId { get; set; }
	public int DayOfWeek { get; set; } // 0-6
	public int Hour { get; set; }
	public decimal Likelihood { get; set; } // 0-1
}

public class PredictionInsight
{
	public string Message { get; set; } = string.Empty;
}

public class PredictionAnomaly
{
	public DateTime? PeriodStart { get; set; }
	public DateTime? PeriodEnd { get; set; }
	public string Description { get; set; } = string.Empty;
}

public class BottleneckPrediction
{
	public string Description { get; set; } = string.Empty;
	public decimal Confidence { get; set; }
}

public class PredictionRecommendation
{
	public string Action { get; set; } = string.Empty;
	public string Rationale { get; set; } = string.Empty;
}












