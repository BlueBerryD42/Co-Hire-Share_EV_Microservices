using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Vehicle.Api.DTOs;
using CoOwnershipVehicle.Vehicle.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GroupDetailsDto = CoOwnershipVehicle.Shared.Contracts.DTOs.GroupDetailsDto;

namespace CoOwnershipVehicle.Vehicle.Api.Services
{
    public class MemberUsageService
    {
        private readonly VehicleDbContext _context;
        private readonly IBookingServiceClient _bookingClient;
        private readonly IGroupServiceClient _groupClient;
        private readonly ILogger<MemberUsageService> _logger;

        private static readonly string[] ChartColors = new[]
        {
            "#FF6384", "#36A2EB", "#FFCE56", "#4BC0C0", "#9966FF",
            "#FF9F40", "#FF6384", "#C9CBCF", "#4BC0C0", "#FF6384"
        };

        public MemberUsageService(
            VehicleDbContext context,
            IBookingServiceClient bookingClient,
            IGroupServiceClient groupClient,
            ILogger<MemberUsageService> logger)
        {
            _context = context;
            _bookingClient = bookingClient;
            _groupClient = groupClient;
            _logger = logger;
        }

        public async Task<MemberUsageResponse> GetMemberUsageAsync(
            Guid vehicleId,
            MemberUsageRequest request,
            string accessToken)
        {
            // 1. Validate vehicle exists
            var vehicle = await _context.Vehicles
                .FirstOrDefaultAsync(v => v.Id == vehicleId);

            if (vehicle == null)
            {
                throw new InvalidOperationException($"Vehicle {vehicleId} not found");
            }

            if (!vehicle.GroupId.HasValue)
            {
                _logger.LogWarning("Vehicle {VehicleId} is not assigned to a group", vehicleId);
                throw new InvalidOperationException($"Vehicle {vehicleId} is not assigned to a group");
            }

            _logger.LogInformation("Vehicle {VehicleId} has GroupId: {GroupId}", vehicleId, vehicle.GroupId.Value);

            // 2. Set date range defaults
            var endDate = request.EndDate ?? DateTime.UtcNow;
            var startDate = request.StartDate ?? endDate.AddMonths(-3); // Default: last 3 months
            var totalDays = (endDate - startDate).Days + 1;

            // 3. Get group members with ownership info
            _logger.LogInformation("Fetching group details for GroupId: {GroupId}", vehicle.GroupId.Value);
            var groupDetails = await _groupClient.GetGroupDetailsAsync(vehicle.GroupId.Value, accessToken);

            if (groupDetails == null)
            {
                _logger.LogError("GetGroupDetailsAsync returned null for GroupId: {GroupId}", vehicle.GroupId.Value);
                throw new InvalidOperationException($"Unable to retrieve group members for vehicle {vehicleId}");
            }

            if (!groupDetails.Members.Any())
            {
                _logger.LogWarning("Group {GroupId} has no members", vehicle.GroupId.Value);
                throw new InvalidOperationException($"Unable to retrieve group members for vehicle {vehicleId}");
            }

            _logger.LogInformation("Successfully retrieved {Count} members for GroupId: {GroupId}",
                groupDetails.Members.Count, vehicle.GroupId.Value);

            // 4. Get booking statistics for all members
            var bookingStats = await _bookingClient.GetVehicleBookingStatisticsAsync(
                vehicleId, startDate, endDate, accessToken);

            if (bookingStats == null || !bookingStats.CompletedBookings.Any())
            {
                // No bookings - return empty response
                return CreateEmptyResponse(vehicleId, vehicle, startDate, endDate, totalDays, groupDetails);
            }

            // 5. Calculate per-member usage breakdowns
            var memberUsages = CalculateMemberUsages(groupDetails.Members, bookingStats.CompletedBookings);

            // 6. Calculate fairness analysis
            var fairnessAnalysis = CalculateFairnessAnalysis(memberUsages);

            // 7. Generate visualization data
            var visualization = GenerateVisualizationData(memberUsages);

            // 8. Generate member usage trends
            var trends = GenerateMemberTrends(bookingStats.CompletedBookings, startDate, endDate);

            return new MemberUsageResponse
            {
                VehicleId = vehicleId,
                VehicleName = vehicle.Model,
                PlateNumber = vehicle.PlateNumber,
                StartDate = startDate,
                EndDate = endDate,
                TotalDays = totalDays,
                MemberUsages = memberUsages,
                Fairness = fairnessAnalysis,
                Visualization = visualization,
                Trends = trends
            };
        }

        private MemberUsageResponse CreateEmptyResponse(
            Guid vehicleId,
            Domain.Entities.Vehicle vehicle,
            DateTime startDate,
            DateTime endDate,
            int totalDays,
            GroupDetailsDto groupDetails)
        {
            var emptyUsages = groupDetails.Members.Select(m => new MemberUsageBreakdown
            {
                MemberId = m.UserId,
                // Microservices: Use UserName from GroupMemberDetailsDto
                MemberName = !string.IsNullOrEmpty(m.UserName)
                    ? m.UserName
                    : $"User-{m.UserId.ToString().Substring(0, 8)}",
                MemberEmail = m.Email ?? string.Empty,
                OwnershipPercentage = m.SharePercentage,
                NumberOfTrips = 0,
                TotalDistanceDriven = 0,
                TotalTimeUsed = 0,
                PercentageOfTotalUsage = 0,
                AverageTripLength = 0,
                AverageTripDuration = 0,
                UsageToOwnershipRatio = 0,
                FairnessDelta = -m.SharePercentage,
                UsageStatus = "Underutilizing",
                PreferredDaysOfWeek = new List<string>(),
                PreferredHoursOfDay = new List<int>()
            }).ToList();

            return new MemberUsageResponse
            {
                VehicleId = vehicleId,
                VehicleName = vehicle.Model,
                PlateNumber = vehicle.PlateNumber,
                StartDate = startDate,
                EndDate = endDate,
                TotalDays = totalDays,
                MemberUsages = emptyUsages,
                Fairness = new FairnessAnalysis
                {
                    AverageFairnessScore = 0,
                    MemberScores = new List<MemberFairnessScore>(),
                    Overutilizers = new List<Guid>(),
                    Underutilizers = groupDetails.Members.Select(m => m.UserId).ToList(),
                    FairnessRecommendations = new List<string> { "No usage data available for the selected period" }
                },
                Visualization = new VisualizationData(),
                Trends = new List<MemberUsageTrend>()
            };
        }

        private List<MemberUsageBreakdown> CalculateMemberUsages(
            List<CoOwnershipVehicle.Shared.Contracts.DTOs.GroupMemberDetailsDto> members,
            List<DTOs.CompletedBookingDto> bookings)
        {
            var totalTrips = bookings.Count;
            var totalDistance = bookings.Sum(b => b.Distance ?? 0);
            var totalHours = bookings.Sum(b => b.UsageHours);

            var usages = new List<MemberUsageBreakdown>();

            foreach (var member in members)
            {
                var memberBookings = bookings.Where(b => b.UserId == member.UserId).ToList();
                var trips = memberBookings.Count;
                var distance = memberBookings.Sum(b => b.Distance ?? 0);
                var hours = memberBookings.Sum(b => b.UsageHours);

                var usagePercentage = totalTrips > 0 ? (trips / (decimal)totalTrips) * 100 : 0;
                var usageToOwnershipRatio = member.SharePercentage > 0 ? usagePercentage / member.SharePercentage : 0;
                var fairnessDelta = usagePercentage - member.SharePercentage;

                string usageStatus;
                if (Math.Abs(fairnessDelta) <= 10) usageStatus = "Fair";
                else if (fairnessDelta > 10) usageStatus = "Overutilizing";
                else usageStatus = "Underutilizing";

                // Analyze preferred patterns
                var preferredDays = memberBookings
                    .GroupBy(b => b.StartAt.DayOfWeek.ToString())
                    .OrderByDescending(g => g.Count())
                    .Take(2)
                    .Select(g => g.Key)
                    .ToList();

                var preferredHours = memberBookings
                    .GroupBy(b => b.StartAt.Hour)
                    .OrderByDescending(g => g.Count())
                    .Take(3)
                    .Select(g => g.Key)
                    .ToList();

                usages.Add(new MemberUsageBreakdown
                {
                    MemberId = member.UserId,
                    // Microservices: Use UserName from GroupMemberDetailsDto
                    MemberName = !string.IsNullOrEmpty(member.UserName)
                        ? member.UserName
                        : $"User-{member.UserId.ToString().Substring(0, 8)}",
                    MemberEmail = member.Email ?? string.Empty,
                    OwnershipPercentage = member.SharePercentage,
                    NumberOfTrips = trips,
                    TotalDistanceDriven = Math.Round(distance, 2),
                    TotalTimeUsed = Math.Round(hours, 2),
                    PercentageOfTotalUsage = Math.Round(usagePercentage, 2),
                    AverageTripLength = trips > 0 ? Math.Round(distance / trips, 2) : 0,
                    AverageTripDuration = trips > 0 ? Math.Round(hours / trips, 2) : 0,
                    UsageToOwnershipRatio = Math.Round(usageToOwnershipRatio, 2),
                    FairnessDelta = Math.Round(fairnessDelta, 2),
                    UsageStatus = usageStatus,
                    PreferredDaysOfWeek = preferredDays,
                    PreferredHoursOfDay = preferredHours
                });
            }

            // Sort by usage (highest to lowest)
            return usages.OrderByDescending(u => u.PercentageOfTotalUsage).ToList();
        }

        private FairnessAnalysis CalculateFairnessAnalysis(List<MemberUsageBreakdown> memberUsages)
        {
            var memberScores = memberUsages.Select(m =>
            {
                // Fairness score: 100 = perfect match, 0 = very unfair
                var score = Math.Max(0, 100 - Math.Abs(m.FairnessDelta) * 2);
                return new MemberFairnessScore
                {
                    MemberId = m.MemberId,
                    MemberName = m.MemberName,
                    Score = Math.Round(score, 2),
                    Status = m.UsageStatus
                };
            }).ToList();

            var avgScore = memberScores.Any() ? memberScores.Average(s => s.Score) : 0;

            var overutilizers = memberUsages
                .Where(m => m.UsageStatus == "Overutilizing")
                .Select(m => m.MemberId)
                .ToList();

            var underutilizers = memberUsages
                .Where(m => m.UsageStatus == "Underutilizing")
                .Select(m => m.MemberId)
                .ToList();

            var recommendations = GenerateFairnessRecommendations(memberUsages, overutilizers, underutilizers);

            return new FairnessAnalysis
            {
                AverageFairnessScore = Math.Round(avgScore, 2),
                MemberScores = memberScores,
                Overutilizers = overutilizers,
                Underutilizers = underutilizers,
                FairnessRecommendations = recommendations
            };
        }

        private List<string> GenerateFairnessRecommendations(
            List<MemberUsageBreakdown> usages,
            List<Guid> overutilizers,
            List<Guid> underutilizers)
        {
            var recommendations = new List<string>();

            if (!overutilizers.Any() && !underutilizers.Any())
            {
                recommendations.Add("✓ Usage is well-balanced across all members");
                return recommendations;
            }

            if (overutilizers.Any())
            {
                var overusers = usages.Where(u => overutilizers.Contains(u.MemberId)).ToList();
                foreach (var user in overusers)
                {
                    recommendations.Add($"⚠ {user.MemberName} is using {user.FairnessDelta:+0.0;-0.0}% more than their ownership share");
                }
            }

            if (underutilizers.Any())
            {
                var underusers = usages.Where(u => underutilizers.Contains(u.MemberId)).ToList();
                foreach (var user in underusers)
                {
                    recommendations.Add($"ℹ {user.MemberName} is using {Math.Abs(user.FairnessDelta):0.0}% less than their ownership share");
                }
            }

            recommendations.Add("Consider adjusting booking priorities or ownership percentages to improve fairness");

            return recommendations;
        }

        private VisualizationData GenerateVisualizationData(List<MemberUsageBreakdown> memberUsages)
        {
            var visualization = new VisualizationData();

            for (int i = 0; i < memberUsages.Count; i++)
            {
                var usage = memberUsages[i];
                var color = ChartColors[i % ChartColors.Length];

                visualization.UsagePieChart.Add(new ChartDataPoint
                {
                    Label = usage.MemberName,
                    Value = usage.PercentageOfTotalUsage,
                    Color = color
                });

                visualization.TripsByMember.Add(new ChartDataPoint
                {
                    Label = usage.MemberName,
                    Value = usage.NumberOfTrips,
                    Color = color
                });

                visualization.DistanceByMember.Add(new ChartDataPoint
                {
                    Label = usage.MemberName,
                    Value = usage.TotalDistanceDriven,
                    Color = color
                });

                visualization.TimeByMember.Add(new ChartDataPoint
                {
                    Label = usage.MemberName,
                    Value = usage.TotalTimeUsed,
                    Color = color
                });
            }

            return visualization;
        }

        private List<MemberUsageTrend> GenerateMemberTrends(
            List<DTOs.CompletedBookingDto> bookings,
            DateTime startDate,
            DateTime endDate)
        {
            var trends = new List<MemberUsageTrend>();
            var memberIds = bookings.Select(b => b.UserId).Distinct();

            foreach (var memberId in memberIds)
            {
                var memberBookings = bookings.Where(b => b.UserId == memberId).ToList();
                if (!memberBookings.Any()) continue;

                var firstName = memberBookings.First().UserFirstName;
                var lastName = memberBookings.First().UserLastName;

                var dataPoints = new List<MemberTrendDataPoint>();
                var currentDate = new DateTime(startDate.Year, startDate.Month, 1);
                var endMonth = new DateTime(endDate.Year, endDate.Month, 1);

                while (currentDate <= endMonth)
                {
                    var monthBookings = memberBookings
                        .Where(b => b.StartAt.Year == currentDate.Year && b.StartAt.Month == currentDate.Month)
                        .ToList();

                    dataPoints.Add(new MemberTrendDataPoint
                    {
                        Date = currentDate,
                        Period = currentDate.ToString("yyyy-MM"),
                        Trips = monthBookings.Count,
                        Distance = Math.Round(monthBookings.Sum(b => b.Distance ?? 0), 2),
                        Hours = Math.Round(monthBookings.Sum(b => b.UsageHours), 2)
                    });

                    currentDate = currentDate.AddMonths(1);
                }

                // Determine trend direction
                var trendDirection = "Stable";
                if (dataPoints.Count >= 3)
                {
                    var recent = dataPoints.TakeLast(3).ToList();
                    if (recent[0].Trips < recent[1].Trips && recent[1].Trips < recent[2].Trips)
                        trendDirection = "Increasing";
                    else if (recent[0].Trips > recent[1].Trips && recent[1].Trips > recent[2].Trips)
                        trendDirection = "Decreasing";
                }

                trends.Add(new MemberUsageTrend
                {
                    MemberId = memberId,
                    MemberName = $"{firstName} {lastName}",
                    DataPoints = dataPoints,
                    TrendDirection = trendDirection
                });
            }

            return trends;
        }
    }
}
