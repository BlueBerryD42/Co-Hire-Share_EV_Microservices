using Microsoft.EntityFrameworkCore;
using CoOwnershipVehicle.Data;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Shared.Contracts.DTOs;

namespace CoOwnershipVehicle.Analytics.Api.Services;

public class AnalyticsService : IAnalyticsService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AnalyticsService> _logger;

    public AnalyticsService(ApplicationDbContext context, ILogger<AnalyticsService> logger)
    {
        _context = context;
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

        // Overall Statistics
        dashboard.TotalGroups = await _context.OwnershipGroups.CountAsync();
        dashboard.TotalVehicles = await _context.Vehicles.CountAsync();
        dashboard.TotalUsers = await _context.Users.CountAsync();
        dashboard.TotalBookings = await _context.Bookings
            .Where(b => b.CreatedAt >= periodStart && b.CreatedAt <= periodEnd)
            .CountAsync();

        // Financial Overview
        dashboard.TotalRevenue = await _context.Payments
            .Where(p => p.Status == PaymentStatus.Completed && 
                       p.CreatedAt >= periodStart && p.CreatedAt <= periodEnd)
            .SumAsync(p => p.Amount);

        dashboard.TotalExpenses = await _context.Expenses
            .Where(e => e.CreatedAt >= periodStart && e.CreatedAt <= periodEnd)
            .SumAsync(e => e.Amount);

        dashboard.NetProfit = dashboard.TotalRevenue - dashboard.TotalExpenses;

        // Efficiency Metrics
        var totalBookingHours = await _context.Bookings
            .Where(b => b.Status == BookingStatus.Completed && 
                       b.CreatedAt >= periodStart && b.CreatedAt <= periodEnd)
            .SumAsync(b => EF.Functions.DateDiffHour(b.StartAt, b.EndAt));

        var totalAvailableHours = (decimal)(periodEnd - periodStart).TotalHours * dashboard.TotalVehicles;
        dashboard.AverageUtilizationRate = totalAvailableHours > 0 ? 
            (decimal)totalBookingHours / totalAvailableHours : 0;

        // Top Performers
        dashboard.TopVehicles = await GetTopVehiclesAsync(request, 5);
        dashboard.TopUsers = await GetTopUsersAsync(request, 5);
        dashboard.TopGroups = await GetTopGroupsAsync(request, 5);

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
        var periodStart = request.StartDate ?? DateTime.UtcNow.AddMonths(-1);
        var periodEnd = request.EndDate ?? DateTime.UtcNow;

        var query = from u in _context.Users
                   join gm in _context.GroupMembers on u.Id equals gm.UserId into groupMembers
                   from gm in groupMembers.DefaultIfEmpty()
                   join g in _context.OwnershipGroups on gm.GroupId equals g.Id into groups
                   from g in groups.DefaultIfEmpty()
                   where (request.UserId == null || u.Id == request.UserId)
                   && (request.GroupId == null || g.Id == request.GroupId)
                   select new { u, gm, g };

        var userData = await query.ToListAsync();
        var userAnalytics = new List<UserAnalyticsDto>();

        foreach (var user in userData)
        {
            var analytics = await CalculateUserAnalytics(user.u.Id, user.g?.Id, periodStart, periodEnd);
            if (analytics != null)
            {
                analytics.UserName = user.u.FirstName + " " + user.u.LastName;
                analytics.GroupName = user.g?.Name ?? "";
                userAnalytics.Add(analytics);
            }
        }

        return userAnalytics;
    }

    public async Task<List<VehicleAnalyticsDto>> GetVehicleAnalyticsAsync(AnalyticsRequestDto request)
    {
        var periodStart = request.StartDate ?? DateTime.UtcNow.AddMonths(-1);
        var periodEnd = request.EndDate ?? DateTime.UtcNow;

        var query = _context.Vehicles
            .Include(v => v.Group)
            .Where(v => request.VehicleId == null || v.Id == request.VehicleId)
            .Where(v => request.GroupId == null || v.GroupId == request.GroupId);

        var vehicles = await query.ToListAsync();
        var vehicleAnalytics = new List<VehicleAnalyticsDto>();

        foreach (var vehicle in vehicles)
        {
            var analytics = await CalculateVehicleAnalytics(vehicle.Id, vehicle.GroupId, periodStart, periodEnd);
            if (analytics != null)
            {
                analytics.VehicleModel = vehicle.Model;
                analytics.PlateNumber = vehicle.PlateNumber;
                analytics.GroupName = vehicle.Group?.Name ?? "";
                vehicleAnalytics.Add(analytics);
            }
        }

        return vehicleAnalytics;
    }

    public async Task<List<GroupAnalyticsDto>> GetGroupAnalyticsAsync(AnalyticsRequestDto request)
    {
        var periodStart = request.StartDate ?? DateTime.UtcNow.AddMonths(-1);
        var periodEnd = request.EndDate ?? DateTime.UtcNow;

        var query = _context.OwnershipGroups
            .Where(g => request.GroupId == null || g.Id == request.GroupId);

        var groups = await query.ToListAsync();
        var groupAnalytics = new List<GroupAnalyticsDto>();

        foreach (var group in groups)
        {
            var analytics = await CalculateGroupAnalytics(group.Id, periodStart, periodEnd);
            if (analytics != null)
            {
                analytics.GroupName = group.Name;
                groupAnalytics.Add(analytics);
            }
        }

        return groupAnalytics;
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
        var periodStart = request.StartDate ?? DateTime.UtcNow.AddMonths(-1);
        var periodEnd = request.EndDate ?? DateTime.UtcNow;

        var metrics = new Dictionary<string, object>();

        // Financial KPIs
        var totalRevenue = await _context.Payments
            .Where(p => p.Status == PaymentStatus.Completed && 
                       p.CreatedAt >= periodStart && p.CreatedAt <= periodEnd)
            .SumAsync(p => p.Amount);

        var totalExpenses = await _context.Expenses
            .Where(e => e.CreatedAt >= periodStart && e.CreatedAt <= periodEnd)
            .SumAsync(e => e.Amount);

        metrics["TotalRevenue"] = totalRevenue;
        metrics["TotalExpenses"] = totalExpenses;
        metrics["NetProfit"] = totalRevenue - totalExpenses;
        metrics["ProfitMargin"] = totalRevenue > 0 ? (totalRevenue - totalExpenses) / totalRevenue : 0;

        // Operational KPIs
        var totalBookings = await _context.Bookings
            .Where(b => b.CreatedAt >= periodStart && b.CreatedAt <= periodEnd)
            .CountAsync();

        var completedBookings = await _context.Bookings
            .Where(b => b.Status == BookingStatus.Completed && 
                       b.CreatedAt >= periodStart && b.CreatedAt <= periodEnd)
            .CountAsync();

        metrics["TotalBookings"] = totalBookings;
        metrics["CompletedBookings"] = completedBookings;
        metrics["BookingCompletionRate"] = totalBookings > 0 ? (decimal)completedBookings / totalBookings : 0;

        // Utilization KPI
        var totalBookingHours = await _context.Bookings
            .Where(b => b.Status == BookingStatus.Completed && 
                       b.CreatedAt >= periodStart && b.CreatedAt <= periodEnd)
            .SumAsync(b => EF.Functions.DateDiffHour(b.StartAt, b.EndAt));

        var totalAvailableHours = (decimal)(periodEnd - periodStart).TotalHours * await _context.Vehicles.CountAsync();
        metrics["UtilizationRate"] = totalAvailableHours > 0 ? (decimal)totalBookingHours / totalAvailableHours : 0;

        return metrics;
    }

    public async Task<List<Dictionary<string, object>>> GetTrendDataAsync(AnalyticsRequestDto request)
    {
        var periodStart = request.StartDate ?? DateTime.UtcNow.AddDays(-30);
        var periodEnd = request.EndDate ?? DateTime.UtcNow;

        var trends = new List<Dictionary<string, object>>();

        // Daily trend data
        var dailyData = await _context.Bookings
            .Where(b => b.CreatedAt >= periodStart && b.CreatedAt <= periodEnd)
            .GroupBy(b => b.CreatedAt.Date)
            .Select(g => new
            {
                Date = g.Key,
                Bookings = g.Count(),
                Revenue = g.Sum(b => 0) // Placeholder - would need payment relationship
            })
            .OrderBy(x => x.Date)
            .ToListAsync();

        foreach (var day in dailyData)
        {
            trends.Add(new Dictionary<string, object>
            {
                ["Date"] = day.Date,
                ["Bookings"] = day.Bookings,
                ["Revenue"] = day.Revenue
            });
        }

        return trends;
    }

    private async Task<List<VehicleAnalyticsDto>> GetTopVehiclesAsync(AnalyticsRequestDto request, int limit)
    {
        var periodStart = request.StartDate ?? DateTime.UtcNow.AddMonths(-1);
        var periodEnd = request.EndDate ?? DateTime.UtcNow;

        var topVehicles = await _context.Vehicles
            .Include(v => v.Group)
            .Select(v => new
            {
                Vehicle = v,
                Bookings = _context.Bookings.Count(b => b.VehicleId == v.Id && 
                                                      b.Status == BookingStatus.Completed &&
                                                      b.CreatedAt >= periodStart && 
                                                      b.CreatedAt <= periodEnd),
                UsageHours = _context.Bookings
                    .Where(b => b.VehicleId == v.Id && 
                               b.Status == BookingStatus.Completed &&
                               b.CreatedAt >= periodStart && 
                               b.CreatedAt <= periodEnd)
                    .Sum(b => EF.Functions.DateDiffHour(b.StartAt, b.EndAt))
            })
            .OrderByDescending(x => x.UsageHours)
            .Take(limit)
            .ToListAsync();

        return topVehicles.Select(x => new VehicleAnalyticsDto
        {
            VehicleId = x.Vehicle.Id,
            VehicleModel = x.Vehicle.Model,
            PlateNumber = x.Vehicle.PlateNumber,
            GroupName = x.Vehicle.Group?.Name ?? "",
            TotalBookings = x.Bookings,
            TotalUsageHours = (int)x.UsageHours,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            Period = request.Period
        }).ToList();
    }

    private async Task<List<UserAnalyticsDto>> GetTopUsersAsync(AnalyticsRequestDto request, int limit)
    {
        var periodStart = request.StartDate ?? DateTime.UtcNow.AddMonths(-1);
        var periodEnd = request.EndDate ?? DateTime.UtcNow;

        var topUsers = await _context.Users
            .Select(u => new
            {
                User = u,
                Bookings = _context.Bookings.Count(b => b.UserId == u.Id && 
                                                      b.Status == BookingStatus.Completed &&
                                                      b.CreatedAt >= periodStart && 
                                                      b.CreatedAt <= periodEnd),
                UsageHours = _context.Bookings
                    .Where(b => b.UserId == u.Id && 
                               b.Status == BookingStatus.Completed &&
                               b.CreatedAt >= periodStart && 
                               b.CreatedAt <= periodEnd)
                    .Sum(b => EF.Functions.DateDiffHour(b.StartAt, b.EndAt))
            })
            .OrderByDescending(x => x.UsageHours)
            .Take(limit)
            .ToListAsync();

        return topUsers.Select(x => new UserAnalyticsDto
        {
            UserId = x.User.Id,
            UserName = x.User.FirstName + " " + x.User.LastName,
            TotalBookings = x.Bookings,
            TotalUsageHours = (int)x.UsageHours,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            Period = request.Period
        }).ToList();
    }

    private async Task<List<GroupAnalyticsDto>> GetTopGroupsAsync(AnalyticsRequestDto request, int limit)
    {
        var periodStart = request.StartDate ?? DateTime.UtcNow.AddMonths(-1);
        var periodEnd = request.EndDate ?? DateTime.UtcNow;

        var topGroups = await _context.OwnershipGroups
            .Select(g => new
            {
                Group = g,
                Bookings = _context.Bookings.Count(b => b.GroupId == g.Id && 
                                                      b.Status == BookingStatus.Completed &&
                                                      b.CreatedAt >= periodStart && 
                                                      b.CreatedAt <= periodEnd),
                Members = _context.GroupMembers.Count(gm => gm.GroupId == g.Id)
            })
            .OrderByDescending(x => x.Bookings)
            .Take(limit)
            .ToListAsync();

        return topGroups.Select(x => new GroupAnalyticsDto
        {
            GroupId = x.Group.Id,
            GroupName = x.Group.Name,
            TotalBookings = x.Bookings,
            TotalMembers = x.Members,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            Period = request.Period
        }).ToList();
    }

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

    private async Task<UserAnalyticsDto> CalculateUserAnalytics(Guid userId, Guid? groupId, DateTime periodStart, DateTime periodEnd)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return null;

        var bookings = await _context.Bookings
            .Where(b => b.UserId == userId && 
                       (groupId == null || b.GroupId == groupId) &&
                       b.CreatedAt >= periodStart && 
                       b.CreatedAt <= periodEnd)
            .ToListAsync();

        var totalUsageHours = bookings
            .Where(b => b.Status == BookingStatus.Completed)
            .Sum(b => (b.EndAt - b.StartAt).TotalHours);

        var totalDistance = 0m; // Would need to calculate from check-ins

        var ownershipShare = await _context.GroupMembers
            .Where(gm => gm.UserId == userId && (groupId == null || gm.GroupId == groupId))
            .SumAsync(gm => gm.SharePercentage);

        var usageShare = bookings.Count > 0 ? 
            (decimal)bookings.Count / await _context.Bookings.CountAsync(b => b.CreatedAt >= periodStart && b.CreatedAt <= periodEnd) : 0;

        var totalPaid = await _context.Payments
            .Where(p => p.UserId == userId && 
                       p.Status == PaymentStatus.Completed &&
                       p.CreatedAt >= periodStart && 
                       p.CreatedAt <= periodEnd)
            .SumAsync(p => p.Amount);

        var totalOwed = await _context.Invoices
            .Where(i => i.UserId == userId && 
                       i.Status == InvoiceStatus.Pending &&
                       i.CreatedAt >= periodStart && 
                       i.CreatedAt <= periodEnd)
            .SumAsync(i => i.TotalAmount);

        return new UserAnalyticsDto
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            GroupId = groupId,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            Period = "Custom",
            TotalBookings = bookings.Count,
            TotalUsageHours = (int)totalUsageHours,
            TotalDistance = totalDistance,
            OwnershipShare = ownershipShare,
            UsageShare = usageShare,
            TotalPaid = totalPaid,
            TotalOwed = totalOwed,
            NetBalance = totalPaid - totalOwed,
            BookingSuccessRate = bookings.Count > 0 ? (decimal)bookings.Count(b => b.Status == BookingStatus.Completed) / bookings.Count : 0,
            Cancellations = bookings.Count(b => b.Status == BookingStatus.Cancelled),
            NoShows = bookings.Count(b => b.Status == BookingStatus.NoShow),
            PunctualityScore = 0.85m // Placeholder - would need actual calculation
        };
    }

    private async Task<VehicleAnalyticsDto> CalculateVehicleAnalytics(Guid vehicleId, Guid? groupId, DateTime periodStart, DateTime periodEnd)
    {
        var vehicle = await _context.Vehicles.FindAsync(vehicleId);
        if (vehicle == null) return null;

        var bookings = await _context.Bookings
            .Where(b => b.VehicleId == vehicleId && 
                       b.CreatedAt >= periodStart && 
                       b.CreatedAt <= periodEnd)
            .ToListAsync();

        var totalUsageHours = bookings
            .Where(b => b.Status == BookingStatus.Completed)
            .Sum(b => (b.EndAt - b.StartAt).TotalHours);

        var totalAvailableHours = (periodEnd - periodStart).TotalHours;
        var utilizationRate = totalAvailableHours > 0 ? (decimal)totalUsageHours / (decimal)totalAvailableHours : 0;

        var revenue = await _context.Payments
            .Where(p => p.VehicleId == vehicleId && 
                       p.Status == PaymentStatus.Completed &&
                       p.CreatedAt >= periodStart && 
                       p.CreatedAt <= periodEnd)
            .SumAsync(p => p.Amount);

        var maintenanceCost = await _context.Expenses
            .Where(e => e.VehicleId == vehicleId && 
                       e.Type == ExpenseType.Maintenance &&
                       e.CreatedAt >= periodStart && 
                       e.CreatedAt <= periodEnd)
            .SumAsync(e => e.Amount);

        var operatingCost = await _context.Expenses
            .Where(e => e.VehicleId == vehicleId && 
                       e.CreatedAt >= periodStart && 
                       e.CreatedAt <= periodEnd)
            .SumAsync(e => e.Amount);

        return new VehicleAnalyticsDto
        {
            Id = Guid.NewGuid(),
            VehicleId = vehicleId,
            GroupId = groupId,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            Period = "Custom",
            TotalBookings = bookings.Count,
            TotalUsageHours = (int)totalUsageHours,
            UtilizationRate = utilizationRate,
            AvailabilityRate = 1 - utilizationRate,
            Revenue = revenue,
            MaintenanceCost = maintenanceCost,
            OperatingCost = operatingCost,
            NetProfit = revenue - operatingCost,
            CostPerKm = 0, // Would need distance calculation
            CostPerHour = totalUsageHours > 0 ? (decimal)operatingCost / (decimal)totalUsageHours : 0,
            MaintenanceEvents = await _context.Expenses.CountAsync(e => e.VehicleId == vehicleId && e.Type == ExpenseType.Maintenance),
            Breakdowns = 0, // Placeholder
            ReliabilityScore = 0.95m // Placeholder
        };
    }

    private async Task<GroupAnalyticsDto> CalculateGroupAnalytics(Guid groupId, DateTime periodStart, DateTime periodEnd)
    {
        var group = await _context.OwnershipGroups.FindAsync(groupId);
        if (group == null) return null;

        var totalMembers = await _context.GroupMembers.CountAsync(gm => gm.GroupId == groupId);
        var activeMembers = await _context.GroupMembers
            .Where(gm => gm.GroupId == groupId)
            .CountAsync();

        var totalBookings = await _context.Bookings
            .Where(b => b.GroupId == groupId && 
                       b.CreatedAt >= periodStart && 
                       b.CreatedAt <= periodEnd)
            .CountAsync();

        var totalRevenue = await _context.Payments
            .Where(p => p.GroupId == groupId && 
                       p.Status == PaymentStatus.Completed &&
                       p.CreatedAt >= periodStart && 
                       p.CreatedAt <= periodEnd)
            .SumAsync(p => p.Amount);

        var totalExpenses = await _context.Expenses
            .Where(e => e.GroupId == groupId && 
                       e.CreatedAt >= periodStart && 
                       e.CreatedAt <= periodEnd)
            .SumAsync(e => e.Amount);

        var totalProposals = await _context.Proposals
            .Where(p => p.GroupId == groupId && 
                       p.CreatedAt >= periodStart && 
                       p.CreatedAt <= periodEnd)
            .CountAsync();

        var totalVotes = await _context.Votes
            .Where(v => v.Proposal.GroupId == groupId && 
                       v.CreatedAt >= periodStart && 
                       v.CreatedAt <= periodEnd)
            .CountAsync();

        return new GroupAnalyticsDto
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            Period = "Custom",
            TotalMembers = totalMembers,
            ActiveMembers = activeMembers,
            NewMembers = 0, // Would need to calculate
            LeftMembers = 0, // Would need to calculate
            TotalRevenue = totalRevenue,
            TotalExpenses = totalExpenses,
            NetProfit = totalRevenue - totalExpenses,
            AverageMemberContribution = totalMembers > 0 ? totalExpenses / totalMembers : 0,
            TotalBookings = totalBookings,
            TotalProposals = totalProposals,
            TotalVotes = totalVotes,
            ParticipationRate = totalMembers > 0 ? (decimal)totalVotes / totalMembers : 0
        };
    }

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

        var query = _context.Bookings.AsQueryable();
        
        if (snapshot.GroupId.HasValue)
            query = query.Where(b => b.GroupId == snapshot.GroupId);
            
        if (snapshot.VehicleId.HasValue)
            query = query.Where(b => b.VehicleId == snapshot.VehicleId);

        var bookings = await query
            .Where(b => b.CreatedAt >= periodStart && b.CreatedAt <= periodEnd)
            .ToListAsync();

        snapshot.TotalBookings = bookings.Count;
        snapshot.TotalUsageHours = bookings
            .Where(b => b.Status == BookingStatus.Completed)
            .Sum(b => (int)(b.EndAt - b.StartAt).TotalHours);

        snapshot.ActiveUsers = bookings.Select(b => b.UserId).Distinct().Count();

        // Calculate utilization rate
        var totalVehicles = snapshot.VehicleId.HasValue ? 1 : 
            await _context.Vehicles.CountAsync(v => snapshot.GroupId == null || v.GroupId == snapshot.GroupId);
        
        var totalAvailableHours = (periodEnd - periodStart).TotalHours * totalVehicles;
        snapshot.UtilizationRate = totalAvailableHours > 0 ? 
            (decimal)snapshot.TotalUsageHours / (decimal)totalAvailableHours : 0;

        // Financial data
        var paymentsQuery = _context.Payments.Where(p => p.CreatedAt >= periodStart && p.CreatedAt <= periodEnd);
        var expensesQuery = _context.Expenses.Where(e => e.CreatedAt >= periodStart && e.CreatedAt <= periodEnd);

        if (snapshot.GroupId.HasValue)
        {
            paymentsQuery = paymentsQuery.Where(p => p.GroupId == snapshot.GroupId);
            expensesQuery = expensesQuery.Where(e => e.GroupId == snapshot.GroupId);
        }

        if (snapshot.VehicleId.HasValue)
        {
            paymentsQuery = paymentsQuery.Where(p => p.VehicleId == snapshot.VehicleId);
            expensesQuery = expensesQuery.Where(e => e.VehicleId == snapshot.VehicleId);
        }

        snapshot.TotalRevenue = await paymentsQuery
            .Where(p => p.Status == PaymentStatus.Completed)
            .SumAsync(p => p.Amount);

        snapshot.TotalExpenses = await expensesQuery.SumAsync(e => e.Amount);
        snapshot.NetProfit = snapshot.TotalRevenue - snapshot.TotalExpenses;

        snapshot.AverageCostPerHour = snapshot.TotalUsageHours > 0 ? 
            snapshot.TotalExpenses / snapshot.TotalUsageHours : 0;

        // Placeholder calculations for remaining fields
        snapshot.TotalDistance = 0;
        snapshot.AverageCostPerKm = 0;
        snapshot.MaintenanceEfficiency = 0.85m;
        snapshot.UserSatisfactionScore = 0.90m;
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
}
