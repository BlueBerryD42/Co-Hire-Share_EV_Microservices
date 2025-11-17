using Microsoft.EntityFrameworkCore;
using CoOwnershipVehicle.Analytics.Api.Data;
using CoOwnershipVehicle.Analytics.Api.Services.HttpClients;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Analytics.Api.Services;

public class AnalyticsService : IAnalyticsService
{
    private readonly AnalyticsDbContext _context;
    private readonly IUserServiceClient _userServiceClient;
    private readonly IGroupServiceClient _groupServiceClient;
    private readonly IVehicleServiceClient _vehicleServiceClient;
    private readonly IBookingServiceClient _bookingServiceClient;
    private readonly IPaymentServiceClient _paymentServiceClient;
    private readonly ILogger<AnalyticsService> _logger;

    public AnalyticsService(
        AnalyticsDbContext context,
        IUserServiceClient userServiceClient,
        IGroupServiceClient groupServiceClient,
        IVehicleServiceClient vehicleServiceClient,
        IBookingServiceClient bookingServiceClient,
        IPaymentServiceClient paymentServiceClient,
        ILogger<AnalyticsService> logger)
    {
        _context = context;
        _userServiceClient = userServiceClient;
        _groupServiceClient = groupServiceClient;
        _vehicleServiceClient = vehicleServiceClient;
        _bookingServiceClient = bookingServiceClient;
        _paymentServiceClient = paymentServiceClient;
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

        // Get statistics from services via HTTP calls
        try
        {
            // Overall Statistics - get from services via HTTP
            var groups = await _groupServiceClient.GetGroupsAsync();
            dashboard.TotalGroups = groups.Count;
            
            var vehicles = await _vehicleServiceClient.GetVehiclesAsync();
            dashboard.TotalVehicles = vehicles.Count;
            
            var users = await _userServiceClient.GetUsersAsync();
            dashboard.TotalUsers = users.Count;
            
            var bookings = await _bookingServiceClient.GetBookingsAsync(periodStart, periodEnd);
            dashboard.TotalBookings = bookings.Count;

            // Financial Overview - get from Payment service via HTTP
            var payments = await _paymentServiceClient.GetPaymentsAsync(periodStart, periodEnd);
            dashboard.TotalRevenue = payments.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount);
            
            var expenses = await _paymentServiceClient.GetExpensesAsync(null, periodStart, periodEnd);
            dashboard.TotalExpenses = expenses.Sum(e => e.Amount);
            dashboard.NetProfit = dashboard.TotalRevenue - dashboard.TotalExpenses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching dashboard statistics from services");
            // Set defaults on error
            dashboard.TotalGroups = 0;
            dashboard.TotalVehicles = 0;
            dashboard.TotalUsers = 0;
            dashboard.TotalBookings = 0;
            dashboard.TotalRevenue = 0;
            dashboard.TotalExpenses = 0;
            dashboard.NetProfit = 0;
        }

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

    public async Task<List<UserAnalyticsDto>> GetUserAnalyticsAsync(AnalyticsRequestDto request)
    {
        try
        {
            var periodStart = request.StartDate ?? DateTime.UtcNow.AddMonths(-1);
            var periodEnd = request.EndDate ?? DateTime.UtcNow;

            // Get users from User service via HTTP
            var users = await _userServiceClient.GetUsersAsync();
            
            // Get bookings to calculate user analytics
            var bookings = await _bookingServiceClient.GetBookingsAsync(periodStart, periodEnd);
            
            var userAnalytics = new List<UserAnalyticsDto>();
            
            foreach (var user in users)
            {
                var userBookings = bookings.Where(b => b.UserId == user.Id).ToList();
                var completedBookings = userBookings.Count(b => b.Status == BookingStatus.Completed);
                var totalHours = userBookings.Where(b => b.Status == BookingStatus.Completed)
                    .Sum(b => (int)(b.EndAt - b.StartAt).TotalHours);
                
                userAnalytics.Add(new UserAnalyticsDto
                {
                    UserId = user.Id,
                    UserName = $"{user.FirstName} {user.LastName}",
                    TotalBookings = userBookings.Count,
                    CompletedBookings = completedBookings,
                    TotalUsageHours = totalHours,
                    AverageBookingDuration = userBookings.Any() ? (decimal)userBookings.Average(b => (b.EndAt - b.StartAt).TotalHours) : 0
                });
            }
            
            return userAnalytics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user analytics");
            return new List<UserAnalyticsDto>();
        }
    }

    public async Task<List<VehicleAnalyticsDto>> GetVehicleAnalyticsAsync(AnalyticsRequestDto request)
    {
        try
        {
            var periodStart = request.StartDate ?? DateTime.UtcNow.AddMonths(-1);
            var periodEnd = request.EndDate ?? DateTime.UtcNow;

            // Get vehicles from Vehicle service via HTTP
            var vehicles = await _vehicleServiceClient.GetVehiclesAsync();
            
            // Get bookings to calculate vehicle analytics
            var bookings = await _bookingServiceClient.GetBookingsAsync(periodStart, periodEnd);
            
            var vehicleAnalytics = new List<VehicleAnalyticsDto>();
            
            foreach (var vehicle in vehicles)
            {
                var vehicleBookings = bookings.Where(b => b.VehicleId == vehicle.Id).ToList();
                var completedBookings = vehicleBookings.Count(b => b.Status == BookingStatus.Completed);
                var totalHours = vehicleBookings.Where(b => b.Status == BookingStatus.Completed)
                    .Sum(b => (int)(b.EndAt - b.StartAt).TotalHours);
                var utilizationRate = periodEnd > periodStart 
                    ? (decimal)totalHours / (decimal)(periodEnd - periodStart).TotalHours * 100 
                    : 0;
                
                vehicleAnalytics.Add(new VehicleAnalyticsDto
                {
                    VehicleId = vehicle.Id,
                    VehicleModel = vehicle.Model ?? "Unknown",
                    TotalBookings = vehicleBookings.Count,
                    CompletedBookings = completedBookings,
                    UtilizationRate = Math.Min(100, utilizationRate),
                    TotalUsageHours = totalHours
                });
            }
            
            return vehicleAnalytics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting vehicle analytics");
            return new List<VehicleAnalyticsDto>();
        }
    }

    public async Task<List<GroupAnalyticsDto>> GetGroupAnalyticsAsync(AnalyticsRequestDto request)
    {
        try
        {
            var periodStart = request.StartDate ?? DateTime.UtcNow.AddMonths(-1);
            var periodEnd = request.EndDate ?? DateTime.UtcNow;

            // Get groups from Group service via HTTP
            var groups = await _groupServiceClient.GetGroupsAsync();
            
            // Get bookings and expenses to calculate group analytics
            var bookings = await _bookingServiceClient.GetBookingsAsync(periodStart, periodEnd);
            var expenses = await _paymentServiceClient.GetExpensesAsync(null, periodStart, periodEnd);
            
            var groupAnalytics = new List<GroupAnalyticsDto>();
            
            foreach (var group in groups)
            {
                var groupBookings = bookings.Where(b => b.GroupId == group.Id).ToList();
                var groupExpenses = expenses.Where(e => e.GroupId == group.Id).ToList();
                var completedBookings = groupBookings.Count(b => b.Status == BookingStatus.Completed);
                var totalHours = groupBookings.Where(b => b.Status == BookingStatus.Completed)
                    .Sum(b => (int)(b.EndAt - b.StartAt).TotalHours);
                
                groupAnalytics.Add(new GroupAnalyticsDto
                {
                    GroupId = group.Id,
                    GroupName = group.Name ?? "Unknown",
                    TotalBookings = groupBookings.Count,
                    CompletedBookings = completedBookings,
                    TotalUsageHours = totalHours,
                    TotalExpenses = groupExpenses.Sum(e => e.Amount),
                    ActiveMembers = group.Members?.Count ?? 0
                });
            }
            
            return groupAnalytics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting group analytics");
            return new List<GroupAnalyticsDto>();
        }
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

    public async Task<Dictionary<string, object>> GetKpiMetricsAsync(AnalyticsRequestDto request)
    {
        try
        {
            var periodStart = request.StartDate ?? DateTime.UtcNow.AddMonths(-1);
            var periodEnd = request.EndDate ?? DateTime.UtcNow;

            // Get data from services via HTTP
            var bookings = await _bookingServiceClient.GetBookingsAsync(periodStart, periodEnd);
            var completedBookings = bookings.Where(b => b.Status == BookingStatus.Completed).ToList();
            
            var payments = await _paymentServiceClient.GetPaymentsAsync(periodStart, periodEnd);
            var completedPayments = payments.Where(p => p.Status == PaymentStatus.Completed).ToList();
            
            var expenses = await _paymentServiceClient.GetExpensesAsync(null, periodStart, periodEnd);
            
            // Calculate KPIs
            var totalRevenue = completedPayments.Sum(p => p.Amount);
            var totalExpenses = expenses.Sum(e => e.Amount);
            var netProfit = totalRevenue - totalExpenses;
            var profitMargin = totalRevenue > 0 ? (netProfit / totalRevenue) * 100 : 0;
            var bookingCompletionRate = bookings.Count > 0 ? (decimal)completedBookings.Count / bookings.Count * 100 : 0;
            
            // Calculate utilization rate
            var totalUsageHours = completedBookings.Sum(b => (int)(b.EndAt - b.StartAt).TotalHours);
            var totalAvailableHours = (periodEnd - periodStart).TotalHours;
            var utilizationRate = totalAvailableHours > 0 ? (decimal)(totalUsageHours / totalAvailableHours * 100) : 0;
            
            var metrics = new Dictionary<string, object>
            {
                ["TotalRevenue"] = totalRevenue,
                ["TotalExpenses"] = totalExpenses,
                ["NetProfit"] = netProfit,
                ["ProfitMargin"] = Math.Round(profitMargin, 2),
                ["TotalBookings"] = bookings.Count,
                ["CompletedBookings"] = completedBookings.Count,
                ["BookingCompletionRate"] = Math.Round(bookingCompletionRate, 2),
                ["UtilizationRate"] = Math.Round(utilizationRate, 2)
            };

            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting KPI metrics");
            // Return defaults on error
            return new Dictionary<string, object>
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
        }
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

    private async Task PopulateSnapshotData(AnalyticsSnapshot snapshot)
    {
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

        try
        {
            // Get bookings for the period via HTTP
            var bookings = await _bookingServiceClient.GetBookingsAsync(periodStart, periodEnd, snapshot.GroupId);
            var completedBookings = bookings.Where(b => b.Status == BookingStatus.Completed).ToList();
            
            // Get expenses for the period via HTTP
            var expenses = await _paymentServiceClient.GetExpensesAsync(snapshot.GroupId, periodStart, periodEnd);
            
            // Get payments for revenue calculation
            var payments = await _paymentServiceClient.GetPaymentsAsync(periodStart, periodEnd);
            var completedPayments = payments.Where(p => p.Status == PaymentStatus.Completed).ToList();
            
            // Calculate metrics
            snapshot.TotalBookings = completedBookings.Count;
            snapshot.TotalUsageHours = completedBookings.Sum(b => (int)(b.EndAt - b.StartAt).TotalHours);
            
            // Calculate active users (unique users who made bookings)
            snapshot.ActiveUsers = completedBookings.Select(b => b.UserId).Distinct().Count();
            
            // Calculate utilization rate (hours used / total available hours in period)
            var totalAvailableHours = (periodEnd - periodStart).TotalHours;
            snapshot.UtilizationRate = totalAvailableHours > 0 
                ? (decimal)(snapshot.TotalUsageHours / totalAvailableHours * 100) 
                : 0;
            
            snapshot.TotalRevenue = completedPayments.Sum(p => p.Amount);
            snapshot.TotalExpenses = expenses.Sum(e => e.Amount);
            snapshot.NetProfit = snapshot.TotalRevenue - snapshot.TotalExpenses;
            
            snapshot.AverageCostPerHour = snapshot.TotalUsageHours > 0 
                ? snapshot.TotalExpenses / snapshot.TotalUsageHours 
                : 0;
            
            // Calculate total distance from check-ins (if available)
            decimal totalDistance = 0;
            foreach (var booking in completedBookings)
            {
                var checkIns = await _bookingServiceClient.GetBookingCheckInsAsync(booking.Id);
                var checkOut = checkIns.FirstOrDefault(c => c.Type == CheckInType.CheckOut);
                var checkIn = checkIns.FirstOrDefault(c => c.Type == CheckInType.CheckIn);
                if (checkOut != null && checkIn != null)
                {
                    totalDistance += Math.Max(0, checkOut.Odometer - checkIn.Odometer);
                }
            }
            snapshot.TotalDistance = totalDistance;
            
            snapshot.AverageCostPerKm = snapshot.TotalDistance > 0 
                ? snapshot.TotalExpenses / snapshot.TotalDistance 
                : 0;
            
            // Placeholder values for metrics that would need additional data
            snapshot.MaintenanceEfficiency = 0.85m; // Would need maintenance data
            snapshot.UserSatisfactionScore = 0.90m; // Would need rating/feedback data
            
            _logger.LogInformation("Populated snapshot data for period {Period} from {Start} to {End}: {Bookings} bookings, {Hours} hours", 
                snapshot.Period, periodStart, periodEnd, snapshot.TotalBookings, snapshot.TotalUsageHours);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error populating snapshot data for period {Period}", snapshot.Period);
            // Set defaults on error
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
        }
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
        // Get group from Group service via HTTP
        var group = await _groupServiceClient.GetGroupDetailsAsync(groupId);
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

        // Get all bookings for the group in the period from Booking service via HTTP
        var allBookings = await _bookingServiceClient.GetBookingsAsync(periodStart, periodEnd, groupId);
        var bookings = allBookings.Where(b => 
            b.Status == BookingStatus.Completed &&
            b.EndAt >= periodStart && 
            b.StartAt <= periodEnd).ToList();

        // Get expenses for cost calculation from Payment service via HTTP
        var expenses = await _paymentServiceClient.GetExpensesAsync(groupId, periodStart, periodEnd);

        // Calculate metrics for each member
        var memberMetrics = new List<MemberUsageMetricsDto>();
        
        foreach (var member in group.Members ?? new List<GroupMemberDetailsDto>())
        {
            var memberBookings = bookings.Where(b => b.UserId == member.UserId).ToList();
            
            // Calculate usage by trips
            var totalTrips = bookings.Count;
            var memberTrips = memberBookings.Count;
            var tripsPercentage = totalTrips > 0 ? (decimal)memberTrips / totalTrips * 100 : 0;

            // Calculate usage by distance (from check-ins)
            // Note: CheckIns fetched separately for each booking
            var totalDistanceAll = await CalculateTotalDistanceAsync(bookings);
            var memberDistance = await CalculateMemberDistanceAsync(memberBookings);
            var distancePercentage = totalDistanceAll > 0 ? (decimal)memberDistance / totalDistanceAll * 100 : 0;

            // Calculate usage by time
            var totalHours = bookings.Sum(b => (int)(b.EndAt - b.StartAt).TotalHours);
            var memberHours = memberBookings.Sum(b => (int)(b.EndAt - b.StartAt).TotalHours);
            var hoursPercentage = totalHours > 0 ? (decimal)memberHours / totalHours * 100 : 0;

            // Calculate usage by cost (from expenses)
            var totalCost = expenses.Sum(e => e.Amount);
            // Note: Payments would need Payment service endpoint to get payments by user
            var memberCost = 0m; // Placeholder - would need Payment service endpoint to get member-specific costs
            var costPercentage = 0m; // Placeholder - would need Payment service endpoint

            // Calculate overall usage percentage (average of all metrics)
            var overallUsagePercentage = (tripsPercentage + distancePercentage + hoursPercentage + costPercentage) / 4;
            var ownershipPercentage = member.SharePercentage;
            var usageDifference = overallUsagePercentage - ownershipPercentage;

            var metrics = new MemberUsageMetricsDto
            {
                MemberId = member.UserId,
                MemberName = member.UserName, // Use UserName property instead of UserFirstName/UserLastName
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
        // Get group from Group service via HTTP
        var group = await _groupServiceClient.GetGroupDetailsAsync(groupId);
        if (group == null)
            throw new KeyNotFoundException($"Group with ID {groupId} not found");

        var periodStart = startDate ?? DateTime.UtcNow.AddMonths(-1);
        var periodEnd = endDate ?? DateTime.UtcNow;

        // Get bookings from Booking service via HTTP
        var allBookings = await _bookingServiceClient.GetBookingsAsync(periodStart, periodEnd, groupId);
        var bookings = allBookings.Where(b => 
            b.Status == BookingStatus.Completed &&
            b.EndAt >= periodStart && 
            b.StartAt <= periodEnd).ToList();

        // Get expenses from Payment service via HTTP
        var expenses = await _paymentServiceClient.GetExpensesAsync(groupId, periodStart, periodEnd);

        // Note: Payments would need Payment service endpoint to get payments by date range
        var payments = new List<PaymentDto>(); // Placeholder

        var result = new MemberComparisonDto
        {
            GroupId = groupId,
            GroupName = group.Name,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            GeneratedAt = DateTime.UtcNow
        };

        var totalTrips = bookings.Count;
        var totalDistance = await CalculateTotalDistanceAsync(bookings);
        var totalHours = bookings.Sum(b => (int)(b.EndAt - b.StartAt).TotalHours);
        var totalCost = payments.Sum(p => p.Amount);

        var comparisonItems = new List<MemberComparisonItemDto>();

        foreach (var member in group.Members ?? new List<GroupMemberDetailsDto>())
        {
            var memberBookings = bookings.Where(b => b.UserId == member.UserId).ToList();
            var memberTrips = memberBookings.Count;
            var memberDistance = await CalculateMemberDistanceAsync(memberBookings);
            var memberHours = memberBookings.Sum(b => (int)(b.EndAt - b.StartAt).TotalHours);
            // Note: Payments would need Payment service endpoint
            var memberCost = 0m; // Placeholder

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
                MemberName = member.UserName,
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

    private async Task<decimal> CalculateMemberDistanceAsync(List<BookingDto> memberBookings)
    {
        decimal totalDistance = 0;
        
        foreach (var booking in memberBookings)
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
    
    // Synchronous version for backward compatibility (returns 0 if check-ins not available)
    private decimal CalculateTotalDistance(List<BookingDto> bookings)
    {
        // Note: This is a simplified version that doesn't fetch check-ins
        // For accurate distance, use CalculateTotalDistanceAsync
        return 0;
    }

    private decimal CalculateMemberDistance(List<BookingDto> memberBookings)
    {
        // Note: This is a simplified version that doesn't fetch check-ins
        // For accurate distance, use CalculateMemberDistanceAsync
        return 0;
    }

    public async Task<VisualizationDataDto> GetVisualizationDataAsync(Guid groupId, DateTime? startDate = null, DateTime? endDate = null)
    {
        // Get group from Group service via HTTP
        var group = await _groupServiceClient.GetGroupDetailsAsync(groupId);
        if (group == null)
            throw new KeyNotFoundException($"Group with ID {groupId} not found");

        var periodStart = startDate ?? DateTime.UtcNow.AddMonths(-1);
        var periodEnd = endDate ?? DateTime.UtcNow;

        // Get bookings from Booking service via HTTP
        var allBookings = await _bookingServiceClient.GetBookingsAsync(periodStart, periodEnd, groupId);
        var bookings = allBookings.Where(b => 
            b.Status == BookingStatus.Completed &&
            b.EndAt >= periodStart && 
            b.StartAt <= periodEnd).ToList();

        var result = new VisualizationDataDto
        {
            GroupId = groupId,
            GroupName = group.Name
        };

        // Pie chart data - usage distribution by member (by trips)
        var totalTrips = bookings.Count;
        foreach (var member in group.Members ?? new List<GroupMemberDetailsDto>())
        {
            var memberTrips = bookings.Count(b => b.UserId == member.UserId);
            var percentage = totalTrips > 0 ? (decimal)memberTrips / totalTrips * 100 : 0;
            result.UsageDistributionByMember.Add(new ChartDataPointDto
            {
                Label = member.UserName,
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
        // Get group from Group service via HTTP
        var group = await _groupServiceClient.GetGroupDetailsAsync(groupId);
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
