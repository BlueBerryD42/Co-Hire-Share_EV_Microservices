using Microsoft.EntityFrameworkCore;
using CoOwnershipVehicle.Analytics.Api.Data;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Data;

namespace CoOwnershipVehicle.Analytics.Api.Services;

public class AnalyticsService : IAnalyticsService
{
    private readonly AnalyticsDbContext _context;
    private readonly ApplicationDbContext _mainContext;
    private readonly ILogger<AnalyticsService> _logger;

    public AnalyticsService(AnalyticsDbContext context, ApplicationDbContext mainContext, ILogger<AnalyticsService> logger)
    {
        _context = context;
        _mainContext = mainContext;
        _logger = logger;
    }

    public async Task<AnalyticsDashboardDto> GetDashboardAsync(AnalyticsRequestDto request)
    {
        var periodStart = request.StartDate ?? DateTime.UtcNow.AddMonths(-1);
        var periodEnd = request.EndDate ?? DateTime.UtcNow;

        var dashboard = new AnalyticsDashboardDto
        {
            GeneratedAt = DateTime.UtcNow,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            Period = request.Period
        };

        // TODO: In a proper microservices architecture, these statistics should be calculated
        // from AnalyticsSnapshot data or received via events from other services
        // For now, using placeholder values
        
        // Overall Statistics - should come from AnalyticsSnapshot data
        dashboard.TotalGroups = 0; // await _context.OwnershipGroups.CountAsync();
        dashboard.TotalVehicles = 0; // await _context.Vehicles.CountAsync();
        dashboard.TotalUsers = 0; // await _context.Users.CountAsync();
        dashboard.TotalBookings = 0; // await _context.Bookings.CountAsync();

        // Financial Overview - should come from AnalyticsSnapshot data
        dashboard.TotalRevenue = 0; // await _context.Payments.SumAsync(p => p.Amount);
        dashboard.TotalExpenses = 0; // await _context.Expenses.SumAsync(e => e.Amount);
        dashboard.NetProfit = dashboard.TotalRevenue - dashboard.TotalExpenses;

        // Efficiency Metrics - should come from AnalyticsSnapshot data
        dashboard.AverageUtilizationRate = 0; // calculated from snapshots

        // Top Performers - should come from AnalyticsSnapshot data
        dashboard.TopVehicles = new List<VehicleAnalyticsDto>(); // await GetTopVehiclesAsync(request, 5);
        dashboard.TopUsers = new List<UserAnalyticsDto>(); // await GetTopUsersAsync(request, 5);
        dashboard.TopGroups = new List<GroupAnalyticsDto>(); // await GetTopGroupsAsync(request, 5);

        // Trends
        dashboard.WeeklyTrends = await GetWeeklyTrendsAsync(request);
        dashboard.MonthlyTrends = await GetMonthlyTrendsAsync(request);

        return dashboard;
    }

    public async Task<List<AnalyticsSnapshotDto>> GetSnapshotsAsync(AnalyticsRequestDto request)
    {
        var query = _context.AnalyticsSnapshots.AsQueryable();

        if (request.GroupId.HasValue)
            query = query.Where(s => s.GroupId == request.GroupId);

        if (request.VehicleId.HasValue)
            query = query.Where(s => s.VehicleId == request.VehicleId);

        if (request.StartDate.HasValue)
            query = query.Where(s => s.SnapshotDate >= request.StartDate);

        if (request.EndDate.HasValue)
            query = query.Where(s => s.SnapshotDate <= request.EndDate);

        if (!string.IsNullOrEmpty(request.Period))
            query = query.Where(s => s.Period.ToString() == request.Period);

        query = query.OrderByDescending(s => s.SnapshotDate);

        if (request.Offset.HasValue)
            query = query.Skip(request.Offset.Value);

        if (request.Limit.HasValue)
            query = query.Take(request.Limit.Value);

        var snapshots = await query.ToListAsync();
        return snapshots.Select(MapToSnapshotDto).ToList();
    }

    public Task<List<UserAnalyticsDto>> GetUserAnalyticsAsync(AnalyticsRequestDto request)
    {
        // TODO: In a proper microservices architecture, user analytics should be calculated
        // from AnalyticsSnapshot data or received via events from other services
        // For now, returning empty list as this service should not directly access User entities
        
        _logger.LogWarning("GetUserAnalyticsAsync called - this should be implemented via event-based analytics");
        return Task.FromResult(new List<UserAnalyticsDto>());
    }

    public Task<List<VehicleAnalyticsDto>> GetVehicleAnalyticsAsync(AnalyticsRequestDto request)
    {
        // TODO: In a proper microservices architecture, vehicle analytics should be calculated
        // from AnalyticsSnapshot data or received via events from other services
        // For now, returning empty list as this service should not directly access Vehicle entities
        
        _logger.LogWarning("GetVehicleAnalyticsAsync called - this should be implemented via event-based analytics");
        return Task.FromResult(new List<VehicleAnalyticsDto>());
    }

    public Task<List<GroupAnalyticsDto>> GetGroupAnalyticsAsync(AnalyticsRequestDto request)
    {
        // TODO: In a proper microservices architecture, group analytics should be calculated
        // from AnalyticsSnapshot data or received via events from other services
        // For now, returning empty list as this service should not directly access Group entities
        
        _logger.LogWarning("GetGroupAnalyticsAsync called - this should be implemented via event-based analytics");
        return Task.FromResult(new List<GroupAnalyticsDto>());
    }

    public async Task<AnalyticsSnapshotDto> CreateSnapshotAsync(CreateAnalyticsSnapshotDto dto)
    {
        var snapshot = new AnalyticsSnapshot
        {
            GroupId = dto.GroupId,
            VehicleId = dto.VehicleId,
            SnapshotDate = dto.SnapshotDate,
            Period = Enum.Parse<AnalyticsPeriod>(dto.Period)
        };

        // Calculate snapshot data
        await PopulateSnapshotData(snapshot);

        _context.AnalyticsSnapshots.Add(snapshot);
        await _context.SaveChangesAsync();

        return MapToSnapshotDto(snapshot);
    }

    public async Task<bool> ProcessAnalyticsAsync(Guid? groupId = null, Guid? vehicleId = null)
    {
        try
        {
            var period = AnalyticsPeriod.Daily;
            var snapshotDate = DateTime.UtcNow.Date;

            var snapshot = new AnalyticsSnapshot
            {
                GroupId = groupId,
                VehicleId = vehicleId,
                SnapshotDate = snapshotDate,
                Period = period
            };

            await PopulateSnapshotData(snapshot);

            _context.AnalyticsSnapshots.Add(snapshot);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Processed analytics for GroupId: {GroupId}, VehicleId: {VehicleId}", 
                groupId, vehicleId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing analytics for GroupId: {GroupId}, VehicleId: {VehicleId}", 
                groupId, vehicleId);
            return false;
        }
    }

    public async Task<bool> GeneratePeriodicAnalyticsAsync(AnalyticsPeriod period, DateTime? startDate = null)
    {
        try
        {
            var date = startDate ?? DateTime.UtcNow;
            
            var snapshot = new AnalyticsSnapshot
            {
                SnapshotDate = date,
                Period = period
            };

            await PopulateSnapshotData(snapshot);

            _context.AnalyticsSnapshots.Add(snapshot);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Generated periodic analytics for period: {Period}, date: {Date}", 
                period, date);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating periodic analytics for period: {Period}", period);
            return false;
        }
    }

    public Task<Dictionary<string, object>> GetKpiMetricsAsync(AnalyticsRequestDto request)
    {
        // TODO: In a proper microservices architecture, KPI metrics should be calculated
        // from AnalyticsSnapshot data or received via events from other services
        // For now, returning placeholder values
        
        var metrics = new Dictionary<string, object>
        {
            ["TotalRevenue"] = 0,
            ["TotalExpenses"] = 0,
            ["NetProfit"] = 0,
            ["ProfitMargin"] = 0,
            ["TotalBookings"] = 0,
            ["CompletedBookings"] = 0,
            ["BookingCompletionRate"] = 0,
            ["UtilizationRate"] = 0
        };

        _logger.LogWarning("GetKpiMetricsAsync called - this should be implemented via event-based analytics");
        return Task.FromResult(metrics);
    }

    public Task<List<Dictionary<string, object>>> GetTrendDataAsync(AnalyticsRequestDto request)
    {
        // TODO: In a proper microservices architecture, trend data should be calculated
        // from AnalyticsSnapshot data or received via events from other services
        // For now, returning empty list
        
        _logger.LogWarning("GetTrendDataAsync called - this should be implemented via event-based analytics");
        return Task.FromResult(new List<Dictionary<string, object>>());
    }

    // TODO: These methods should be removed or refactored to work with AnalyticsSnapshot data
    // They currently access entities from other services which violates microservices principles

    private async Task<List<AnalyticsSnapshotDto>> GetWeeklyTrendsAsync(AnalyticsRequestDto request)
    {
        var weeklySnapshots = await _context.AnalyticsSnapshots
            .Where(s => s.Period == AnalyticsPeriod.Weekly)
            .OrderByDescending(s => s.SnapshotDate)
            .Take(12)
            .ToListAsync();

        return weeklySnapshots.Select(MapToSnapshotDto).ToList();
    }

    private async Task<List<AnalyticsSnapshotDto>> GetMonthlyTrendsAsync(AnalyticsRequestDto request)
    {
        var monthlySnapshots = await _context.AnalyticsSnapshots
            .Where(s => s.Period == AnalyticsPeriod.Monthly)
            .OrderByDescending(s => s.SnapshotDate)
            .Take(12)
            .ToListAsync();

        return monthlySnapshots.Select(MapToSnapshotDto).ToList();
    }

    // TODO: These calculation methods should be removed or refactored to work with AnalyticsSnapshot data
    // They currently access entities from other services which violates microservices principles

    private Task PopulateSnapshotData(AnalyticsSnapshot snapshot)
    {
        // TODO: In a proper microservices architecture, this method should receive data
        // via events from other services rather than directly querying their entities
        // For now, using placeholder values
        
        var periodStart = snapshot.SnapshotDate.Date;
        var periodEnd = snapshot.Period switch
        {
            AnalyticsPeriod.Daily => periodStart.AddDays(1),
            AnalyticsPeriod.Weekly => periodStart.AddDays(7),
            AnalyticsPeriod.Monthly => periodStart.AddMonths(1),
            AnalyticsPeriod.Quarterly => periodStart.AddMonths(3),
            AnalyticsPeriod.Yearly => periodStart.AddYears(1),
            _ => periodStart.AddDays(1)
        };

        // Placeholder values - should be populated from event data
        snapshot.TotalBookings = 0;
        snapshot.TotalUsageHours = 0;
        snapshot.ActiveUsers = 0;
        snapshot.UtilizationRate = 0;
        snapshot.TotalRevenue = 0;
        snapshot.TotalExpenses = 0;
        snapshot.NetProfit = 0;
        snapshot.AverageCostPerHour = 0;
        snapshot.TotalDistance = 0;
        snapshot.AverageCostPerKm = 0;
        snapshot.MaintenanceEfficiency = 0.85m;
        snapshot.UserSatisfactionScore = 0.90m;
        
        _logger.LogInformation("Populated snapshot data with placeholder values for period {Period} from {Start} to {End}", 
            snapshot.Period, periodStart, periodEnd);
        
        return Task.CompletedTask;
    }

    private static AnalyticsSnapshotDto MapToSnapshotDto(AnalyticsSnapshot snapshot)
    {
        return new AnalyticsSnapshotDto
        {
            Id = snapshot.Id,
            GroupId = snapshot.GroupId,
            VehicleId = snapshot.VehicleId,
            SnapshotDate = snapshot.SnapshotDate,
            Period = snapshot.Period.ToString(),
            TotalDistance = snapshot.TotalDistance,
            TotalBookings = snapshot.TotalBookings,
            TotalUsageHours = snapshot.TotalUsageHours,
            ActiveUsers = snapshot.ActiveUsers,
            TotalRevenue = snapshot.TotalRevenue,
            TotalExpenses = snapshot.TotalExpenses,
            NetProfit = snapshot.NetProfit,
            AverageCostPerHour = snapshot.AverageCostPerHour,
            AverageCostPerKm = snapshot.AverageCostPerKm,
            UtilizationRate = snapshot.UtilizationRate,
            MaintenanceEfficiency = snapshot.MaintenanceEfficiency,
            UserSatisfactionScore = snapshot.UserSatisfactionScore
        };
    }

    #region Usage vs Ownership Analytics

    public async Task<UsageVsOwnershipDto> GetUsageVsOwnershipAsync(Guid groupId, DateTime? startDate = null, DateTime? endDate = null)
    {
        var group = await _mainContext.OwnershipGroups
            .Include(g => g.Members)
                .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(g => g.Id == groupId);

        if (group == null)
            throw new KeyNotFoundException($"Group with ID {groupId} not found");

        var periodStart = startDate ?? DateTime.UtcNow.AddMonths(-1);
        var periodEnd = endDate ?? DateTime.UtcNow;

        var result = new UsageVsOwnershipDto
        {
            GroupId = groupId,
            GroupName = group.Name,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            GeneratedAt = DateTime.UtcNow
        };

        // Get all bookings for the group in the period
        var bookings = await _mainContext.Bookings
            .Include(b => b.CheckIns)
            .Where(b => b.GroupId == groupId && 
                       b.Status == BookingStatus.Completed &&
                       b.EndAt >= periodStart && 
                       b.StartAt <= periodEnd)
            .ToListAsync();

        // Get expenses for cost calculation
        var expenses = await _mainContext.Expenses
            .Where(e => e.GroupId == groupId && 
                       e.DateIncurred >= periodStart && 
                       e.DateIncurred <= periodEnd)
            .ToListAsync();

        // Get payments for cost attribution
        var payments = await _mainContext.Payments
            .Include(p => p.Invoice)
            .Where(p => p.Invoice != null && 
                       p.Status == PaymentStatus.Completed &&
                       p.PaidAt >= periodStart && 
                       p.PaidAt <= periodEnd)
            .ToListAsync();

        // Calculate metrics for each member
        var memberMetrics = new List<MemberUsageMetricsDto>();
        
        foreach (var member in group.Members)
        {
            var memberBookings = bookings.Where(b => b.UserId == member.UserId).ToList();
            
            // Calculate usage by trips
            var totalTrips = bookings.Count;
            var memberTrips = memberBookings.Count;
            var tripsPercentage = totalTrips > 0 ? (decimal)memberTrips / totalTrips * 100 : 0;

            // Calculate usage by distance (from check-ins)
            var totalDistanceAll = CalculateTotalDistance(bookings);
            var memberDistance = CalculateMemberDistance(memberBookings);
            var distancePercentage = totalDistanceAll > 0 ? (decimal)memberDistance / totalDistanceAll * 100 : 0;

            // Calculate usage by time
            var totalHours = bookings.Sum(b => (int)(b.EndAt - b.StartAt).TotalHours);
            var memberHours = memberBookings.Sum(b => (int)(b.EndAt - b.StartAt).TotalHours);
            var hoursPercentage = totalHours > 0 ? (decimal)memberHours / totalHours * 100 : 0;

            // Calculate usage by cost (from payments)
            var totalCost = payments.Sum(p => p.Amount);
            var memberCost = payments.Where(p => p.PayerId == member.UserId).Sum(p => p.Amount);
            var costPercentage = totalCost > 0 ? memberCost / totalCost * 100 : 0;

            // Calculate overall usage percentage (average of all metrics)
            var overallUsagePercentage = (tripsPercentage + distancePercentage + hoursPercentage + costPercentage) / 4;
            var ownershipPercentage = (decimal)(member.SharePercentage * 100);
            var usageDifference = overallUsagePercentage - ownershipPercentage;

            var metrics = new MemberUsageMetricsDto
            {
                MemberId = member.UserId,
                MemberName = $"{member.User.FirstName} {member.User.LastName}",
                OwnershipPercentage = ownershipPercentage,
                ByTrips = new UsageMetricsDto
                {
                    Value = memberTrips,
                    ActualUsagePercentage = tripsPercentage,
                    ExpectedUsagePercentage = ownershipPercentage,
                    Difference = tripsPercentage - ownershipPercentage
                },
                ByDistance = new UsageMetricsDto
                {
                    Value = memberDistance,
                    ActualUsagePercentage = distancePercentage,
                    ExpectedUsagePercentage = ownershipPercentage,
                    Difference = distancePercentage - ownershipPercentage
                },
                ByTime = new UsageMetricsDto
                {
                    Value = memberHours,
                    ActualUsagePercentage = hoursPercentage,
                    ExpectedUsagePercentage = ownershipPercentage,
                    Difference = hoursPercentage - ownershipPercentage
                },
                ByCost = new UsageMetricsDto
                {
                    Value = memberCost,
                    ActualUsagePercentage = costPercentage,
                    ExpectedUsagePercentage = ownershipPercentage,
                    Difference = costPercentage - ownershipPercentage
                },
                OverallUsagePercentage = overallUsagePercentage,
                UsageDifference = usageDifference,
                FairShareIndicator = Math.Abs(usageDifference) < 5 ? "Fair" : (usageDifference > 0 ? "Over" : "Under"),
                FairnessScore = Math.Max(0, Math.Min(100, 100 - Math.Abs(usageDifference) * 2))
            };

            memberMetrics.Add(metrics);
        }

        result.Members = memberMetrics;

        // Calculate group-level metrics
        result.GroupMetrics = CalculateGroupFairnessMetrics(memberMetrics, bookings.Count);

        // Get historical trends
        result.HistoricalTrends = await GetHistoricalTrendsAsync(groupId, periodStart, periodEnd);

        return result;
    }

    private GroupFairnessMetricsDto CalculateGroupFairnessMetrics(List<MemberUsageMetricsDto> members, int totalBookings)
    {
        if (!members.Any())
            return new GroupFairnessMetricsDto();

        // Calculate overall fairness score (average of member fairness scores)
        var overallFairnessScore = members.Average(m => m.FairnessScore);

        // Calculate distribution balance (how evenly distributed usage is)
        // Using coefficient of variation (lower is more balanced)
        var usageValues = members.Select(m => m.OverallUsagePercentage).ToList();
        var mean = (double)usageValues.Average();
        var variance = usageValues.Sum(v => Math.Pow((double)v - mean, 2)) / usageValues.Count;
        var stdDev = Math.Sqrt(variance);
        var coefficientOfVariation = mean > 0 ? (decimal)(stdDev / mean) : 1;
        var distributionBalance = Math.Max(0, 1 - coefficientOfVariation);

        // Calculate usage concentration (what % of usage is from top users)
        var sortedMembers = members.OrderByDescending(m => m.OverallUsagePercentage).ToList();
        var topUsersCount = Math.Max(1, members.Count / 3); // Top third
        var topUsersUsagePercentage = sortedMembers.Take(topUsersCount).Sum(m => m.OverallUsagePercentage);

        // Calculate Gini coefficient
        var giniCoefficient = CalculateGiniCoefficient(usageValues);

        var metrics = new GroupFairnessMetricsDto
        {
            OverallFairnessScore = overallFairnessScore,
            DistributionBalance = distributionBalance,
            UsageConcentration = topUsersUsagePercentage / 100,
            GiniCoefficient = giniCoefficient,
            TopUsersCount = topUsersCount,
            TopUsersUsagePercentage = topUsersUsagePercentage
        };

        // Generate recommendations
        metrics.Recommendations = GenerateRecommendations(members, metrics);

        return metrics;
    }

    private decimal CalculateGiniCoefficient(List<decimal> values)
    {
        if (!values.Any() || values.All(v => v == 0))
            return 0;

        var sorted = values.OrderBy(v => v).ToList();
        var n = sorted.Count;
        var sum = sorted.Sum();
        var mean = sum / n;

        if (mean == 0)
            return 0;

        var numerator = 0m;
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                numerator += Math.Abs(sorted[i] - sorted[j]);
            }
        }

        return numerator / (2 * n * n * mean);
    }

    private List<string> GenerateRecommendations(List<MemberUsageMetricsDto> members, GroupFairnessMetricsDto metrics)
    {
        var recommendations = new List<string>();

        if (metrics.OverallFairnessScore < 70)
        {
            recommendations.Add("Overall fairness score is below optimal. Consider redistributing usage or ownership shares.");
        }

        var overUtilizers = members.Where(m => m.FairShareIndicator == "Over" && Math.Abs(m.UsageDifference) > 10).ToList();
        if (overUtilizers.Any())
        {
            recommendations.Add($"{overUtilizers.Count} member(s) are significantly over-utilizing. Consider usage limits or additional contributions.");
        }

        var underUtilizers = members.Where(m => m.FairShareIndicator == "Under" && Math.Abs(m.UsageDifference) > 10).ToList();
        if (underUtilizers.Any())
        {
            recommendations.Add($"{underUtilizers.Count} member(s) are significantly under-utilizing. Encourage more active participation.");
        }

        if (metrics.UsageConcentration > 0.7m)
        {
            recommendations.Add("Usage is highly concentrated among few members. Consider incentive programs to encourage broader participation.");
        }

        if (metrics.GiniCoefficient > 0.4m)
        {
            recommendations.Add("High inequality in usage distribution detected. Review ownership percentages and usage patterns.");
        }

        return recommendations;
    }

    public async Task<MemberComparisonDto> GetMemberComparisonAsync(Guid groupId, DateTime? startDate = null, DateTime? endDate = null)
    {
        var group = await _mainContext.OwnershipGroups
            .Include(g => g.Members)
                .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(g => g.Id == groupId);

        if (group == null)
            throw new KeyNotFoundException($"Group with ID {groupId} not found");

        var periodStart = startDate ?? DateTime.UtcNow.AddMonths(-1);
        var periodEnd = endDate ?? DateTime.UtcNow;

        var bookings = await _mainContext.Bookings
            .Include(b => b.CheckIns)
            .Where(b => b.GroupId == groupId && 
                       b.Status == BookingStatus.Completed &&
                       b.EndAt >= periodStart && 
                       b.StartAt <= periodEnd)
            .ToListAsync();

        var expenses = await _mainContext.Expenses
            .Where(e => e.GroupId == groupId && 
                       e.DateIncurred >= periodStart && 
                       e.DateIncurred <= periodEnd)
            .ToListAsync();

        var payments = await _mainContext.Payments
            .Include(p => p.Invoice)
            .Where(p => p.Invoice != null && 
                       p.Status == PaymentStatus.Completed &&
                       p.PaidAt >= periodStart && 
                       p.PaidAt <= periodEnd)
            .ToListAsync();

        var result = new MemberComparisonDto
        {
            GroupId = groupId,
            GroupName = group.Name,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            GeneratedAt = DateTime.UtcNow
        };

        var totalTrips = bookings.Count;
        var totalDistance = CalculateTotalDistance(bookings);
        var totalHours = bookings.Sum(b => (int)(b.EndAt - b.StartAt).TotalHours);
        var totalCost = payments.Sum(p => p.Amount);

        var comparisonItems = new List<MemberComparisonItemDto>();

        foreach (var member in group.Members)
        {
            var memberBookings = bookings.Where(b => b.UserId == member.UserId).ToList();
            var memberTrips = memberBookings.Count;
            var memberDistance = CalculateMemberDistance(memberBookings);
            var memberHours = memberBookings.Sum(b => (int)(b.EndAt - b.StartAt).TotalHours);
            var memberCost = payments.Where(p => p.PayerId == member.UserId).Sum(p => p.Amount);

            var lastBooking = memberBookings.OrderByDescending(b => b.EndAt).FirstOrDefault();
            var daysSinceLastBooking = lastBooking != null 
                ? (DateTime.UtcNow - lastBooking.EndAt).Days 
                : (int)(DateTime.UtcNow - periodStart).TotalDays;

            var weeksInPeriod = Math.Max(1, (periodEnd - periodStart).Days / 7.0);
            var bookingFrequency = memberTrips / (decimal)weeksInPeriod;

            var activityLevel = bookingFrequency switch
            {
                >= 2 => "Active",
                >= 0.5m => "Moderate",
                _ => "Inactive"
            };

            var distancePerTrip = memberTrips > 0 ? memberDistance / memberTrips : 0;
            var hoursPerTrip = memberTrips > 0 ? (decimal)memberHours / memberTrips : 0;
            var costPerTrip = memberTrips > 0 ? memberCost / memberTrips : 0;
            var costPerHour = memberHours > 0 ? memberCost / memberHours : 0;
            var costPerKm = memberDistance > 0 ? memberCost / memberDistance : 0;

            var ownershipPercentage = (decimal)(member.SharePercentage * 100);
            var usagePercentageByTrips = totalTrips > 0 ? (decimal)memberTrips / totalTrips * 100 : 0;
            var usagePercentageByDistance = totalDistance > 0 ? memberDistance / totalDistance * 100 : 0;
            var usagePercentageByTime = totalHours > 0 ? (decimal)memberHours / totalHours * 100 : 0;
            var usagePercentageByCost = totalCost > 0 ? memberCost / totalCost * 100 : 0;

            var overallUsagePercentage = (usagePercentageByTrips + usagePercentageByDistance + usagePercentageByTime + usagePercentageByCost) / 4;
            var usageDifference = overallUsagePercentage - ownershipPercentage;
            var fairnessScore = Math.Max(0, Math.Min(100, 100 - Math.Abs(usageDifference) * 2));
            var fairShareStatus = Math.Abs(usageDifference) < 5 ? "Fair" : (usageDifference > 0 ? "Over-utilizing" : "Under-utilizing");

            comparisonItems.Add(new MemberComparisonItemDto
            {
                MemberId = member.UserId,
                MemberName = $"{member.User.FirstName} {member.User.LastName}",
                OwnershipPercentage = ownershipPercentage,
                TotalTrips = memberTrips,
                TotalDistance = memberDistance,
                TotalHours = memberHours,
                TotalCost = memberCost,
                UsagePercentageByTrips = usagePercentageByTrips,
                UsagePercentageByDistance = usagePercentageByDistance,
                UsagePercentageByTime = usagePercentageByTime,
                UsagePercentageByCost = usagePercentageByCost,
                DistancePerTrip = distancePerTrip,
                HoursPerTrip = hoursPerTrip,
                CostPerTrip = costPerTrip,
                CostPerHour = costPerHour,
                CostPerKm = costPerKm,
                ActivityLevel = activityLevel,
                DaysSinceLastBooking = daysSinceLastBooking,
                BookingFrequency = bookingFrequency,
                FairnessScore = fairnessScore,
                FairShareStatus = fairShareStatus
            });
        }

        result.Members = comparisonItems.OrderByDescending(m => m.TotalTrips).ToList();
        return result;
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

    private decimal CalculateMemberDistance(List<Booking> memberBookings)
    {
        return memberBookings
            .SelectMany(b => b.CheckIns)
            .Where(c => c.Type == CheckInType.CheckOut)
            .Select(c =>
            {
                var booking = memberBookings.FirstOrDefault(b => b.Id == c.BookingId);
                var checkIn = booking?.CheckIns.FirstOrDefault(ci => ci.Type == CheckInType.CheckIn && ci.BookingId == c.BookingId);
                if (checkIn != null)
                {
                    return Math.Max(0, c.Odometer - checkIn.Odometer);
                }
                return 0;
            })
            .Sum();
    }

    public async Task<VisualizationDataDto> GetVisualizationDataAsync(Guid groupId, DateTime? startDate = null, DateTime? endDate = null)
    {
        var group = await _mainContext.OwnershipGroups
            .Include(g => g.Members)
                .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(g => g.Id == groupId);

        if (group == null)
            throw new KeyNotFoundException($"Group with ID {groupId} not found");

        var periodStart = startDate ?? DateTime.UtcNow.AddMonths(-1);
        var periodEnd = endDate ?? DateTime.UtcNow;

        var bookings = await _mainContext.Bookings
            .Include(b => b.CheckIns)
            .Where(b => b.GroupId == groupId && 
                       b.Status == BookingStatus.Completed &&
                       b.EndAt >= periodStart && 
                       b.StartAt <= periodEnd)
            .ToListAsync();

        var result = new VisualizationDataDto
        {
            GroupId = groupId,
            GroupName = group.Name
        };

        // Pie chart data - usage distribution by member (by trips)
        var totalTrips = bookings.Count;
        foreach (var member in group.Members)
        {
            var memberTrips = bookings.Count(b => b.UserId == member.UserId);
            var percentage = totalTrips > 0 ? (decimal)memberTrips / totalTrips * 100 : 0;
            result.UsageDistributionByMember.Add(new ChartDataPointDto
            {
                Label = $"{member.User.FirstName} {member.User.LastName}",
                Value = memberTrips,
                Percentage = percentage
            });
        }

        // Bar chart data - ownership vs usage comparison
        var usageVsOwnership = await GetUsageVsOwnershipAsync(groupId, startDate, endDate);
        foreach (var member in usageVsOwnership.Members)
        {
            result.OwnershipVsUsage.Add(new ComparisonBarDataDto
            {
                MemberName = member.MemberName,
                OwnershipPercentage = member.OwnershipPercentage,
                UsagePercentage = member.OverallUsagePercentage,
                Metric = "Overall"
            });
        }

        // Timeline chart data - usage trends (daily)
        var dailyGroups = bookings
            .GroupBy(b => b.StartAt.Date)
            .OrderBy(g => g.Key)
            .ToList();

        foreach (var dayGroup in dailyGroups)
        {
            var dayBookings = dayGroup.ToList();
            var totalUsage = dayBookings.Sum(b => (decimal)(b.EndAt - b.StartAt).TotalHours);
            
            // Calculate average fairness for the day (simplified)
            var averageFairness = 75m; // Would need to calculate per day
            var activeMembers = dayBookings.Select(b => b.UserId).Distinct().Count();

            result.UsageTrends.Add(new TimelineDataPointDto
            {
                Date = dayGroup.Key,
                TotalUsage = totalUsage,
                AverageFairnessScore = averageFairness,
                ActiveMembers = activeMembers
            });
        }

        // Heat map data - usage by time/day
        var dayNames = new[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
        
        for (int hour = 0; hour < 24; hour++)
        {
            foreach (var dayName in dayNames)
            {
                var dayOfWeek = (DayOfWeek)Array.IndexOf(dayNames, dayName);
                var hourBookings = bookings.Where(b => 
                    b.StartAt.DayOfWeek == dayOfWeek && 
                    b.StartAt.Hour == hour).ToList();

                var usageValue = hourBookings.Sum(b => (decimal)(b.EndAt - b.StartAt).TotalHours);

                result.UsageHeatMap.Add(new HeatMapDataPointDto
                {
                    DayOfWeek = dayName,
                    Hour = hour,
                    UsageValue = usageValue,
                    BookingCount = hourBookings.Count
                });
            }
        }

        return result;
    }

    public async Task<FairnessReportDto> GetFairnessReportAsync(Guid groupId, DateTime? startDate = null, DateTime? endDate = null)
    {
        var group = await _mainContext.OwnershipGroups
            .FirstOrDefaultAsync(g => g.Id == groupId);

        if (group == null)
            throw new KeyNotFoundException($"Group with ID {groupId} not found");

        var periodStart = startDate ?? DateTime.UtcNow.AddMonths(-1);
        var periodEnd = endDate ?? DateTime.UtcNow;

        var usageVsOwnership = await GetUsageVsOwnershipAsync(groupId, startDate, endDate);
        var visualizationData = await GetVisualizationDataAsync(groupId, startDate, endDate);
        var historicalComparison = await GetPeriodComparisonsAsync(groupId, "month");

        var report = new FairnessReportDto
        {
            GroupId = groupId,
            GroupName = group.Name,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            OverallFairnessScore = usageVsOwnership.GroupMetrics.OverallFairnessScore,
            OverallAssessment = usageVsOwnership.GroupMetrics.OverallFairnessScore switch
            {
                >= 90 => "Excellent - Usage is very well balanced with ownership",
                >= 75 => "Good - Minor imbalances exist but within acceptable range",
                >= 60 => "Fair - Some significant imbalances that should be addressed",
                _ => "Poor - Major imbalances require immediate attention"
            },
            MemberBreakdown = usageVsOwnership.Members,
            HistoricalComparison = historicalComparison,
            VisualizationData = visualizationData
        };

        // Generate detailed recommendations
        var recommendations = new List<RecommendationDto>();

        foreach (var member in usageVsOwnership.Members)
        {
            if (member.FairShareIndicator == "Over" && Math.Abs(member.UsageDifference) > 10)
            {
                recommendations.Add(new RecommendationDto
                {
                    Category = "Usage",
                    Title = $"{member.MemberName} is over-utilizing",
                    Description = $"{member.MemberName} is using {member.UsageDifference:F1}% more than their ownership share.",
                    Priority = Math.Abs(member.UsageDifference) > 20 ? "High" : "Medium",
                    Impact = "Consider usage limits or additional cost contributions to balance the group."
                });
            }
            else if (member.FairShareIndicator == "Under" && Math.Abs(member.UsageDifference) > 10)
            {
                recommendations.Add(new RecommendationDto
                {
                    Category = "Activity",
                    Title = $"{member.MemberName} is under-utilizing",
                    Description = $"{member.MemberName} is using {Math.Abs(member.UsageDifference):F1}% less than their ownership share.",
                    Priority = "Low",
                    Impact = "Encourage more active participation or consider adjusting ownership share."
                });
            }
        }

        if (usageVsOwnership.GroupMetrics.UsageConcentration > 0.7m)
        {
            recommendations.Add(new RecommendationDto
            {
                Category = "Ownership",
                Title = "High usage concentration",
                Description = $"Top {usageVsOwnership.GroupMetrics.TopUsersCount} members account for {usageVsOwnership.GroupMetrics.TopUsersUsagePercentage:F1}% of usage.",
                Priority = "Medium",
                Impact = "Consider redistributing ownership shares or implementing usage incentives."
            });
        }

        report.Recommendations = recommendations;

        return report;
    }

    public async Task<List<PeriodComparisonDto>> GetPeriodComparisonsAsync(Guid groupId, string comparisonType = "month")
    {
        var comparisons = new List<PeriodComparisonDto>();
        var now = DateTime.UtcNow;

        switch (comparisonType.ToLower())
        {
            case "month":
                // Compare last 6 months
                for (int i = 5; i >= 0; i--)
                {
                    var periodStart = new DateTime(now.Year, now.Month, 1).AddMonths(-i);
                    var periodEnd = periodStart.AddMonths(1).AddDays(-1);
                    var comparison = await CalculatePeriodComparisonAsync(groupId, periodStart, periodEnd, $"Month {periodStart:MMM yyyy}");
                    if (comparison != null)
                        comparisons.Add(comparison);
                }
                break;

            case "quarter":
                // Compare last 4 quarters
                var currentQuarter = (now.Month - 1) / 3;
                for (int i = 3; i >= 0; i--)
                {
                    var quarterStart = new DateTime(now.Year, (currentQuarter - i) * 3 + 1, 1);
                    if (quarterStart > now) continue;
                    var quarterEnd = quarterStart.AddMonths(3).AddDays(-1);
                    var quarterComparison = await CalculatePeriodComparisonAsync(groupId, quarterStart, quarterEnd, $"Q{((currentQuarter - i) % 4 + 4) % 4 + 1} {quarterStart.Year}");
                    if (quarterComparison != null)
                        comparisons.Add(quarterComparison);
                }
                break;

            case "year":
                // Compare last 3 years
                for (int i = 2; i >= 0; i--)
                {
                    var yearStart = new DateTime(now.Year - i, 1, 1);
                    var yearEnd = new DateTime(now.Year - i, 12, 31);
                    var yearComparison = await CalculatePeriodComparisonAsync(groupId, yearStart, yearEnd, $"Year {yearStart.Year}");
                    if (yearComparison != null)
                        comparisons.Add(yearComparison);
                }
                break;
        }

        return comparisons;
    }

    private async Task<PeriodComparisonDto?> CalculatePeriodComparisonAsync(Guid groupId, DateTime periodStart, DateTime periodEnd, string label)
    {
        try
        {
            var usageVsOwnership = await GetUsageVsOwnershipAsync(groupId, periodStart, periodEnd);
            
            return new PeriodComparisonDto
            {
                PeriodLabel = label,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                AverageFairnessScore = usageVsOwnership.GroupMetrics.OverallFairnessScore,
                AverageUsageBalance = usageVsOwnership.GroupMetrics.DistributionBalance * 100,
                ActiveMembers = usageVsOwnership.Members.Count(m => m.OverallUsagePercentage > 0)
            };
        }
        catch
        {
            return null;
        }
    }

    private async Task<List<PeriodComparisonDto>> GetHistoricalTrendsAsync(Guid groupId, DateTime periodStart, DateTime periodEnd)
    {
        // Get monthly trends for the past 6 months
        var trends = new List<PeriodComparisonDto>();
        var now = DateTime.UtcNow;

        for (int i = 5; i >= 0; i--)
        {
            var monthStart = new DateTime(now.Year, now.Month, 1).AddMonths(-i);
            if (monthStart > periodEnd) break;
            if (monthStart.AddMonths(1).AddDays(-1) < periodStart) continue;

            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            var trend = await CalculatePeriodComparisonAsync(groupId, monthStart, monthEnd, $"Month {monthStart:MMM yyyy}");
            if (trend != null)
                trends.Add(trend);
        }

        return trends;
    }

    #endregion
}
