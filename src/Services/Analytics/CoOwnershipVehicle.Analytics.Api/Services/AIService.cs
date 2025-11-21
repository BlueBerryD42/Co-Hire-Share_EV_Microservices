using CoOwnershipVehicle.Analytics.Api.Data;
using CoOwnershipVehicle.Analytics.Api.Data.Entities;
using CoOwnershipVehicle.Analytics.Api.Models;
using CoOwnershipVehicle.Analytics.Api.Services.HttpClients;
using CoOwnershipVehicle.Analytics.Api.Services.Prompts;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Domain.Enums;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using Microsoft.EntityFrameworkCore;

namespace CoOwnershipVehicle.Analytics.Api.Services;

public class AIService : IAIService
{
	private readonly AnalyticsDbContext _context;
	private readonly IGroupServiceClient _groupServiceClient;
	private readonly IBookingServiceClient _bookingServiceClient;
	private readonly IPaymentServiceClient _paymentServiceClient;
	private readonly IVehicleServiceClient _vehicleServiceClient;
	private readonly IOpenAIServiceClient _openAIServiceClient;
	private readonly ILogger<AIService> _logger;

	public AIService(
		AnalyticsDbContext context,
		IGroupServiceClient groupServiceClient,
		IBookingServiceClient bookingServiceClient,
		IPaymentServiceClient paymentServiceClient,
		IVehicleServiceClient vehicleServiceClient,
		IOpenAIServiceClient openAIServiceClient,
		ILogger<AIService> logger)
	{
		_context = context;
		_groupServiceClient = groupServiceClient;
		_bookingServiceClient = bookingServiceClient;
		_paymentServiceClient = paymentServiceClient;
		_vehicleServiceClient = vehicleServiceClient;
		_openAIServiceClient = openAIServiceClient;
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

		// Try AI API first
		try
		{
			// Prepare data for AI prompt
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

			if (members.Any())
			{
				// Normalize usage if needed
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

				var memberData = members.Select(m => new MemberFairnessData
				{
					UserId = m.UserId,
					OwnershipPercentage = (decimal)(m.Ownership * 100.0),
					UsagePercentage = (decimal)(m.Usage * 100.0)
				}).ToList();

				// Get historical trends
				var historicalTrends = await _context.FairnessTrends
					.Where(t => t.GroupId == groupId)
					.OrderByDescending(t => t.PeriodEnd)
					.Take(6)
					.Select(t => new FairnessTrendPoint
					{
						PeriodStart = t.PeriodStart,
						PeriodEnd = t.PeriodEnd,
						GroupFairnessScore = t.GroupFairnessScore
					})
					.ToListAsync();

				// Build prompt and call AI
				var prompt = AIPromptTemplates.BuildFairnessAnalysisPrompt(
					groupId, periodStart, periodEnd, memberData, historicalTrends);

				var aiResponse = await _openAIServiceClient.AnalyzeFairnessAsync(prompt);
				
				if (aiResponse != null)
				{
					_logger.LogInformation("Successfully received AI fairness analysis for group {GroupId}", groupId);
					
					// Store trend for current period
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
								GroupFairnessScore = aiResponse.GroupFairnessScore
							});
							await _context.SaveChangesAsync();
						}
						else if (existing.GroupFairnessScore != aiResponse.GroupFairnessScore)
						{
							existing.GroupFairnessScore = aiResponse.GroupFairnessScore;
							await _context.SaveChangesAsync();
						}
					}
					catch (Exception ex)
					{
						_logger.LogWarning(ex, "Failed to upsert fairness trend for group {GroupId}", groupId);
					}

					return aiResponse;
				}
			}
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "AI API call failed for fairness analysis, falling back to hardcoded logic");
		}

		// Fallback to hardcoded logic
		_logger.LogInformation("Using fallback hardcoded logic for fairness analysis");
		return await CalculateFairnessFallbackAsync(groupId, periodStart, periodEnd, userAnalytics);
	}

	private async Task<FairnessAnalysisResponse?> CalculateFairnessFallbackAsync(
		Guid groupId, DateTime periodStart, DateTime periodEnd, List<UserAnalytics> userAnalytics)
	{

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

		if (!members.Any())
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
			if (!monthData.Any())
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
		
		_logger.LogInformation("Checking analytics data for GroupId: {GroupId}, UserId: {UserId}. hasUser: {HasUser}, hasGroup: {HasGroup}", 
			request.GroupId, request.UserId, hasUser, hasGroup);
		
		if (!hasUser && !hasGroup)
		{
			_logger.LogWarning("No analytics data found for GroupId: {GroupId}, UserId: {UserId}. Attempting to create analytics data...", 
				request.GroupId, request.UserId);
			
			// Try to create analytics data automatically if not exists
			// This helps when event consumer hasn't run yet or failed
			try
			{
				// Note: We can't call ProcessAnalyticsAsync directly as it's in AnalyticsService
				// Instead, we'll proceed with fallback logic which doesn't require analytics data
				_logger.LogInformation("Proceeding with fallback logic that doesn't require analytics data for GroupId: {GroupId}", 
					request.GroupId);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error attempting to create analytics data for GroupId: {GroupId}", request.GroupId);
			}
			
			// Don't return null - proceed with fallback logic instead
			// This allows AI suggestions to work even without analytics data
			_logger.LogInformation("Proceeding with booking suggestions using fallback logic (no analytics data required)");
		}
		else
		{
			_logger.LogInformation("Analytics data found. Proceeding with booking suggestions for GroupId: {GroupId}, UserId: {UserId}", 
				request.GroupId, request.UserId);
		}

		// Determine a target date window - preserve time component if provided
		var baseDateLocal = request.PreferredDate ?? DateTime.UtcNow;
		var preferredHour = baseDateLocal.Hour;
		var preferredMinute = baseDateLocal.Minute;
		var baseDateOnly = baseDateLocal.Date;
		var duration = TimeSpan.FromMinutes(Math.Max(30, request.DurationMinutes));

		// Try AI API first
		try
		{
			// Compute fairness for prioritization
			var fairness = await CalculateFairnessAsync(request.GroupId, baseDateLocal.AddMonths(-3), baseDateLocal.AddDays(1));
			decimal userFairness = 100m;
			decimal groupFairness = 100m;
			if (fairness != null)
			{
				groupFairness = fairness.GroupFairnessScore;
				var member = fairness.Members.FirstOrDefault(m => m.UserId == request.UserId);
				if (member != null)
				{
					userFairness = member.FairnessScore;
				}
			}

			// Get existing bookings to check for conflicts
			var periodStart = baseDateOnly.ToUniversalTime();
			var periodEnd = baseDateOnly.AddDays(7).ToUniversalTime();
			var existingBookings = await _bookingServiceClient.GetBookingsAsync(periodStart, periodEnd, request.GroupId);
			
			// Get historical booking patterns (simplified - would need actual booking data)
			var bookingPatterns = new List<HistoricalBookingPattern>
			{
				new() { DayOfWeek = "Monday", Hour = 8, Frequency = 5 },
				new() { DayOfWeek = "Friday", Hour = 18, Frequency = 4 }
			};

			// Build prompt and call AI
			var prompt = AIPromptTemplates.BuildBookingSuggestionPrompt(
				request.UserId, request.GroupId, request.PreferredDate, request.DurationMinutes,
				userFairness, groupFairness, bookingPatterns, existingBookings);

			var aiResponse = await _openAIServiceClient.SuggestBookingTimesAsync(prompt);
			
			if (aiResponse != null)
			{
				_logger.LogInformation("Successfully received AI booking suggestions for user {UserId}", request.UserId);
				return aiResponse;
			}
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "AI API call failed for booking suggestions, falling back to hardcoded logic");
		}

		// Fallback to hardcoded logic
		_logger.LogInformation("Using fallback hardcoded logic for booking suggestions");
		return await SuggestBookingTimesFallbackAsync(request, baseDateOnly, duration, preferredHour, preferredMinute);
	}

	private async Task<SuggestBookingResponse?> SuggestBookingTimesFallbackAsync(
		SuggestBookingRequest request, DateTime baseDateOnly, TimeSpan duration, int? preferredHour = null, int? preferredMinute = null)
	{

		// Compute fairness for prioritization
		var fairness = await CalculateFairnessAsync(request.GroupId, baseDateOnly.AddMonths(-3), baseDateOnly.AddDays(1));
		decimal userFairness = 100m;
		if (fairness != null)
		{
			var member = fairness.Members.FirstOrDefault(m => m.UserId == request.UserId);
			if (member != null)
			{
				userFairness = member.FairnessScore;
			}
		}

		// Fetch existing bookings for the group to check for actual conflicts
		var periodStart = baseDateOnly.ToUniversalTime();
		var periodEnd = baseDateOnly.AddDays(7).ToUniversalTime();
		var existingBookings = await _bookingServiceClient.GetBookingsAsync(periodStart, periodEnd, request.GroupId);
		
		// Helper function to check if a time slot conflicts with existing bookings
		bool HasConflict(DateTime start, DateTime end)
		{
			return existingBookings.Any(booking =>
			{
				var bookingStart = booking.StartAt.ToUniversalTime();
				var bookingEnd = booking.EndAt.ToUniversalTime();
				// Check for overlap: start < bookingEnd && end > bookingStart
				return start < bookingEnd && end > bookingStart;
			});
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

		// Generate candidate slots - prioritize same day with different hours
		// Strategy: First try preferred day with various hours, then move to next day only if needed
		var candidates = new List<(DateTime start, DateTime end, List<string> reasons, double score, int dayOffset)>();
		
		// Generate all possible hours for candidate slots
		var allCandidateHours = new List<int>();
		if (request.PreferredDate.HasValue && preferredHour.HasValue)
		{
			// Start with preferred hour, then expand to nearby hours
			allCandidateHours.Add(preferredHour.Value);
			// Add hours around preferred time (±3 hours for variety)
			for (int offset = 1; offset <= 3; offset++)
			{
				if (preferredHour.Value + offset < 24) allCandidateHours.Add(preferredHour.Value + offset);
				if (preferredHour.Value - offset >= 0) allCandidateHours.Add(preferredHour.Value - offset);
			}
			// Add standard hours for more options
			var standardHours = new[] { 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 };
			foreach (var h in standardHours)
			{
				if (!allCandidateHours.Contains(h) && h >= 6 && h <= 22)
				{
					allCandidateHours.Add(h);
				}
			}
		}
		else
		{
			// No preferred time - use standard hours throughout the day
			allCandidateHours = new List<int> { 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 };
		}
		allCandidateHours.Sort();

		// Priority: Same day (dayOffset = 0) first, then next days only if needed
		for (int dayOffset = 0; dayOffset < 7; dayOffset++)
		{
			var day = baseDateOnly.AddDays(dayOffset);
			
			foreach (var h in allCandidateHours)
			{
				// Use preferred minute if provided and it's the preferred hour, otherwise use 0 or 30
				var minute = 0;
				if (request.PreferredDate.HasValue && preferredHour.HasValue && h == preferredHour.Value && preferredMinute.HasValue)
				{
					minute = preferredMinute.Value;
				}
				else if (h % 2 == 0) // Even hours use :00, odd hours use :30 for variety
				{
					minute = 0;
				}
				else
				{
					minute = 30;
				}
				
				var start = new DateTime(day.Year, day.Month, day.Day, h, minute, 0, DateTimeKind.Utc);
				var end = start.Add(duration);
				
				// Skip if end time exceeds 23:59
				if (end.Hour >= 23 && end.Minute > 0) continue;
				
				// Check for actual conflicts with existing bookings
				if (HasConflict(start, end))
				{
					continue; // Skip conflicting slots
				}
				
				var reasons = new List<string>();
				double score = 0.5; // base

				// Strong preference for same day (dayOffset = 0)
				if (dayOffset == 0)
				{
					reasons.Add("Same day as your preference");
					score += 0.6; // Strong boost for same day
				}
				else
				{
					// Penalize different days - only use if same day has no available slots
					score -= dayOffset * 0.2; // Reduce score for later days
					reasons.Add($"Alternative day ({dayOffset} day{(dayOffset > 1 ? "s" : "")} later)");
				}

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

				// Preference alignment: prioritize preferred time if provided
				if (request.PreferredDate.HasValue && preferredHour.HasValue)
				{
					var prefHour = preferredHour.Value;
					var prefMinute = preferredMinute ?? 0;
					var hourDelta = Math.Abs(prefHour - h);
					var minuteDelta = h == prefHour ? Math.Abs(prefMinute - minute) : 60;
					
					// Strong preference for exact or very close time (but only if same day)
					if (dayOffset == 0)
					{
						if (hourDelta == 0 && minuteDelta <= 30)
						{
							reasons.Add("Matches your preferred time");
							score += 0.5; // Strong boost for exact match
						}
						else if (hourDelta == 0)
						{
							reasons.Add("Same hour as your preference");
							score += 0.4;
						}
						else if (hourDelta == 1)
						{
							reasons.Add("Close to your preferred time");
							score += 0.3;
						}
						else if (hourDelta == 2)
						{
							reasons.Add("Near your preferred time");
							score += 0.15;
						}
					}
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

				candidates.Add((start, end, reasons, Math.Clamp(score, 0.0, 1.0), dayOffset));
			}
		}

		// Rank: prioritize same day (dayOffset = 0), then by score, then by time
		var top = candidates
			.OrderBy(c => c.dayOffset) // Same day first
			.ThenByDescending(c => c.score) // Then by score
			.ThenBy(c => c.start) // Then by time
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
			// Get group from Group service via HTTP
			var group = await _groupServiceClient.GetGroupDetailsAsync(groupId);
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

		if (historySpan < 30)
		{
			return new UsagePredictionResponse
			{
				GroupId = groupId,
				GeneratedAt = DateTime.UtcNow,
				HistoryStart = historyStart,
				HistoryEnd = historyEnd,
				InsufficientHistory = true
			};
		}

		// Try AI API first
		try
		{
			// Aggregate daily usage
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

			// Calculate trend
			var last30Start = historyEnd.Date.AddDays(-29);
			var prev30Start = last30Start.AddDays(-30);
			double sumLast = dayUsage.Where(kv => kv.Key >= last30Start && kv.Key <= historyEnd.Date).Sum(kv => kv.Value);
			double sumPrev = dayUsage.Where(kv => kv.Key >= prev30Start && kv.Key < last30Start).Sum(kv => kv.Value);
			double trendPct = (sumPrev <= 0) ? 0 : ((sumLast - sumPrev) / sumPrev) * 100.0;

			// Day-of-week averages
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

			// Member patterns
			var memberPatterns = userHistory
				.GroupBy(x => x.UserId)
				.Select(g => new MemberUsagePattern
				{
					UserId = g.Key,
					UsageShare = (decimal)g.Average(x => (double)x.UsageShare),
					PreferredDayOfWeek = "Monday", // Simplified
					PreferredHour = 8 // Simplified
				})
				.Take(10)
				.ToList();

			// Build prompt and call AI
			var prompt = AIPromptTemplates.BuildUsagePredictionPrompt(
				groupId, historyStart, historyEnd, dayUsage, dowAvg, trendPct, memberPatterns);

			var aiResponse = await _openAIServiceClient.PredictUsageAsync(prompt);
			
			if (aiResponse != null)
			{
				_logger.LogInformation("Successfully received AI usage predictions for group {GroupId}", groupId);
				return aiResponse;
			}
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "AI API call failed for usage predictions, falling back to hardcoded logic");
		}

		// Fallback to hardcoded logic
		_logger.LogInformation("Using fallback hardcoded logic for usage predictions");
		return await GetUsagePredictionsFallbackAsync(groupId, userHistory, snapHistory, historyStart, historyEnd, historySpan);
	}

	private async Task<UsagePredictionResponse?> GetUsagePredictionsFallbackAsync(
		Guid groupId, List<UserAnalytics> userHistory, List<AnalyticsSnapshot> snapHistory,
		DateTime historyStart, DateTime historyEnd, double historySpan)
	{
		var response = new UsagePredictionResponse
		{
			GroupId = groupId,
			GeneratedAt = DateTime.UtcNow,
			HistoryStart = historyStart,
			HistoryEnd = historyEnd,
			InsufficientHistory = false
		};

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
		if (monthAvg.Count() >= 3)
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
		for (int i = 1; i < orderedDays.Count(); i++)
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
		if (!values.Any()) return 0.0;
		var avg = values.Average();
		var variance = values.Average(v => Math.Pow(v - avg, 2));
		return Math.Sqrt(variance);
	}

	private static double GiniCoefficient(List<double> values01)
	{
		// values01 expected between 0 and 1
		var values = values01.Where(v => v >= 0).ToList();
		if (!values.Any()) return 0.0;
		var mean = values.Average();
		if (mean == 0) return 0.0;
		values.Sort();
		double cumulative = 0;
		for (int i = 0; i < values.Count(); i++)
		{
			cumulative += (2 * (i + 1) - values.Count() - 1) * values[i];
		}
		var gini = cumulative / (values.Count() * values.Sum());
		return Math.Abs(gini);
	}

	public async Task<CostOptimizationResponse?> GetCostOptimizationAsync(Guid groupId)
	{
		var periodEnd = DateTime.UtcNow;
		var periodStart = periodEnd.AddMonths(-12); // Last 12 months

		// Check if group exists
		var group = await _groupServiceClient.GetGroupDetailsAsync(groupId);
		if (group == null)
		{
			return null;
		}

		// Get all expenses for the group from Payment service via HTTP
		var expenses = await _paymentServiceClient.GetExpensesAsync(groupId, periodStart, periodEnd);

		// Get bookings for distance calculations from Booking service via HTTP
		var allBookings = await _bookingServiceClient.GetBookingsAsync(periodStart, periodEnd, groupId);
		var bookings = allBookings.Where(b => b.Status == BookingStatus.Completed).ToList();

		if (!expenses.Any() || !bookings.Any())
		{
			return new CostOptimizationResponse
			{
				GroupId = groupId,
				GroupName = group.Name ?? "Unknown",
				PeriodStart = periodStart,
				PeriodEnd = periodEnd,
				GeneratedAt = DateTime.UtcNow,
				InsufficientData = true
			};
		}

		// Try AI API first
		try
		{
			// Calculate metrics for prompt
			var totalDistance = await CalculateTotalDistanceAsync(bookings);
			var totalTrips = bookings.Count();
			var totalMembers = group.Members?.Count() ?? 0;
			var totalHours = bookings.Sum(b => (int)(b.EndAt - b.StartAt).TotalHours);
			var totalExpenses = expenses.Sum(e => e.Amount);

			var summary = new CostAnalysisSummary
			{
				TotalExpenses = totalExpenses,
				AverageMonthlyExpenses = expenses.GroupBy(e => new { e.DateIncurred.Year, e.DateIncurred.Month })
					.Select(g => g.Sum(e => e.Amount))
					.DefaultIfEmpty(0)
					.Average(),
				TotalExpenseCount = expenses.Count(),
				ExpensesByType = expenses.GroupBy(e => e.ExpenseType)
					.ToDictionary(g => g.Key.ToString(), g => g.Sum(e => e.Amount)),
				ExpensesByMonth = expenses.GroupBy(e => new { e.DateIncurred.Year, e.DateIncurred.Month })
					.ToDictionary(g => $"{g.Key.Year}-{g.Key.Month:D2}", g => g.Sum(e => e.Amount))
			};

			var efficiencyMetrics = new CostEfficiencyMetrics
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

			var highCostAreas = AnalyzeHighCostAreas(expenses, totalExpenses);

			// Build prompt and call AI
			var prompt = AIPromptTemplates.BuildCostOptimizationPrompt(
				groupId, group.Name ?? "Unknown", periodStart, periodEnd,
				summary, highCostAreas, efficiencyMetrics,
				summary.ExpensesByType, summary.ExpensesByMonth);

			var aiResponse = await _openAIServiceClient.OptimizeCostsAsync(prompt);
			
			if (aiResponse != null)
			{
				_logger.LogInformation("Successfully received AI cost optimization for group {GroupId}", groupId);
				return aiResponse;
			}
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "AI API call failed for cost optimization, falling back to hardcoded logic");
		}

		// Fallback to hardcoded logic
		_logger.LogInformation("Using fallback hardcoded logic for cost optimization");
		return await GetCostOptimizationFallbackAsync(groupId, group, expenses, bookings, periodStart, periodEnd);
	}

	private async Task<CostOptimizationResponse?> GetCostOptimizationFallbackAsync(
		Guid groupId, GroupDetailsDto group, List<ExpenseDto> expenses, List<BookingDto> bookings,
		DateTime periodStart, DateTime periodEnd)
	{

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
		var totalDistance = await CalculateTotalDistanceAsync(bookings);
		var totalTrips = bookings.Count();
		var totalMembers = group.Members?.Count() ?? 0;
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
			TotalExpenseCount = expenses.Count(),
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
		// Note: Vehicles would need to come from Vehicle service if needed
		// Note: Vehicle data would need to be fetched from Vehicle service if needed for benchmarks
		response.Benchmarks = CalculateBenchmarks(response.EfficiencyMetrics, new List<VehicleDto>());

		// 5. Generate Recommendations
		response.Recommendations = GenerateRecommendations(expenses, response.HighCostAreas, response.EfficiencyMetrics, response.Benchmarks, totalExpenses);

		// 6. Cost Predictions
		response.Predictions = GeneratePredictions(expenses, expensesByMonth, totalDistance, totalTrips);

		// 7. Spending Alerts
		response.Alerts = GenerateAlerts(expenses, expensesByMonth, totalExpenses);

		// 8. ROI Calculations
		// Note: Vehicles would need to come from Vehicle service if needed
		// Note: Vehicle data would need to be fetched from Vehicle service if needed for ROI calculations
		response.ROICalculations = CalculateROI(expenses, response.EfficiencyMetrics, new List<VehicleDto>());

		return response;
	}

	private async Task<decimal> CalculateTotalDistanceAsync(List<BookingDto> bookings)
	{
		decimal totalDistance = 0;
		
		foreach (var booking in bookings)
		{
			var checkIns = await _bookingServiceClient.GetBookingCheckInsAsync(booking.Id);
			var checkOut = checkIns.FirstOrDefault(c => c.Type == CheckInType.CheckOut);
			var checkIn = checkIns.FirstOrDefault(c => c.Type == CheckInType.CheckIn);
			
			if (checkOut != null && checkIn != null)
			{
				totalDistance += Math.Max(0, checkOut.Odometer - checkIn.Odometer);
			}
		}
		
		return totalDistance;
	}

	private List<HighCostArea> AnalyzeHighCostAreas(List<ExpenseDto> expenses, decimal totalExpenses)
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
		var providerGroups = new Dictionary<string, List<ExpenseDto>>();
		
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
				providerGroups[providerName] = new List<ExpenseDto>();
			}
			providerGroups[providerName].Add(exp);
		}

		// Add expensive providers
		foreach (var provider in providerGroups.OrderByDescending(p => p.Value.Sum(e => e.Amount)).Take(5))
		{
			var providerTotal = provider.Value.Sum(e => e.Amount);
			if (provider.Value.Any())
			{
				highCostAreas.Add(new HighCostArea
				{
					Category = "Maintenance Provider",
					TotalAmount = providerTotal,
					Count = provider.Value.Count(),
					AverageAmount = providerTotal / provider.Value.Count(),
					ProviderName = provider.Key,
					PercentageOfTotal = totalExpenses > 0 ? (providerTotal / totalExpenses) * 100 : 0,
					Description = $"Multiple expenses with {provider.Key}"
				});
			}
		}

		// Identify frequent repairs (may indicate bigger issues)
		var repairExpenses = expenses.Where(e => e.ExpenseType == ExpenseType.Repair).ToList();
		if (repairExpenses.Count() >= 3)
		{
			var repairTotal = repairExpenses.Sum(e => e.Amount);
			highCostAreas.Add(new HighCostArea
			{
				Category = "Frequent Repairs",
				TotalAmount = repairTotal,
				Count = repairExpenses.Count(),
				AverageAmount = repairExpenses.Average(e => e.Amount),
				PercentageOfTotal = totalExpenses > 0 ? (repairTotal / totalExpenses) * 100 : 0,
				Description = $"High frequency of repairs ({repairExpenses.Count()} repairs) may indicate underlying issues"
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

	private BenchmarkComparisons CalculateBenchmarks(CostEfficiencyMetrics metrics, List<VehicleDto> vehicles)
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
		List<ExpenseDto> expenses,
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
			var avgCleaningCost = cleaningExpenses.Average(e => e.Amount);
			
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
		List<ExpenseDto> expenses,
		Dictionary<string, decimal> expensesByMonth,
		decimal totalDistance,
		int totalTrips)
	{
		var prediction = new CostPrediction
		{
			ConfidenceScore = expenses.Count() >= 6 ? 0.75m : expenses.Count() >= 3 ? 0.5m : 0.3m
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
		List<ExpenseDto> expenses,
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
		if (expensesByMonth.Count() >= 3)
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
		if (!values.Any()) return 0.0;
		var avg = values.Average();
		var variance = values.Average(v => Math.Pow(v - avg, 2));
		return Math.Sqrt(variance);
	}

	private List<ROICalculation> CalculateROI(
		List<ExpenseDto> expenses,
		CostEfficiencyMetrics metrics,
		List<VehicleDto> vehicles)
	{
		var roiCalculations = new List<ROICalculation>();

		// Maintenance vs Replacement ROI
		var repairExpenses = expenses.Where(e => e.ExpenseType == ExpenseType.Repair).ToList();
		var maintenanceExpenses = expenses.Where(e => e.ExpenseType == ExpenseType.Maintenance).ToList();
		var annualRepairCost = repairExpenses.Any() ? repairExpenses.Sum(e => e.Amount) * 12 / Math.Max(1m, (decimal)((DateTime.UtcNow - expenses.Min(e => e.DateIncurred)).Days / 365.0)) : 0;
		var annualMaintenanceCost = maintenanceExpenses.Any() ? maintenanceExpenses.Sum(e => e.Amount) * 12 / Math.Max(1m, (decimal)((DateTime.UtcNow - expenses.Min(e => e.DateIncurred)).Days / 365.0)): 0;

		if (annualRepairCost > 2000 && vehicles.Any())
		{
			var vehicle = vehicles.First();
			var vehicleAge = vehicle.Year > 0 ? DateTime.UtcNow.Year - vehicle.Year : 5;
			var estimatedReplacementCost = 30000m; // Average EV cost
			
			// Calculate if replacement is more cost-effective
			var remainingRepairYears = Math.Max(1, 10 - vehicleAge);
			var totalRepairCostProjected = annualRepairCost * remainingRepairYears;
			
			if (totalRepairCostProjected > estimatedReplacementCost * 0.3m)
			{
				var savings = totalRepairCostProjected - estimatedReplacementCost;
				var paybackMonths = annualRepairCost > 0 ? (estimatedReplacementCost / (annualRepairCost / 12)) : 0;
				
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
		if (repairExpenses.Any())
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
				PaybackPeriodMonths = netSavings > 0 ? (preventiveInvestment / (netSavings / 12)) : 0,
				ROIPercentage = preventiveInvestment > 0 ? (netSavings / preventiveInvestment) * 100 : 0,
				Scenario = "Upgrade"
			});
		}

		// Lease vs Own ROI (simplified)
		if (vehicles.Any())
		{
			var totalAnnualCost = metrics.CostPerMember > 0 ? metrics.CostPerMember * metrics.TotalMembers * 12 : (metrics.CostPerHour > 0 ? metrics.CostPerHour * 24 * 365 : 0);
			var estimatedLeaseCost = totalAnnualCost > 0 ? totalAnnualCost * 1.2m : 10000; // Assume lease is 20% more or 10k
			
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

	public async Task<PredictiveMaintenanceResponse?> GetPredictiveMaintenanceAsync(Guid vehicleId)
	{
		// Get vehicle information
		var vehicle = await _vehicleServiceClient.GetVehicleAsync(vehicleId);
		if (vehicle == null)
		{
			return null;
		}

		// Try AI API first
		try
		{
		// Get vehicle age
		var vehicleAge = DateTime.UtcNow.Year - vehicle.Year;
		var odometer = vehicle.Odometer;

		// Build prompt for AI
		var prompt = AIPromptTemplates.BuildPredictiveMaintenancePrompt(
			vehicleId, vehicle.Model ?? "Unknown", vehicle.Model ?? "Unknown", 
			vehicle.Year, vehicleAge, odometer);

			var aiResponse = await _openAIServiceClient.GetPredictiveMaintenanceAsync(prompt);
			
			if (aiResponse != null)
			{
				_logger.LogInformation("Successfully received AI predictive maintenance for vehicle {VehicleId}", vehicleId);
				return aiResponse;
			}
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "AI API call failed for predictive maintenance, falling back to hardcoded logic");
		}

		// Fallback to hardcoded logic
		_logger.LogInformation("Using fallback hardcoded logic for predictive maintenance");
		return await GetPredictiveMaintenanceFallbackAsync(vehicleId, vehicle);
	}

	private async Task<PredictiveMaintenanceResponse?> GetPredictiveMaintenanceFallbackAsync(
		Guid vehicleId, VehicleDto vehicle)
	{
		var vehicleAge = DateTime.UtcNow.Year - vehicle.Year;
		var odometer = vehicle.Odometer;
		var mileagePerYear = vehicleAge > 0 ? odometer / vehicleAge : odometer;

		// Calculate health score (0-100)
		var healthScore = 100m;
		
		// Reduce score based on age
		if (vehicleAge > 0)
		{
			healthScore -= Math.Min(30, vehicleAge * 2); // -2 points per year, max -30
		}

		// Reduce score based on mileage
		if (mileagePerYear > 0)
		{
			if (mileagePerYear > 20000) // High mileage
			{
				healthScore -= Math.Min(20, (mileagePerYear - 20000) / 1000); // -1 per 1000km over 20k
			}
		}

		// Ensure score is between 0 and 100
		healthScore = Math.Max(0, Math.Min(100, healthScore));

		var response = new PredictiveMaintenanceResponse
		{
			VehicleId = vehicleId,
			VehicleName = $"{vehicle.Year} {vehicle.Model}",
			HealthScore = Math.Round(healthScore, 0),
			GeneratedAt = DateTime.UtcNow
		};

		// Generate predicted issues based on vehicle age and mileage
		var predictedIssues = new List<PredictedIssue>();

		// Battery issues (common for EVs)
		if (vehicleAge >= 3 || odometer > 50000)
		{
			var batteryLikelihood = Math.Min(0.8m, 0.3m + (vehicleAge * 0.1m) + (odometer > 50000 ? 0.2m : 0m));
			predictedIssues.Add(new PredictedIssue
			{
				Type = "Battery",
				Name = "Battery Capacity Degradation",
				Severity = batteryLikelihood > 0.6m ? "High" : "Medium",
				Likelihood = batteryLikelihood,
				Timeline = vehicleAge >= 5 ? "1-3 months" : "6-12 months",
				CostRange = new CostRange { Min = 5000, Max = 15000 },
				Recommendation = "Schedule battery health check. Consider battery replacement if capacity drops below 70%."
			});
		}

		// Tire wear
		if (odometer > 40000 || mileagePerYear > 15000)
		{
			var tireLikelihood = Math.Min(0.7m, 0.2m + ((odometer - 40000) / 100000m));
			predictedIssues.Add(new PredictedIssue
			{
				Type = "Tires",
				Name = "Tire Wear",
				Severity = tireLikelihood > 0.5m ? "Medium" : "Low",
				Likelihood = tireLikelihood,
				Timeline = odometer > 60000 ? "2-4 months" : "6-12 months",
				CostRange = new CostRange { Min = 400, Max = 1200 },
				Recommendation = "Inspect tire tread depth. Replace if below 3mm. Consider rotating tires."
			});
		}

		// Brake system
		if (odometer > 50000)
		{
			var brakeLikelihood = Math.Min(0.6m, 0.2m + ((odometer - 50000) / 100000m));
			predictedIssues.Add(new PredictedIssue
			{
				Type = "Brakes",
				Name = "Brake Pad Wear",
				Severity = brakeLikelihood > 0.4m ? "Medium" : "Low",
				Likelihood = brakeLikelihood,
				Timeline = odometer > 70000 ? "3-6 months" : "9-15 months",
				CostRange = new CostRange { Min = 300, Max = 800 },
				Recommendation = "Check brake pad thickness. EV regenerative braking reduces wear but inspection is recommended."
			});
		}

		// Charging port issues (EV specific)
		if (vehicleAge >= 2)
		{
			var chargingLikelihood = Math.Min(0.5m, 0.1m + (vehicleAge * 0.05m));
			predictedIssues.Add(new PredictedIssue
			{
				Type = "Charging",
				Name = "Charging Port Wear",
				Severity = "Low",
				Likelihood = chargingLikelihood,
				Timeline = "12-24 months",
				CostRange = new CostRange { Min = 200, Max = 600 },
				Recommendation = "Inspect charging port for wear or damage. Clean regularly to prevent issues."
			});
		}

		// Suspension (for older/high mileage vehicles)
		if (vehicleAge >= 5 || odometer > 80000)
		{
			var suspensionLikelihood = Math.Min(0.5m, 0.1m + ((vehicleAge - 5) * 0.05m) + (odometer > 80000 ? 0.2m : 0m));
			predictedIssues.Add(new PredictedIssue
			{
				Type = "Suspension",
				Name = "Suspension Component Wear",
				Severity = suspensionLikelihood > 0.4m ? "Medium" : "Low",
				Likelihood = suspensionLikelihood,
				Timeline = "6-18 months",
				CostRange = new CostRange { Min = 500, Max = 2000 },
				Recommendation = "Inspect suspension components for wear. Check for unusual noises or handling issues."
			});
		}

		response.PredictedIssues = predictedIssues;

		// Generate suggested maintenance bundles
		var bundles = new List<MaintenanceBundle>();

		// Bundle 1: Basic maintenance
		if (predictedIssues.Any(i => i.Type == "Tires" || i.Type == "Brakes"))
		{
			bundles.Add(new MaintenanceBundle
			{
				Title = "Tire & Brake Service Bundle",
				Services = new List<string> { "Tire Rotation", "Brake Inspection", "Tire Pressure Check" },
				PotentialSavings = 150
			});
		}

		// Bundle 2: Comprehensive check
		if (predictedIssues.Count >= 2)
		{
			bundles.Add(new MaintenanceBundle
			{
				Title = "Comprehensive Vehicle Inspection",
				Services = new List<string> { "Full Vehicle Inspection", "Battery Health Check", "Charging System Check", "Tire Inspection" },
				PotentialSavings = 200
			});
		}

		// Bundle 3: Preventive maintenance
		if (vehicleAge >= 3)
		{
			bundles.Add(new MaintenanceBundle
			{
				Title = "Preventive Maintenance Package",
				Services = new List<string> { "Battery Health Check", "Tire Rotation", "Brake Inspection", "Charging Port Inspection" },
				PotentialSavings = 180
			});
		}

		response.SuggestedBundles = bundles;

		return response;
	}
}
