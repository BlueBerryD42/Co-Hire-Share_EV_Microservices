using CoOwnershipVehicle.Analytics.Api.Data;
using CoOwnershipVehicle.Analytics.Api.Data.Entities;
using CoOwnershipVehicle.Analytics.Api.Models;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Data;
using Microsoft.EntityFrameworkCore;

namespace CoOwnershipVehicle.Analytics.Api.Services;

public class AIService : IAIService
{
	private readonly AnalyticsDbContext _context;
	private readonly ApplicationDbContext _mainContext;
	private readonly ILogger<AIService> _logger;

	public AIService(AnalyticsDbContext context, ApplicationDbContext mainContext, ILogger<AIService> logger)
	{
		_context = context;
		_mainContext = mainContext;
		_logger = logger;
	}

	public async Task<FairnessAnalysisResponse?> CalculateFairnessAsync(Guid groupId, DateTime? startDate, DateTime? endDate)
	{
		var periodEnd = endDate?.ToUniversalTime() ?? DateTime.UtcNow;
		var periodStart = startDate?.ToUniversalTime() ?? periodEnd.AddDays(-90);

		// Pull user analytics for the group in the window
		var userAnalytics = await _context.UserAnalytics
			.Where(u => u.GroupId == groupId && u.PeriodEnd >= periodStart && u.PeriodStart <= periodEnd)
			.ToListAsync();

		// If no analytics data at all for this group, consider group not found if no signals exist
		if (!userAnalytics.Any())
		{
			var hasSignals = await _context.AnalyticsSnapshots.AnyAsync(s => s.GroupId == groupId)
				|| await _context.FairnessTrends.AnyAsync(t => t.GroupId == groupId)
				|| await _context.UserAnalytics.AnyAsync(u => u.GroupId == groupId);
			if (!hasSignals)
			{
				return null;
			}
		}

		// Aggregate by user
		var members = userAnalytics
			.GroupBy(x => x.UserId)
			.Select(g => new
			{
				UserId = g.Key,
				Ownership = g.Average(x => (double)x.OwnershipShare),
				Usage = g.Average(x => (double)x.UsageShare),
				TotalUsageHours = g.Sum(x => x.TotalUsageHours)
			})
			.ToList();

		if (members.Count == 0)
		{
			return new FairnessAnalysisResponse
			{
				GroupId = groupId,
				PeriodStart = periodStart,
				PeriodEnd = periodEnd,
				GroupFairnessScore = 0,
				FairnessIndex = 0,
				GiniCoefficient = 0,
				StandardDeviationFromOwnership = 0,
				Members = new List<MemberFairness>()
			};
		}

		// Normalize usage if UsageShare not present: derive from usage hours
		var anyUsageShareMissing = members.Any(m => double.IsNaN(m.Usage) || double.IsInfinity(m.Usage));
		if (anyUsageShareMissing || members.All(m => m.Usage == 0))
		{
			var totalHours = members.Sum(m => m.TotalUsageHours);
			if (totalHours > 0)
			{
				members = members.Select(m => new
				{
					m.UserId,
					m.Ownership,
					Usage = (double)m.TotalUsageHours / totalHours,
					m.TotalUsageHours
				}).ToList();
			}
		}

		// Prepare member fairness
		var memberFairness = new List<MemberFairness>();
		foreach (var m in members)
		{
			var ownershipPct = (decimal)(m.Ownership * 100.0);
			var usagePct = (decimal)(m.Usage * 100.0);
			var fairness = ownershipPct == 0 ? 0 : (usagePct / (ownershipPct == 0 ? 1 : ownershipPct)) * 100m;
			memberFairness.Add(new MemberFairness
			{
				UserId = m.UserId,
				OwnershipPercentage = Decimal.Round(ownershipPct, 2),
				UsagePercentage = Decimal.Round(usagePct, 2),
				FairnessScore = Decimal.Round(fairness, 2),
				IsOverUtilizer = fairness > 100m,
				IsUnderUtilizer = fairness < 100m
			});
		}

		// Metrics
		var deviations = memberFairness
			.Select(m => (double)Math.Abs(m.UsagePercentage - m.OwnershipPercentage))
			.ToList();
		var mapd = deviations.Average(); // mean absolute percentage deviation (in p.p.)
		var stddev = StdDev(memberFairness.Select(m => (double)(m.UsagePercentage - m.OwnershipPercentage)).ToList());
		var gini = GiniCoefficient(memberFairness.Select(m => (double)m.UsagePercentage / 100.0).ToList());
		var groupScore = Math.Max(0, 100 - mapd);

		// Alerts
		var overSevere = memberFairness.Where(m => m.FairnessScore >= 150m).Select(m => m.UserId).ToList();
		var underSevere = memberFairness.Where(m => m.FairnessScore <= 50m).Select(m => m.UserId).ToList();
		var groupLow = (decimal)groupScore < 70m;

		// Recommendations
		var recommendations = new FairnessRecommendations();
		if (groupLow)
		{
			recommendations.GroupRecommendations.Add("Establish usage quotas aligned with ownership shares.");
			recommendations.GroupRecommendations.Add("Encourage equitable booking through priority windows for underutilizers.");
			recommendations.GroupRecommendations.Add("Consider rebalancing ownership shares to match sustained usage patterns.");
		}
		foreach (var m in memberFairness)
		{
			var list = new List<string>();
			if (m.FairnessScore < 100m)
			{
				list.Add("Book more during available off-peak times.");
				list.Add("Enable reminders for upcoming availability windows.");
			}
			else if (m.FairnessScore > 100m)
			{
				list.Add("Reduce peak-time bookings to allow balance.");
				list.Add("Shift some usage to off-peak slots.");
			}
			recommendations.MemberRecommendations[m.UserId] = list;
		}

		// Visualization
		var viz = new FairnessVisualization
		{
			OwnershipVsUsageChart = memberFairness.Select(m => new OwnershipVsUsagePoint
			{
				UserId = m.UserId,
				OwnershipPercentage = m.OwnershipPercentage,
				UsagePercentage = m.UsagePercentage
			}).ToList(),
			MemberComparison = memberFairness.Select(m => new MemberComparisonPoint
			{
				UserId = m.UserId,
				FairnessScore = m.FairnessScore
			}).ToList()
		};

		// Trend storage: upsert current window, and build timeline from stored data
		var trendPoints = new List<FairnessTrendPoint>();
		// Upsert current period trend record
		try
		{
			var existing = await _context.FairnessTrends.FirstOrDefaultAsync(t => t.GroupId == groupId && t.PeriodStart == periodStart && t.PeriodEnd == periodEnd);
			if (existing == null)
			{
				_context.FairnessTrends.Add(new FairnessTrend
				{
					GroupId = groupId,
					PeriodStart = periodStart,
					PeriodEnd = periodEnd,
					GroupFairnessScore = (decimal)Math.Round(groupScore, 2)
				});
				await _context.SaveChangesAsync();
			}
			else if (existing.GroupFairnessScore != (decimal)Math.Round(groupScore, 2))
			{
				existing.GroupFairnessScore = (decimal)Math.Round(groupScore, 2);
				await _context.SaveChangesAsync();
			}
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Failed to upsert fairness trend for group {GroupId}", groupId);
		}

		// Retrieve last 6 months (including current) from stored trends; fill gaps by computing on the fly
		var endAnchor = new DateTime(periodEnd.Year, periodEnd.Month, 1);
		for (int i = 5; i >= 0; i--)
		{
			var monthStart = endAnchor.AddMonths(-i);
			var monthEnd = monthStart.AddMonths(1).AddTicks(-1);
			var stored = await _context.FairnessTrends.FirstOrDefaultAsync(t => t.GroupId == groupId && t.PeriodStart == monthStart && t.PeriodEnd == monthEnd);
			if (stored != null)
			{
				trendPoints.Add(new FairnessTrendPoint { PeriodStart = monthStart, PeriodEnd = monthEnd, GroupFairnessScore = stored.GroupFairnessScore });
				continue;
			}

			// Fallback compute from user analytics for that month
			var monthData = userAnalytics
				.Where(x => x.PeriodEnd >= monthStart && x.PeriodStart <= monthEnd)
				.GroupBy(x => x.UserId)
				.Select(g => new
				{
					Ownership = g.Average(x => (double)x.OwnershipShare),
					Usage = g.Average(x => (double)x.UsageShare),
					TotalUsageHours = g.Sum(x => x.TotalUsageHours)
				})
				.ToList();
			if (monthData.Count == 0)
			{
				trendPoints.Add(new FairnessTrendPoint { PeriodStart = monthStart, PeriodEnd = monthEnd, GroupFairnessScore = 0 });
				continue;
			}
			if (monthData.All(m => m.Usage == 0))
			{
				var totalH = monthData.Sum(m => m.TotalUsageHours);
				if (totalH > 0)
				{
					monthData = monthData.Select(m => new { m.Ownership, Usage = (double)m.TotalUsageHours / totalH, m.TotalUsageHours }).ToList();
				}
			}
			var monthMapd = monthData
				.Select(m => Math.Abs(m.Usage * 100.0 - m.Ownership * 100.0))
				.Average();
			var monthScore = Math.Max(0, 100 - monthMapd);
			trendPoints.Add(new FairnessTrendPoint { PeriodStart = monthStart, PeriodEnd = monthEnd, GroupFairnessScore = (decimal)Math.Round(monthScore, 2) });
		}
		viz.FairnessTimeline = trendPoints;

		var response = new FairnessAnalysisResponse
		{
			GroupId = groupId,
			PeriodStart = periodStart,
			PeriodEnd = periodEnd,
			GroupFairnessScore = (decimal)Math.Round(groupScore, 2),
			FairnessIndex = (decimal)Math.Round(groupScore, 2),
			GiniCoefficient = (decimal)Math.Round(gini, 4),
			StandardDeviationFromOwnership = (decimal)Math.Round(stddev, 2),
			Members = memberFairness
		};

		response.Alerts = new FairnessAlerts
		{
			HasSevereOverUtilizers = overSevere.Any(),
			HasSevereUnderUtilizers = underSevere.Any(),
			GroupFairnessLow = groupLow,
			OverUtilizerUserIds = overSevere,
			UnderUtilizerUserIds = underSevere
		};
		response.Recommendations = recommendations;
		response.Visualization = viz;
		response.Trend = trendPoints;

		return response;
	}

	public async Task<SuggestBookingResponse?> SuggestBookingTimesAsync(SuggestBookingRequest request)
	{
		// Validate data existence in analytics context
		var hasUser = await _context.UserAnalytics.AnyAsync(u => u.GroupId == request.GroupId && u.UserId == request.UserId);
		var hasGroup = await _context.AnalyticsSnapshots.AnyAsync(s => s.GroupId == request.GroupId)
			|| await _context.FairnessTrends.AnyAsync(t => t.GroupId == request.GroupId);
		if (!hasUser && !hasGroup)
		{
			return null;
		}

		// Determine a target date window
		var baseDateLocal = (request.PreferredDate ?? DateTime.UtcNow).Date;
		var duration = TimeSpan.FromMinutes(Math.Max(30, request.DurationMinutes));

		// Compute fairness for prioritization
		var fairness = await CalculateFairnessAsync(request.GroupId, baseDateLocal.AddMonths(-3), baseDateLocal.AddDays(1));
		decimal userFairness = 100m;
		if (fairness != null)
		{
			var member = fairness.Members.FirstOrDefault(m => m.UserId == request.UserId);
			if (member != null)
			{
				userFairness = member.FairnessScore;
			}
		}

		// Heuristic availability and conflict avoidance
		// Define peak and off-peak hours
		var peakRanges = new List<(int startHour, int endHour)> { (7, 9), (17, 21) };
		var offPeakRanges = new List<(int startHour, int endHour)> { (9, 12), (13, 17), (21, 23) };

		bool isPeak(DateTime dt)
		{
			var hour = dt.Hour;
			return peakRanges.Any(r => hour >= r.startHour && hour < r.endHour) && dt.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday;
		}

		bool isOffPeak(DateTime dt)
		{
			var hour = dt.Hour;
			var weekday = dt.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday;
			return offPeakRanges.Any(r => hour >= r.startHour && hour < r.endHour) || !weekday; // weekends mostly off-peak
		}

		// Generate candidate slots over next 7 days around preferred date
		var candidates = new List<(DateTime start, DateTime end, List<string> reasons, double score)>();
		for (int dayOffset = 0; dayOffset < 7; dayOffset++)
		{
			var day = baseDateLocal.AddDays(dayOffset);
			// propose 6 fixed windows per day
			var starts = new[] { 8, 10, 12, 14, 18, 20 };
			foreach (var h in starts)
			{
				var start = new DateTime(day.Year, day.Month, day.Day, h, 0, 0, DateTimeKind.Utc);
				var end = start.Add(duration);
				var reasons = new List<string>();
				double score = 0.5; // base

				// Favor underutilizers; discourage overutilizers during peak
				if (userFairness < 100m)
				{
					reasons.Add($"Suggested because you're underutilizing ({Math.Round((double)userFairness)}% of fair share)");
					score += (double)Math.Min(0.4m, (100m - userFairness) / 250m); // up to +0.4
				}
				else if (userFairness > 120m)
				{
					reasons.Add("Off-peak time to allow others peak access");
					if (isPeak(start)) score -= 0.2;
					if (isOffPeak(start)) score += 0.2;
				}

				// Historical group usage proxy via fairness group score
				if (fairness != null)
				{
					if (fairness.GroupFairnessScore < 70m && isPeak(start))
					{
						reasons.Add("Group imbalance detected; avoid peak to reduce conflicts");
						score -= 0.1;
					}
				}

				// Preference alignment: around preferred date time-of-day if provided
				if (request.PreferredDate.HasValue)
				{
					var prefHour = request.PreferredDate.Value.Hour;
					var hourDelta = Math.Abs(prefHour - h);
					var align = Math.Max(0, 1.0 - hourDelta / 6.0);
					if (align > 0.5) reasons.Add("This time has historically worked well for you");
					score += align * 0.2; // up to +0.2
				}

				// Conflict prediction heuristic: avoid overlapping with common peaks
				if (isPeak(start))
				{
					reasons.Add("Likely peak period; potential conflicts");
					score -= 0.15;
				}
				else
				{
					reasons.Add("Lower expected conflicts");
					score += 0.1;
				}

				candidates.Add((start, end, reasons, Math.Clamp(score, 0.0, 1.0)));
			}
		}

		// Rank and select top 5
		var top = candidates
			.OrderByDescending(c => c.score)
			.ThenBy(c => c.start)
			.Take(5)
			.Select(c => new BookingSuggestion
			{
				Start = c.start,
				End = c.end,
				Confidence = (decimal)Math.Round(c.score, 2),
				Reasons = c.reasons.Distinct().ToList()
			})
			.ToList();

		return new SuggestBookingResponse
		{
			UserId = request.UserId,
			GroupId = request.GroupId,
			Suggestions = top
		};
	}

	public async Task<UsagePredictionResponse?> GetUsagePredictionsAsync(Guid groupId)
	{
		// Pull history (last 120 days) from analytics snapshots and user analytics
		var end = DateTime.UtcNow;
		var start = end.AddDays(-120);
		var userHistory = await _context.UserAnalytics
			.Where(u => u.GroupId == groupId && u.PeriodEnd >= start && u.PeriodStart <= end)
			.ToListAsync();
		var snapHistory = await _context.AnalyticsSnapshots
			.Where(s => s.GroupId == groupId && s.SnapshotDate >= start && s.SnapshotDate <= end)
			.ToListAsync();

		if (!userHistory.Any() && !snapHistory.Any())
		{
			var group = await _mainContext.OwnershipGroups.AsNoTracking().FirstOrDefaultAsync(g => g.Id == groupId);
			if (group == null)
			{
				return null; // unknown group
			}

			return new UsagePredictionResponse
			{
				GroupId = groupId,
				GeneratedAt = DateTime.UtcNow,
				HistoryStart = DateTime.UtcNow,
				HistoryEnd = DateTime.UtcNow,
				InsufficientHistory = true
			};
		}

		var historyStart = userHistory.Any() ? userHistory.Min(x => x.PeriodStart) : snapHistory.Min(x => x.SnapshotDate);
		var historyEnd = userHistory.Any() ? userHistory.Max(x => x.PeriodEnd) : snapHistory.Max(x => x.SnapshotDate);
		var historySpan = (historyEnd - historyStart).TotalDays;

		var response = new UsagePredictionResponse
		{
			GroupId = groupId,
			GeneratedAt = DateTime.UtcNow,
			HistoryStart = historyStart,
			HistoryEnd = historyEnd,
			InsufficientHistory = historySpan < 30
		};

		if (response.InsufficientHistory)
		{
			return response; // return empty payload per acceptance criteria
		}

		// Aggregate approximate daily usage using UserAnalytics TotalUsageHours equally over covered days
		var dayUsage = new Dictionary<DateTime, double>();
		foreach (var ua in userHistory)
		{
			var spanDays = Math.Max(1, (ua.PeriodEnd.Date - ua.PeriodStart.Date).Days + 1);
			var perDay = ua.TotalUsageHours / (double)spanDays;
			for (var d = ua.PeriodStart.Date; d <= ua.PeriodEnd.Date; d = d.AddDays(1))
			{
				if (!dayUsage.ContainsKey(d)) dayUsage[d] = 0;
				dayUsage[d] += perDay;
			}
		}

		// Trend detection: compare last 30 days vs previous 30 days
		var last30Start = historyEnd.Date.AddDays(-29);
		var prev30Start = last30Start.AddDays(-30);
		double sumLast = dayUsage.Where(kv => kv.Key >= last30Start && kv.Key <= historyEnd.Date).Sum(kv => kv.Value);
		double sumPrev = dayUsage.Where(kv => kv.Key >= prev30Start && kv.Key < last30Start).Sum(kv => kv.Value);
		double trendPct = (sumPrev <= 0) ? 0 : ((sumLast - sumPrev) / sumPrev) * 100.0;

		// Day-of-week pattern
		var dowAvg = Enumerable.Range(0, 7).ToDictionary(i => i, i => 0.0);
		var dowCounts = Enumerable.Range(0, 7).ToDictionary(i => i, i => 0);
		foreach (var kv in dayUsage)
		{
			var dow = (int)kv.Key.DayOfWeek;
			dowAvg[dow] += kv.Value;
			dowCounts[dow] += 1;
		}
		foreach (var k in dowAvg.Keys.ToList())
		{
			dowAvg[k] = dowCounts[k] == 0 ? 0 : dowAvg[k] / dowCounts[k];
		}

		// Peak hour heuristic: favor commute bands and evening based on weekday usage magnitude
		var weekdayLoad = Enumerable.Range(1, 5).Sum(d => dowAvg[d]);
		var weekendLoad = dowAvg[0] + dowAvg[6];
		var peakHours = new List<PeakHourPrediction>();
		var commuteWeight = weekdayLoad >= weekendLoad ? 1.0 : 0.5;
		var leisureWeight = weekendLoad > weekdayLoad ? 1.0 : 0.5;
		peakHours.Add(new PeakHourPrediction { Hour = 8, RelativeLoad = (decimal)Math.Min(1.0, 0.7 * commuteWeight), Confidence = 0.6m });
		peakHours.Add(new PeakHourPrediction { Hour = 9, RelativeLoad = (decimal)Math.Min(1.0, 0.6 * commuteWeight), Confidence = 0.6m });
		peakHours.Add(new PeakHourPrediction { Hour = 18, RelativeLoad = (decimal)Math.Min(1.0, 0.8 * commuteWeight), Confidence = 0.6m });
		peakHours.Add(new PeakHourPrediction { Hour = 19, RelativeLoad = (decimal)Math.Min(1.0, 0.7 * commuteWeight), Confidence = 0.6m });
		peakHours.Add(new PeakHourPrediction { Hour = 14, RelativeLoad = (decimal)Math.Min(1.0, 0.6 * leisureWeight), Confidence = 0.5m });

		response.PeakHours = peakHours;

		// Predict next 30 days usage by repeating DOW averages adjusted by trend
		var next30 = new List<DayPrediction>();
		var trendAdj = 1.0 + Math.Clamp(trendPct / 100.0, -0.5, 0.5);
		var baseline = dayUsage.Any() ? dayUsage.Values.DefaultIfEmpty(0).Average() : 0.0;
		for (int i = 1; i <= 30; i++)
		{
			var dt = historyEnd.Date.AddDays(i);
			var dow = (int)dt.DayOfWeek;
			var expected = (dowAvg[dow] == 0 ? baseline : dowAvg[dow]) * trendAdj;
			next30.Add(new DayPrediction
			{
				Date = dt,
				ExpectedUsageHours = (decimal)Math.Round(expected, 2),
				Confidence = (decimal)Math.Clamp(0.4 + (historySpan / 180.0), 0.4, 0.9)
			});
		}
		response.Next30Days = next30;

		// Member-specific likelihoods by usage share
		var memberShares = userHistory
			.GroupBy(x => x.UserId)
			.Select(g => new { UserId = g.Key, Share = g.Average(x => (double)x.UsageShare) })
			.Where(x => x.Share > 0)
			.ToList();
		var memberLikely = new List<MemberTimeSlotLikelihood>();
		foreach (var m in memberShares)
		{
			// Spread likelihood to commute/leisure hours across all days weighted by share
			int[] commute = { 8, 9, 18, 19 };
			int[] leisure = { 13, 14, 15, 16 };
			for (int dow = 0; dow < 7; dow++)
			{
				var isWeekend = dow == 0 || dow == 6;
				var hours = isWeekend ? leisure : commute;
				foreach (var h in hours)
				{
					memberLikely.Add(new MemberTimeSlotLikelihood
					{
						UserId = m.UserId,
						DayOfWeek = dow,
						Hour = h,
						Likelihood = (decimal)Math.Min(1.0, 0.5 * m.Share + (isWeekend ? 0.1 : 0.0))
					});
				}
			}
		}
		response.MemberLikelyUsage = memberLikely;

		// Seasonal patterns (rough heuristic by month averages)
		var monthAvg = dayUsage
			.GroupBy(kv => new { kv.Key.Year, kv.Key.Month })
			.Select(g => new { g.Key.Year, g.Key.Month, Avg = g.Average(x => x.Value) })
			.OrderBy(x => new DateTime(x.Year, x.Month, 1))
			.ToList();
		if (monthAvg.Count >= 3)
		{
			var first = monthAvg.First();
			var last = monthAvg.Last();
			if (last.Avg > first.Avg * 1.2)
				response.Insights.Add(new PredictionInsight { Message = "Usage increased significantly compared to earlier period" });
			if (last.Avg < first.Avg * 0.8)
				response.Insights.Add(new PredictionInsight { Message = "Usage decreased significantly compared to earlier period" });
		}

		// Anomaly detection: sudden spikes or drops day-to-day > 2.5x change
		var orderedDays = dayUsage.OrderBy(kv => kv.Key).ToList();
		for (int i = 1; i < orderedDays.Count; i++)
		{
			var prev = orderedDays[i - 1];
			var cur = orderedDays[i];
			if (prev.Value > 0 && (cur.Value > prev.Value * 2.5 || cur.Value < prev.Value * 0.4))
			{
				response.Anomalies.Add(new PredictionAnomaly
				{
					PeriodStart = cur.Key,
					PeriodEnd = cur.Key,
					Description = cur.Value > prev.Value ? "Sudden usage spike" : "Sudden usage drop"
				});
			}
		}

		// Bottlenecks: predict conflicts when predicted usage > 1.5x baseline on weekdays
		var mean = dayUsage.Values.DefaultIfEmpty(0).Average();
		foreach (var d in response.Next30Days)
		{
			if (d.Date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday && (double)d.ExpectedUsageHours > mean * 1.5)
			{
				response.Bottlenecks.Add(new BottleneckPrediction { Description = $"Potential conflicts on {d.Date:yyyy-MM-dd}", Confidence = 0.6m });
			}
		}

		// Recommendations
		response.Recommendations.Add(new PredictionRecommendation { Action = "Encourage off-peak bookings", Rationale = "Reduce peak conflicts and improve fairness" });
		response.Recommendations.Add(new PredictionRecommendation { Action = "Promote weekend usage", Rationale = "Leverage lower weekend load" });
		if (trendPct > 30)
		{
			response.Insights.Add(new PredictionInsight { Message = "Usage increased 30% compared to previous month" });
			response.Recommendations.Add(new PredictionRecommendation { Action = "Evaluate adding a second vehicle", Rationale = "Sustained growth and predicted bottlenecks" });
		}

		return response;
	}

	private static double StdDev(List<double> values)
	{
		if (values.Count == 0) return 0.0;
		var avg = values.Average();
		var variance = values.Average(v => Math.Pow(v - avg, 2));
		return Math.Sqrt(variance);
	}

	private static double GiniCoefficient(List<double> values01)
	{
		// values01 expected between 0 and 1
		var values = values01.Where(v => v >= 0).ToList();
		if (values.Count == 0) return 0.0;
		var mean = values.Average();
		if (mean == 0) return 0.0;
		values.Sort();
		double cumulative = 0;
		for (int i = 0; i < values.Count; i++)
		{
			cumulative += (2 * (i + 1) - values.Count - 1) * values[i];
		}
		var gini = cumulative / (values.Count * values.Sum());
		return Math.Abs(gini);
	}

	public async Task<CostOptimizationResponse?> GetCostOptimizationAsync(Guid groupId)
	{
		var periodEnd = DateTime.UtcNow;
		var periodStart = periodEnd.AddMonths(-12); // Last 12 months

		// Check if group exists
		var group = await _mainContext.OwnershipGroups
			.Include(g => g.Members)
			.Include(g => g.Vehicles)
			.FirstOrDefaultAsync(g => g.Id == groupId);

		if (group == null)
		{
			return null;
		}

		// Get all expenses for the group
		var expenses = await _mainContext.Expenses
			.Include(e => e.Vehicle)
			.Where(e => e.GroupId == groupId && e.DateIncurred >= periodStart && e.DateIncurred <= periodEnd)
			.ToListAsync();

		// Get bookings for distance calculations
		var bookings = await _mainContext.Bookings
			.Include(b => b.CheckIns)
			.Where(b => b.GroupId == groupId && b.StartAt >= periodStart && b.EndAt <= periodEnd && b.Status == BookingStatus.Completed)
			.ToListAsync();

		if (!expenses.Any())
		{
			return new CostOptimizationResponse
			{
				GroupId = groupId,
				GroupName = group.Name,
				PeriodStart = periodStart,
				PeriodEnd = periodEnd,
				GeneratedAt = DateTime.UtcNow,
				InsufficientData = true
			};
		}

		if (!bookings.Any())
		{
			return new CostOptimizationResponse
			{
				GroupId = groupId,
				GroupName = group.Name,
				PeriodStart = periodStart,
				PeriodEnd = periodEnd,
				GeneratedAt = DateTime.UtcNow,
				InsufficientData = true
			};
		}

		var response = new CostOptimizationResponse
		{
			GroupId = groupId,
			GroupName = group.Name,
			PeriodStart = periodStart,
			PeriodEnd = periodEnd,
			GeneratedAt = DateTime.UtcNow,
			InsufficientData = false
		};

		// Calculate total distance from check-ins
		var totalDistance = CalculateTotalDistance(bookings);
		var totalTrips = bookings.Count;
		var totalMembers = group.Members.Count;
		var totalHours = bookings.Sum(b => (int)(b.EndAt - b.StartAt).TotalHours);

		// 1. Cost Analysis Summary
		var totalExpenses = expenses.Sum(e => e.Amount);
		var expensesByType = expenses
			.GroupBy(e => e.ExpenseType)
			.ToDictionary(g => g.Key.ToString(), g => g.Sum(e => e.Amount));
		
		var expensesByMonth = expenses
			.GroupBy(e => new { e.DateIncurred.Year, e.DateIncurred.Month })
			.ToDictionary(g => $"{g.Key.Year}-{g.Key.Month:D2}", g => g.Sum(e => e.Amount));

		response.Summary = new CostAnalysisSummary
		{
			TotalExpenses = totalExpenses,
			AverageMonthlyExpenses = expensesByMonth.Any() ? expensesByMonth.Values.Average() : 0,
			TotalExpenseCount = expenses.Count,
			ExpensesByType = expensesByType,
			ExpensesByMonth = expensesByMonth
		};

		// 2. High-Cost Areas Analysis
		response.HighCostAreas = AnalyzeHighCostAreas(expenses, totalExpenses);

		// 3. Cost Efficiency Metrics
		response.EfficiencyMetrics = new CostEfficiencyMetrics
		{
			CostPerKilometer = totalDistance > 0 ? totalExpenses / totalDistance : 0,
			CostPerTrip = totalTrips > 0 ? totalExpenses / totalTrips : 0,
			CostPerMember = totalMembers > 0 ? totalExpenses / totalMembers : 0,
			CostPerHour = totalHours > 0 ? totalExpenses / totalHours : 0,
			TotalKilometers = (int)totalDistance,
			TotalTrips = totalTrips,
			TotalMembers = totalMembers,
			TotalHours = totalHours
		};

		// 4. Benchmark Comparisons
		response.Benchmarks = CalculateBenchmarks(response.EfficiencyMetrics, group.Vehicles.ToList());

		// 5. Generate Recommendations
		response.Recommendations = GenerateRecommendations(expenses, response.HighCostAreas, response.EfficiencyMetrics, response.Benchmarks, totalExpenses);

		// 6. Cost Predictions
		response.Predictions = GeneratePredictions(expenses, expensesByMonth, totalDistance, totalTrips);

		// 7. Spending Alerts
		response.Alerts = GenerateAlerts(expenses, expensesByMonth, totalExpenses);

		// 8. ROI Calculations
		response.ROICalculations = CalculateROI(expenses, response.EfficiencyMetrics, group.Vehicles.ToList());

		return response;
	}

	private decimal CalculateTotalDistance(List<Booking> bookings)
	{
		return bookings
			.SelectMany(b => b.CheckIns)
			.Where(c => c.Type == CheckInType.CheckOut)
			.Select(c =>
			{
				var booking = bookings.FirstOrDefault(b => b.Id == c.BookingId);
				var checkIn = booking?.CheckIns.FirstOrDefault(ci => ci.Type == CheckInType.CheckIn && ci.BookingId == c.BookingId);
				if (checkIn != null)
				{
					return Math.Max(0, c.Odometer - checkIn.Odometer);
				}
				return 0;
			})
			.Sum();
	}

	private List<HighCostArea> AnalyzeHighCostAreas(List<Expense> expenses, decimal totalExpenses)
	{
		var highCostAreas = new List<HighCostArea>();

		// Analyze by expense type
		var expensesByType = expenses
			.GroupBy(e => e.ExpenseType)
			.Select(g => new HighCostArea
			{
				Category = g.Key.ToString(),
				TotalAmount = g.Sum(e => e.Amount),
				Count = g.Count(),
				AverageAmount = g.Average(e => e.Amount),
				PercentageOfTotal = totalExpenses > 0 ? (g.Sum(e => e.Amount) / totalExpenses) * 100 : 0
			})
			.OrderByDescending(h => h.TotalAmount)
			.ToList();

		highCostAreas.AddRange(expensesByType);

		// Analyze maintenance providers (extract from description/notes)
		var maintenanceExpenses = expenses.Where(e => e.ExpenseType == ExpenseType.Maintenance || e.ExpenseType == ExpenseType.Repair).ToList();
		var providerGroups = new Dictionary<string, List<Expense>>();
		
		foreach (var exp in maintenanceExpenses)
		{
			// Try to extract provider name from description (common patterns)
			var providerName = ExtractProviderName(exp.Description, exp.Notes);
			if (string.IsNullOrEmpty(providerName))
			{
				providerName = "Unknown Provider";
			}

			if (!providerGroups.ContainsKey(providerName))
			{
				providerGroups[providerName] = new List<Expense>();
			}
			providerGroups[providerName].Add(exp);
		}

		// Add expensive providers
		foreach (var provider in providerGroups.OrderByDescending(p => p.Value.Sum(e => e.Amount)).Take(5))
		{
			var providerTotal = provider.Value.Sum(e => e.Amount);
			highCostAreas.Add(new HighCostArea
			{
				Category = "Maintenance Provider",
				TotalAmount = providerTotal,
				Count = provider.Value.Count,
				AverageAmount = providerTotal / provider.Value.Count,
				ProviderName = provider.Key,
				PercentageOfTotal = totalExpenses > 0 ? (providerTotal / totalExpenses) * 100 : 0,
				Description = $"Multiple expenses with {provider.Key}"
			});
		}

		// Identify frequent repairs (may indicate bigger issues)
		var repairExpenses = expenses.Where(e => e.ExpenseType == ExpenseType.Repair).ToList();
		if (repairExpenses.Count >= 3)
		{
			var repairTotal = repairExpenses.Sum(e => e.Amount);
			highCostAreas.Add(new HighCostArea
			{
				Category = "Frequent Repairs",
				TotalAmount = repairTotal,
				Count = repairExpenses.Count,
				AverageAmount = repairExpenses.Average(e => e.Amount),
				PercentageOfTotal = totalExpenses > 0 ? (repairTotal / totalExpenses) * 100 : 0,
				Description = $"High frequency of repairs ({repairExpenses.Count} repairs) may indicate underlying issues"
			});
		}

		return highCostAreas.OrderByDescending(h => h.TotalAmount).ToList();
	}

	private string? ExtractProviderName(string description, string? notes)
	{
		// Common provider name patterns in descriptions
		var text = $"{description} {notes ?? ""}";
		var commonProviders = new[] { "Dealer", "Service Center", "Auto Shop", "Garage", "Mechanic", "Workshop" };
		
		foreach (var provider in commonProviders)
		{
			if (text.Contains(provider, StringComparison.OrdinalIgnoreCase))
			{
				// Try to extract a more specific name
				var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
				for (int i = 0; i < words.Length - 1; i++)
				{
					if (words[i + 1].Contains(provider, StringComparison.OrdinalIgnoreCase))
					{
						return $"{words[i]} {provider}";
					}
				}
				return provider;
			}
		}
		
		return null;
	}

	private BenchmarkComparisons CalculateBenchmarks(CostEfficiencyMetrics metrics, List<Vehicle> vehicles)
	{
		var comparisons = new BenchmarkComparisons();

		// Industry benchmarks (typical EV co-ownership costs)
		var industryCostPerKm = 0.25m; // $0.25/km average for EV
		var industryCostPerTrip = 15.0m; // $15 per trip average
		
		var yourCostPerKm = metrics.CostPerKilometer;
		var yourCostPerTrip = metrics.CostPerTrip;

		comparisons.IndustryComparison = new BenchmarkComparison
		{
			BenchmarkName = "Industry Average",
			BenchmarkCostPerKm = industryCostPerKm,
			YourCostPerKm = yourCostPerKm,
			BenchmarkCostPerTrip = industryCostPerTrip,
			YourCostPerTrip = yourCostPerTrip,
			VariancePercentage = industryCostPerKm > 0 ? ((yourCostPerKm - industryCostPerKm) / industryCostPerKm) * 100 : 0,
			Status = yourCostPerKm <= industryCostPerKm * 1.1m ? "Below Average" : yourCostPerKm <= industryCostPerKm * 1.3m ? "Average" : "Above Average"
		};

		// Vehicle-specific benchmarks (based on vehicle type)
		if (vehicles.Any())
		{
			var avgVehicleAge = vehicles.Any() ? DateTime.UtcNow.Year - vehicles.Average(v => v.Year) : 0;
			var vehicleBenchmarkCostPerKm = avgVehicleAge < 3 ? 0.20m : avgVehicleAge < 7 ? 0.28m : 0.35m;
			
			comparisons.VehicleComparison = new BenchmarkComparison
			{
				BenchmarkName = $"Similar Vehicles ({avgVehicleAge:F0} years avg)",
				BenchmarkCostPerKm = vehicleBenchmarkCostPerKm,
				YourCostPerKm = yourCostPerKm,
				BenchmarkCostPerTrip = industryCostPerTrip,
				YourCostPerTrip = yourCostPerTrip,
				VariancePercentage = vehicleBenchmarkCostPerKm > 0 ? ((yourCostPerKm - vehicleBenchmarkCostPerKm) / vehicleBenchmarkCostPerKm) * 100 : 0,
				Status = yourCostPerKm <= vehicleBenchmarkCostPerKm * 1.1m ? "Below Average" : yourCostPerKm <= vehicleBenchmarkCostPerKm * 1.3m ? "Average" : "Above Average"
			};
		}

		// Group comparison (would need data from other groups - simplified)
		comparisons.GroupComparison = new BenchmarkComparison
		{
			BenchmarkName = "Similar Groups",
			BenchmarkCostPerKm = industryCostPerKm,
			YourCostPerKm = yourCostPerKm,
			BenchmarkCostPerTrip = industryCostPerTrip,
			YourCostPerTrip = yourCostPerTrip,
			VariancePercentage = comparisons.IndustryComparison.VariancePercentage,
			Status = comparisons.IndustryComparison.Status
		};

		return comparisons;
	}

	private List<CostRecommendation> GenerateRecommendations(
		List<Expense> expenses,
		List<HighCostArea> highCostAreas,
		CostEfficiencyMetrics metrics,
		BenchmarkComparisons benchmarks,
		decimal totalExpenses)
	{
		var recommendations = new List<CostRecommendation>();

		// Provider-specific recommendations
        var expensiveProviders = highCostAreas
			.Where(h => h.Category == "Maintenance Provider" && h.TotalAmount > 200)
			.OrderByDescending(h => h.TotalAmount)
			.ToList();

		foreach (var provider in expensiveProviders.Take(3))
		{
            var maintenanceAmounts = expenses
                .Where(e => e.ExpenseType == ExpenseType.Maintenance)
                .Select(e => e.Amount)
                .ToList();

            var avgMaintenanceCost = maintenanceAmounts.Any() ? maintenanceAmounts.Average() : 0m;
            var providerAverage = provider.Count > 0 ? provider.AverageAmount : avgMaintenanceCost;
            var potentialSavings = providerAverage * 0.15m; // Assume 15% savings with alternative provider
            if (provider.Count == 0 || providerAverage == 0)
            {
                continue;
            }
			
			recommendations.Add(new CostRecommendation
			{
				Title = $"Switch Maintenance Provider",
                Description = $"Consider switching from {provider.ProviderName} (avg ${providerAverage:F2}/service). Alternative providers may offer similar quality at lower cost.",
				EstimatedSavings = potentialSavings * provider.Count,
				EstimatedSavingsPercentage = 15,
				Category = "Provider Optimization",
				Priority = provider.TotalAmount > 500 ? "High" : "Medium",
				ProviderName = provider.ProviderName,
				ActionRequired = "Research alternative maintenance providers and compare quotes"
			});
		}

		// Preventive maintenance recommendation
		var repairCount = expenses.Count(e => e.ExpenseType == ExpenseType.Repair);
		var repairTotal = expenses.Where(e => e.ExpenseType == ExpenseType.Repair).Sum(e => e.Amount);
		
		if (repairCount >= 3)
		{
			recommendations.Add(new CostRecommendation
			{
				Title = "Increase Preventive Maintenance",
				Description = $"High frequency of repairs ({repairCount} repairs, ${repairTotal:F2} total) suggests preventive maintenance could reduce future repair costs.",
				EstimatedSavings = repairTotal * 0.3m, // Estimate 30% reduction
				EstimatedSavingsPercentage = 30,
				Category = "Preventive Maintenance",
				Priority = "High",
				ActionRequired = "Schedule regular preventive maintenance checks"
			});
		}

		// Insurance optimization
		var insuranceExpenses = expenses.Where(e => e.ExpenseType == ExpenseType.Insurance).ToList();
		if (insuranceExpenses.Any())
		{
			var annualInsurance = insuranceExpenses.Sum(e => e.Amount);
			var avgInsurance = insuranceExpenses.Any() ? insuranceExpenses.Average(e => e.Amount) : 0m;
			
			recommendations.Add(new CostRecommendation
			{
				Title = "Review Insurance Plan",
				Description = $"Current insurance costs (${annualInsurance:F2}/year) may be optimized based on usage patterns. Usage-based insurance could be more cost-effective.",
				EstimatedSavings = annualInsurance * 0.15m,
				EstimatedSavingsPercentage = 15,
				Category = "Insurance",
				Priority = "Medium",
				ActionRequired = "Compare insurance quotes and consider usage-based options"
			});
		}

		// Cleaning cost optimization
		var cleaningExpenses = expenses.Where(e => e.ExpenseType == ExpenseType.Cleaning).ToList();
		if (cleaningExpenses.Any())
		{
			var cleaningTotal = cleaningExpenses.Sum(e => e.Amount);
			var avgCleaningCost = cleaningExpenses.Any() ? cleaningExpenses.Average(e => e.Amount) : 0m;
			
			if (avgCleaningCost > 50)
			{
				recommendations.Add(new CostRecommendation
				{
					Title = "Optimize Cleaning Costs",
					Description = $"Average cleaning cost (${avgCleaningCost:F2}) is above typical range. Consider self-service options or package deals.",
					EstimatedSavings = cleaningTotal * 0.25m,
					EstimatedSavingsPercentage = 25,
					Category = "Cleaning",
					Priority = "Low",
					ActionRequired = "Explore self-service cleaning or negotiate package deals"
				});
			}
		}

		// Charging optimization (for EVs)
		var fuelExpenses = expenses.Where(e => e.ExpenseType == ExpenseType.Fuel).ToList();
		if (fuelExpenses.Any())
		{
			var fuelTotal = fuelExpenses.Sum(e => e.Amount);
			recommendations.Add(new CostRecommendation
			{
				Title = "Optimize Charging Schedule",
				Description = "Charging during off-peak hours can reduce electricity costs by 20-30%.",
				EstimatedSavings = fuelTotal * 0.25m,
				EstimatedSavingsPercentage = 25,
				Category = "Charging",
				Priority = "Medium",
				ActionRequired = "Schedule charging during off-peak hours (typically 10 PM - 6 AM)"
			});
		}

		// Cost per km optimization
		if (benchmarks.IndustryComparison.VariancePercentage > 20)
		{
			recommendations.Add(new CostRecommendation
			{
				Title = "Improve Cost Efficiency",
				Description = $"Cost per kilometer (${metrics.CostPerKilometer:F2}/km) is {benchmarks.IndustryComparison.VariancePercentage:F1}% above industry average. Review maintenance schedules and driving patterns.",
				EstimatedSavings = totalExpenses * 0.15m,
				EstimatedSavingsPercentage = 15,
				Category = "Efficiency",
				Priority = benchmarks.IndustryComparison.VariancePercentage > 40 ? "High" : "Medium",
				ActionRequired = "Review maintenance schedules, tire pressure, and driving efficiency"
			});
		}

		return recommendations.OrderByDescending(r => r.EstimatedSavings).ToList();
	}

	private CostPrediction GeneratePredictions(
		List<Expense> expenses,
		Dictionary<string, decimal> expensesByMonth,
		decimal totalDistance,
		int totalTrips)
	{
		var prediction = new CostPrediction
		{
			ConfidenceScore = expenses.Count >= 6 ? 0.75m : expenses.Count >= 3 ? 0.5m : 0.3m
		};

		// Calculate monthly average from historical data
		var monthlyAverages = expensesByMonth.Values.ToList();
		var avgMonthlyExpense = monthlyAverages.Any() ? monthlyAverages.Average() : 0;
		
		// Trend analysis
		var recentMonths = expensesByMonth.OrderByDescending(kv => kv.Key).Take(3).ToList();
		var olderMonths = expensesByMonth.OrderByDescending(kv => kv.Key).Skip(3).Take(3).ToList();
		
		var recentAvg = recentMonths.Any() ? recentMonths.Average(kv => kv.Value) : avgMonthlyExpense;
		var olderAvg = olderMonths.Any() ? olderMonths.Average(kv => kv.Value) : avgMonthlyExpense;
		
		var trendFactor = olderAvg > 0 ? (recentAvg - olderAvg) / olderAvg : 0;
		
		// Predict next month
		prediction.NextMonthPrediction = avgMonthlyExpense * (1 + trendFactor * 0.5m); // Apply 50% of trend
		
		// Predict next quarter
		prediction.NextQuarterPrediction = prediction.NextMonthPrediction * 3;

		// Upcoming expenses based on recurring patterns
		var recurringExpenses = expenses.Where(e => e.IsRecurring).ToList();
		var insuranceExpenses = expenses.Where(e => e.ExpenseType == ExpenseType.Insurance).ToList();
		var registrationExpenses = expenses.Where(e => e.ExpenseType == ExpenseType.Registration).ToList();

		// Predict insurance renewal (typically annual)
		if (insuranceExpenses.Any())
		{
			var lastInsurance = insuranceExpenses.OrderByDescending(e => e.DateIncurred).FirstOrDefault();
			if (lastInsurance != null && (DateTime.UtcNow - lastInsurance.DateIncurred).Days > 300)
			{
				prediction.UpcomingExpenses.Add(new UpcomingExpense
				{
					Description = "Insurance Renewal",
					EstimatedAmount = lastInsurance.Amount,
					ExpectedDate = lastInsurance.DateIncurred.AddYears(1),
					Category = "Insurance",
					Reason = "Annual insurance renewal based on historical pattern"
				});
			}
		}

		// Predict registration renewal
		if (registrationExpenses.Any())
		{
			var lastRegistration = registrationExpenses.OrderByDescending(e => e.DateIncurred).FirstOrDefault();
			if (lastRegistration != null)
			{
				var nextRenewal = lastRegistration.DateIncurred.AddYears(1);
				if (nextRenewal > DateTime.UtcNow && nextRenewal <= DateTime.UtcNow.AddMonths(3))
				{
					prediction.UpcomingExpenses.Add(new UpcomingExpense
					{
						Description = "Vehicle Registration Renewal",
						EstimatedAmount = lastRegistration.Amount,
						ExpectedDate = nextRenewal,
						Category = "Registration",
						Reason = "Annual registration renewal"
					});
				}
			}
		}

		// Predict maintenance based on distance
		var maintenanceExpenses = expenses.Where(e => e.ExpenseType == ExpenseType.Maintenance).ToList();
		if (maintenanceExpenses.Any() && totalDistance > 0)
		{
			var avgMaintenancePerKm = maintenanceExpenses.Sum(e => e.Amount) / totalDistance;
			var avgMaintenanceInterval = 10000; // km (typical maintenance interval)
			var estimatedNextMaintenance = avgMaintenancePerKm * avgMaintenanceInterval;
			
			prediction.UpcomingExpenses.Add(new UpcomingExpense
			{
				Description = "Scheduled Maintenance",
				EstimatedAmount = estimatedNextMaintenance,
				ExpectedDate = DateTime.UtcNow.AddMonths(2),
				Category = "Maintenance",
				Reason = "Based on average maintenance frequency and distance"
			});
		}

		// Monthly forecast for next 3 months
		for (int i = 1; i <= 3; i++)
		{
			var forecastMonth = DateTime.UtcNow.AddMonths(i);
			var monthKey = $"{forecastMonth.Year}-{forecastMonth.Month:D2}";
			
			prediction.MonthlyForecast.Add(new MonthlyPrediction
			{
				Month = new DateTime(forecastMonth.Year, forecastMonth.Month, 1),
				PredictedAmount = prediction.NextMonthPrediction * (1 + trendFactor * (i * 0.1m)),
				Confidence = Math.Max(0.3m, prediction.ConfidenceScore - (i * 0.1m))
			});
		}

		return prediction;
	}

	private List<SpendingAlert> GenerateAlerts(
		List<Expense> expenses,
		Dictionary<string, decimal> expensesByMonth,
		decimal totalExpenses)
	{
		var alerts = new List<SpendingAlert>();

		// Budget exceeded (using average + 20% as threshold)
		var avgMonthlyExpense = expensesByMonth.Values.Any() ? expensesByMonth.Values.Average() : 0;
		var budgetThreshold = avgMonthlyExpense * 1.2m;
		
		var recentMonths = expensesByMonth.OrderByDescending(kv => kv.Key).Take(1).ToList();
		foreach (var month in recentMonths)
		{
			if (month.Value > budgetThreshold)
			{
				alerts.Add(new SpendingAlert
				{
					Type = "BudgetExceeded",
					Title = "Monthly Budget Exceeded",
					Description = $"Spending in {month.Key} (${month.Value:F2}) exceeded budget threshold (${budgetThreshold:F2})",
					Amount = month.Value,
					BudgetThreshold = budgetThreshold,
					AlertDate = DateTime.UtcNow,
					Severity = month.Value > budgetThreshold * 1.5m ? "High" : "Medium"
				});
			}
		}

		// Unusual expense spikes
		if (expensesByMonth.Count >= 3)
		{
			var monthlyValues = expensesByMonth.Values.ToList();
			var avgMonthly = monthlyValues.Average();
			var stdDev = CalculateStdDev(monthlyValues.Select(v => (double)v).ToList());
			
			var lastMonth = expensesByMonth.OrderByDescending(kv => kv.Key).FirstOrDefault();
			if (lastMonth.Key != null && (double)lastMonth.Value > (double)avgMonthly + (2 * stdDev))
			{
				alerts.Add(new SpendingAlert
				{
					Type = "UnusualSpike",
					Title = "Unusual Expense Spike Detected",
					Description = $"Spending in {lastMonth.Key} (${lastMonth.Value:F2}) is significantly higher than average (${avgMonthly:F2})",
					Amount = lastMonth.Value,
					AlertDate = DateTime.UtcNow,
					Severity = "High"
				});
			}
		}

		// Recurring overcharges (check for expenses with similar amounts from same provider)
		var maintenanceExpenses = expenses
			.Where(e => e.ExpenseType == ExpenseType.Maintenance || e.ExpenseType == ExpenseType.Repair)
			.ToList();

		var expenseGroups = maintenanceExpenses
			.GroupBy(e => Math.Round(e.Amount / 10) * 10) // Group by similar amounts
			.Where(g => g.Count() >= 3)
			.ToList();

		foreach (var group in expenseGroups)
		{
			var avgAmount = group.Average(e => e.Amount);
			var variance = group.Select(e => Math.Abs(e.Amount - avgAmount)).Average();
			
			if (variance < avgAmount * 0.1m) // Very consistent amounts
			{
				alerts.Add(new SpendingAlert
				{
					Type = "RecurringOvercharge",
					Title = "Potential Recurring Overcharge",
					Description = $"Multiple similar expenses detected (${avgAmount:F2} average, {group.Count()} occurrences). Verify if consistent pricing is expected.",
					Amount = group.Sum(e => e.Amount),
					AlertDate = DateTime.UtcNow,
					Severity = "Medium"
				});
			}
		}

		return alerts;
	}

	private double CalculateStdDev(List<double> values)
	{
		if (values.Count == 0) return 0.0;
		var avg = values.Average();
		var variance = values.Average(v => Math.Pow(v - avg, 2));
		return Math.Sqrt(variance);
	}

	private List<ROICalculation> CalculateROI(
		List<Expense> expenses,
		CostEfficiencyMetrics metrics,
		List<Vehicle> vehicles)
	{
		var roiCalculations = new List<ROICalculation>();

		// Maintenance vs Replacement ROI
		var repairExpenses = expenses.Where(e => e.ExpenseType == ExpenseType.Repair).ToList();
		var maintenanceExpenses = expenses.Where(e => e.ExpenseType == ExpenseType.Maintenance).ToList();
		var annualRepairCost = repairExpenses.Sum(e => e.Amount) * 12 / Math.Max(1m, (decimal)((DateTime.UtcNow - expenses.Min(e => e.DateIncurred)).Days / 365.0));
		var annualMaintenanceCost = maintenanceExpenses.Sum(e => e.Amount) * 12 / Math.Max(1m, (decimal)((DateTime.UtcNow - expenses.Min(e => e.DateIncurred)).Days / 365.0));

		if (annualRepairCost > 2000 && vehicles.Any())
		{
			var vehicle = vehicles.First();
			var vehicleAge = DateTime.UtcNow.Year - vehicle.Year;
			var estimatedReplacementCost = 30000m; // Average EV cost
			
			// Calculate if replacement is more cost-effective
			var remainingRepairYears = Math.Max(1, 10 - vehicleAge);
			var totalRepairCostProjected = annualRepairCost * remainingRepairYears;
			
			if (totalRepairCostProjected > estimatedReplacementCost * 0.3m)
			{
				var savings = totalRepairCostProjected - estimatedReplacementCost;
				var paybackMonths = estimatedReplacementCost / (annualRepairCost / 12);
				
				roiCalculations.Add(new ROICalculation
				{
					Title = "Vehicle Replacement Analysis",
					Description = $"High repair costs (${annualRepairCost:F2}/year) suggest replacement may be more cost-effective long-term.",
					InvestmentAmount = estimatedReplacementCost,
					ExpectedSavings = Math.Max(0, savings),
					ExpectedSavingsPerYear = Math.Max(0, annualRepairCost - (estimatedReplacementCost * 0.05m)), // Assuming 5% depreciation
					PaybackPeriodMonths = paybackMonths,
					ROIPercentage = estimatedReplacementCost > 0 ? (savings / estimatedReplacementCost) * 100 : 0,
					Scenario = "Maintenance vs Replacement"
				});
			}
		}

		// Preventive Maintenance Upgrade ROI
		if (repairExpenses.Count >= 3)
		{
			var preventiveInvestment = 500m; // Annual preventive maintenance budget
			var estimatedRepairReduction = annualRepairCost * 0.3m; // 30% reduction
			var netSavings = estimatedRepairReduction - preventiveInvestment;
			
			roiCalculations.Add(new ROICalculation
			{
				Title = "Preventive Maintenance Program",
				Description = $"Investing ${preventiveInvestment:F2}/year in preventive maintenance could reduce repair costs by 30%.",
				InvestmentAmount = preventiveInvestment,
				ExpectedSavings = netSavings,
				ExpectedSavingsPerYear = netSavings,
				PaybackPeriodMonths = preventiveInvestment > 0 ? (preventiveInvestment / (netSavings / 12)) : 0,
				ROIPercentage = preventiveInvestment > 0 ? (netSavings / preventiveInvestment) * 100 : 0,
				Scenario = "Upgrade"
			});
		}

		// Lease vs Own ROI (simplified)
		if (vehicles.Any())
		{
			var totalAnnualCost = metrics.CostPerMember * metrics.TotalMembers * 12;
			var estimatedLeaseCost = totalAnnualCost * 1.2m; // Assume lease is 20% more
			
			roiCalculations.Add(new ROICalculation
			{
				Title = "Ownership vs Lease Comparison",
				Description = $"Current ownership costs (${totalAnnualCost:F2}/year) vs estimated lease costs (${estimatedLeaseCost:F2}/year).",
				InvestmentAmount = 0,
				ExpectedSavings = estimatedLeaseCost - totalAnnualCost,
				ExpectedSavingsPerYear = totalAnnualCost - estimatedLeaseCost, // Negative means owning is cheaper
				PaybackPeriodMonths = 0,
				ROIPercentage = 0,
				Scenario = "Lease vs Own"
			});
		}

		return roiCalculations.OrderByDescending(r => r.ExpectedSavings).ToList();
	}
}


