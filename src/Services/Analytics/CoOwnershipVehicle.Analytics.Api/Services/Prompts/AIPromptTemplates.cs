using System.Text;
using CoOwnershipVehicle.Analytics.Api.Models;
using CoOwnershipVehicle.Shared.Contracts.DTOs;

namespace CoOwnershipVehicle.Analytics.Api.Services.Prompts;

public static class AIPromptTemplates
{
	public static string BuildFairnessAnalysisPrompt(
		Guid groupId,
		DateTime periodStart,
		DateTime periodEnd,
		List<MemberFairnessData> members,
		List<FairnessTrendPoint> historicalTrends)
	{
		var sb = new StringBuilder();
		sb.AppendLine("You are an AI assistant analyzing fairness in vehicle co-ownership groups.");
		sb.AppendLine("Analyze the following data and return a JSON response matching the FairnessAnalysisResponse format.");
		sb.AppendLine();
		sb.AppendLine($"Group ID: {groupId}");
		sb.AppendLine($"Analysis Period: {periodStart:yyyy-MM-dd} to {periodEnd:yyyy-MM-dd}");
		sb.AppendLine();
		sb.AppendLine("Member Data:");
		foreach (var member in members)
		{
			sb.AppendLine($"  - User {member.UserId}: Ownership {member.OwnershipPercentage:F2}%, Usage {member.UsagePercentage:F2}%");
		}
		sb.AppendLine();
		sb.AppendLine("Historical Trends:");
		foreach (var trend in historicalTrends.Take(6))
		{
			sb.AppendLine($"  - {trend.PeriodStart:yyyy-MM} to {trend.PeriodEnd:yyyy-MM}: Score {trend.GroupFairnessScore:F2}");
		}
		sb.AppendLine();
		sb.AppendLine("Calculate:");
		sb.AppendLine("1. Group fairness score (0-100, where 100 is perfectly fair)");
		sb.AppendLine("2. Gini coefficient for usage distribution (0-1)");
		sb.AppendLine("3. Standard deviation from ownership percentages");
		sb.AppendLine("4. Individual member fairness scores");
		sb.AppendLine("5. Identify overutilizers (>150% of fair share) and underutilizers (<50%)");
		sb.AppendLine("6. Provide actionable recommendations for improving fairness");
		sb.AppendLine();
		sb.AppendLine("Return JSON in this exact format:");
		sb.AppendLine("{");
		sb.AppendLine("  \"groupId\": \"guid\",");
		sb.AppendLine("  \"periodStart\": \"2024-01-01T00:00:00Z\",");
		sb.AppendLine("  \"periodEnd\": \"2024-03-31T23:59:59Z\",");
		sb.AppendLine("  \"groupFairnessScore\": 85.5,");
		sb.AppendLine("  \"fairnessIndex\": 85.5,");
		sb.AppendLine("  \"giniCoefficient\": 0.25,");
		sb.AppendLine("  \"standardDeviationFromOwnership\": 12.3,");
		sb.AppendLine("  \"members\": [{\"userId\": \"guid\", \"ownershipPercentage\": 50.0, \"usagePercentage\": 45.0, \"fairnessScore\": 90.0, \"isOverUtilizer\": false, \"isUnderUtilizer\": true}],");
		sb.AppendLine("  \"alerts\": {\"hasSevereOverUtilizers\": false, \"hasSevereUnderUtilizers\": true, \"groupFairnessLow\": false, \"overUtilizerUserIds\": [], \"underUtilizerUserIds\": [\"guid\"]},");
		sb.AppendLine("  \"recommendations\": {\"groupRecommendations\": [\"string\"], \"memberRecommendations\": {\"guid\": [\"string\"]}},");
		sb.AppendLine("  \"visualization\": {\"ownershipVsUsageChart\": [], \"fairnessTimeline\": [], \"memberComparison\": []},");
		sb.AppendLine("  \"trend\": []");
		sb.AppendLine("}");
		
		return sb.ToString();
	}

	public static string BuildBookingSuggestionPrompt(
		Guid userId,
		Guid groupId,
		DateTime? preferredDate,
		int durationMinutes,
		decimal userFairnessScore,
		decimal groupFairnessScore,
		List<HistoricalBookingPattern> bookingPatterns,
		List<BookingDto>? existingBookings = null)
	{
		var sb = new StringBuilder();
		sb.AppendLine("You are an AI assistant suggesting optimal booking times for vehicle co-ownership.");
		sb.AppendLine("Analyze the following data and return a JSON response with booking suggestions.");
		sb.AppendLine();
		sb.AppendLine($"User ID: {userId}");
		sb.AppendLine($"Group ID: {groupId}");
		if (preferredDate.HasValue)
		{
			sb.AppendLine($"Preferred Date: {preferredDate.Value:yyyy-MM-dd HH:mm} (optional)");
		}
		else
		{
			sb.AppendLine("Preferred Date: Not specified");
		}
		sb.AppendLine($"Duration: {durationMinutes} minutes");
		sb.AppendLine($"User Fairness Score: {userFairnessScore:F2}% (100% = fair share)");
		sb.AppendLine($"Group Fairness Score: {groupFairnessScore:F2}%");
		sb.AppendLine();
		
		// Add existing bookings information to help avoid conflicts
		if (existingBookings != null && existingBookings.Any())
		{
			sb.AppendLine("Existing Bookings (DO NOT suggest overlapping times):");
			foreach (var booking in existingBookings.Take(20))
			{
				sb.AppendLine($"  - {booking.StartAt:yyyy-MM-dd HH:mm} to {booking.EndAt:yyyy-MM-dd HH:mm}");
			}
			sb.AppendLine();
		}
		
		sb.AppendLine("Historical Booking Patterns:");
		foreach (var pattern in bookingPatterns.Take(10))
		{
			sb.AppendLine($"  - {pattern.DayOfWeek} at {pattern.Hour}:00 - {pattern.Frequency} bookings");
		}
		sb.AppendLine();
		sb.AppendLine("IMPORTANT: Suggest 5 optimal booking time slots following these priorities:");
		sb.AppendLine("1. PRIORITY: Keep the SAME DAY as preferred date, but suggest DIFFERENT HOURS if conflicts exist");
		sb.AppendLine("   - If preferred time has conflicts, suggest alternative hours on the SAME DAY first");
		sb.AppendLine("   - Only suggest different days if no available hours exist on the preferred day");
		sb.AppendLine("   - DO NOT keep the same hour and just change the day");
		sb.AppendLine("2. User's current fairness (prioritize underutilizers)");
		sb.AppendLine("3. Historical booking preferences");
		sb.AppendLine("4. Group balance and avoiding conflicts with existing bookings");
		sb.AppendLine("5. Peak vs off-peak times");
		sb.AppendLine();
		sb.AppendLine("Return JSON in this exact format:");
		sb.AppendLine("{");
		sb.AppendLine("  \"userId\": \"guid\",");
		sb.AppendLine("  \"groupId\": \"guid\",");
		sb.AppendLine("  \"suggestions\": [");
		sb.AppendLine("    {\"start\": \"2024-01-15T08:00:00Z\", \"end\": \"2024-01-15T10:00:00Z\", \"confidence\": 0.85, \"reasons\": [\"Off-peak time\", \"Matches your historical pattern\"]}");
		sb.AppendLine("  ]");
		sb.AppendLine("}");
		
		return sb.ToString();
	}

	public static string BuildUsagePredictionPrompt(
		Guid groupId,
		DateTime historyStart,
		DateTime historyEnd,
		Dictionary<DateTime, double> dailyUsage,
		Dictionary<int, double> dayOfWeekAverages,
		double trendPercentage,
		List<MemberUsagePattern> memberPatterns)
	{
		var sb = new StringBuilder();
		sb.AppendLine("You are an AI assistant predicting vehicle usage patterns.");
		sb.AppendLine("Analyze historical data and predict usage for the next 30 days.");
		sb.AppendLine();
		sb.AppendLine($"Group ID: {groupId}");
		sb.AppendLine($"History Period: {historyStart:yyyy-MM-dd} to {historyEnd:yyyy-MM-dd}");
		sb.AppendLine($"Trend: {trendPercentage:F1}% change (positive = increasing)");
		sb.AppendLine();
		sb.AppendLine("Daily Usage (last 30 days):");
		foreach (var day in dailyUsage.OrderByDescending(kv => kv.Key).Take(30))
		{
			sb.AppendLine($"  - {day.Key:yyyy-MM-dd}: {day.Value:F2} hours");
		}
		sb.AppendLine();
		sb.AppendLine("Day of Week Averages:");
		var dayNames = new[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
		for (int i = 0; i < 7; i++)
		{
			if (dayOfWeekAverages.ContainsKey(i))
			{
				sb.AppendLine($"  - {dayNames[i]}: {dayOfWeekAverages[i]:F2} hours");
			}
		}
		sb.AppendLine();
		sb.AppendLine("Member Usage Patterns:");
		foreach (var pattern in memberPatterns.Take(10))
		{
			sb.AppendLine($"  - User {pattern.UserId}: {pattern.UsageShare:F2}% share, prefers {pattern.PreferredDayOfWeek} {pattern.PreferredHour}:00");
		}
		sb.AppendLine();
		sb.AppendLine("Predict:");
		sb.AppendLine("1. Daily usage for next 30 days with confidence scores");
		sb.AppendLine("2. Peak hours (hours 0-23 with relative load 0-1)");
		sb.AppendLine("3. Member-specific time slot likelihoods");
		sb.AppendLine("4. Insights about patterns and trends");
		sb.AppendLine("5. Anomalies (unusual spikes/drops)");
		sb.AppendLine("6. Potential bottlenecks");
		sb.AppendLine("7. Recommendations for optimization");
		sb.AppendLine();
		sb.AppendLine("Return JSON matching UsagePredictionResponse format with all required fields.");
		
		return sb.ToString();
	}

	public static string BuildCostOptimizationPrompt(
		Guid groupId,
		string groupName,
		DateTime periodStart,
		DateTime periodEnd,
		CostAnalysisSummary summary,
		List<HighCostArea> highCostAreas,
		CostEfficiencyMetrics efficiencyMetrics,
		Dictionary<string, decimal> expensesByType,
		Dictionary<string, decimal> expensesByMonth)
	{
		var sb = new StringBuilder();
		sb.AppendLine("You are an AI assistant analyzing costs and providing optimization recommendations.");
		sb.AppendLine("Analyze the following expense data and provide actionable cost-saving recommendations.");
		sb.AppendLine();
		sb.AppendLine($"Group: {groupName} (ID: {groupId})");
		sb.AppendLine($"Period: {periodStart:yyyy-MM-dd} to {periodEnd:yyyy-MM-dd}");
		sb.AppendLine($"Total Expenses: ${summary.TotalExpenses:F2}");
		sb.AppendLine($"Average Monthly: ${summary.AverageMonthlyExpenses:F2}");
		sb.AppendLine();
		sb.AppendLine("Expenses by Type:");
		foreach (var expense in expensesByType.OrderByDescending(kv => kv.Value))
		{
			sb.AppendLine($"  - {expense.Key}: ${expense.Value:F2}");
		}
		sb.AppendLine();
		sb.AppendLine("Expenses by Month:");
		foreach (var month in expensesByMonth.OrderBy(kv => kv.Key))
		{
			sb.AppendLine($"  - {month.Key}: ${month.Value:F2}");
		}
		sb.AppendLine();
		sb.AppendLine("Efficiency Metrics:");
		sb.AppendLine($"  - Cost per km: ${efficiencyMetrics.CostPerKilometer:F2}");
		sb.AppendLine($"  - Cost per trip: ${efficiencyMetrics.CostPerTrip:F2}");
		sb.AppendLine($"  - Cost per member: ${efficiencyMetrics.CostPerMember:F2}");
		sb.AppendLine($"  - Total km: {efficiencyMetrics.TotalKilometers}");
		sb.AppendLine($"  - Total trips: {efficiencyMetrics.TotalTrips}");
		sb.AppendLine();
		sb.AppendLine("High Cost Areas:");
		foreach (var area in highCostAreas.Take(5))
		{
			sb.AppendLine($"  - {area.Category}: ${area.TotalAmount:F2} ({area.Count} expenses, {area.PercentageOfTotal:F1}% of total)");
		}
		sb.AppendLine();
		sb.AppendLine("Provide:");
		sb.AppendLine("1. Specific cost optimization recommendations with estimated savings");
		sb.AppendLine("2. Cost predictions for next month and quarter");
		sb.AppendLine("3. Spending alerts (budget exceeded, unusual spikes)");
		sb.AppendLine("4. ROI calculations for potential investments");
		sb.AppendLine("5. Benchmark comparisons (industry, similar vehicles, similar groups)");
		sb.AppendLine();
		sb.AppendLine("Return JSON matching CostOptimizationResponse format with all required fields.");
		
		return sb.ToString();
	}

	public static string BuildPredictiveMaintenancePrompt(
		Guid vehicleId,
		string make,
		string model,
		int year,
		int vehicleAge,
		decimal odometer)
	{
		var sb = new StringBuilder();
		sb.AppendLine("You are an AI assistant providing predictive maintenance analysis for electric vehicles.");
		sb.AppendLine("Analyze the vehicle data and predict potential maintenance issues.");
		sb.AppendLine();
		sb.AppendLine($"Vehicle ID: {vehicleId}");
		sb.AppendLine($"Vehicle: {year} {make} {model}");
		sb.AppendLine($"Vehicle Age: {vehicleAge} years");
		sb.AppendLine($"Odometer: {odometer:N0} km");
		sb.AppendLine();
		sb.AppendLine("Provide:");
		sb.AppendLine("1. Health score (0-100, where 100 is excellent condition)");
		sb.AppendLine("2. Predicted issues with severity, likelihood, timeline, and cost estimates");
		sb.AppendLine("3. Suggested maintenance bundles that can save money");
		sb.AppendLine();
		sb.AppendLine("Return JSON in this exact format:");
		sb.AppendLine("{");
		sb.AppendLine("  \"vehicleId\": \"guid\",");
		sb.AppendLine("  \"vehicleName\": \"string\",");
		sb.AppendLine("  \"healthScore\": 75.0,");
		sb.AppendLine("  \"predictedIssues\": [");
		sb.AppendLine("    {\"type\": \"Battery\", \"name\": \"Battery Degradation\", \"severity\": \"Medium\", \"likelihood\": 0.6, \"timeline\": \"6-12 months\", \"costRange\": {\"min\": 5000, \"max\": 15000}, \"recommendation\": \"string\"}");
		sb.AppendLine("  ],");
		sb.AppendLine("  \"suggestedBundles\": [");
		sb.AppendLine("    {\"title\": \"string\", \"services\": [\"string\"], \"potentialSavings\": 150}");
		sb.AppendLine("  ],");
		sb.AppendLine("  \"generatedAt\": \"2024-01-01T00:00:00Z\"");
		sb.AppendLine("}");
		
		return sb.ToString();
	}
}

// Helper classes for prompt building
public class MemberFairnessData
{
	public Guid UserId { get; set; }
	public decimal OwnershipPercentage { get; set; }
	public decimal UsagePercentage { get; set; }
}

public class HistoricalBookingPattern
{
	public string DayOfWeek { get; set; } = string.Empty;
	public int Hour { get; set; }
	public int Frequency { get; set; }
}

public class MemberUsagePattern
{
	public Guid UserId { get; set; }
	public decimal UsageShare { get; set; }
	public string PreferredDayOfWeek { get; set; } = string.Empty;
	public int PreferredHour { get; set; }
}

