using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using CoOwnershipVehicle.Admin.Api.Data;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Admin.Api.Services;

public class AdminService : IAdminService
{
    private readonly AdminDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AdminService> _logger;
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(5);

    public AdminService(AdminDbContext context, IMemoryCache cache, ILogger<AdminService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    public async Task<DashboardMetricsDto> GetDashboardMetricsAsync(DashboardRequestDto request)
    {
        var cacheKey = await GenerateDashboardCacheKeyAsync(request);

        if (_cache.TryGetValue(cacheKey, out DashboardMetricsDto? cachedMetrics))
        {
            return cachedMetrics!;
        }

        var metrics = new DashboardMetricsDto();

        try
        {
            // Get user metrics
            var userMetrics = await GetUserMetricsAsync(request.Period);
            metrics.Users = userMetrics;
            
            // Get group metrics
            var groupMetrics = await GetGroupMetricsAsync(request.Period);
            metrics.Groups = groupMetrics;
            
            // Get vehicle metrics
            var vehicleMetrics = await GetVehicleMetricsAsync(request.Period);
            metrics.Vehicles = vehicleMetrics;
            
            // Get booking metrics
            metrics.Bookings = await GetBookingMetricsAsync(request.Period);
            
            // Get revenue metrics
            metrics.Revenue = await GetRevenueMetricsAsync(
                request.Period,
                userMetrics.TotalUsers,
                groupMetrics.TotalGroups,
                vehicleMetrics.TotalVehicles);
            
            // Get system health
            metrics.SystemHealth = await GetSystemHealthAsync();
            
            // Get recent activity
            metrics.RecentActivity = await GetRecentActivityAsync(20);
            
            // Get alerts if requested
            if (request.IncludeAlerts)
            {
                metrics.Alerts = await GetAlertsAsync();
            }

            // Cache the results
            _cache.Set(cacheKey, metrics, _cacheExpiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating dashboard metrics");
            throw;
        }

        return metrics;
    }

    private async Task<string> GenerateDashboardCacheKeyAsync(DashboardRequestDto request)
    {
        var baseKey = $"dashboard_metrics_{request.Period}_{request.IncludeGrowthMetrics}_{request.IncludeAlerts}";

        var userCount = await _context.Users.CountAsync();
        var groupCount = await _context.OwnershipGroups.CountAsync();
        var vehicleCount = await _context.Vehicles.CountAsync();
        var bookingCount = await _context.Bookings.CountAsync();
        var paymentCount = await _context.Payments.CountAsync();
        var expenseCount = await _context.Expenses.CountAsync();

        var latestUserUpdate = await _context.Users
            .OrderByDescending(u => u.UpdatedAt)
            .Select(u => (DateTime?)u.UpdatedAt)
            .FirstOrDefaultAsync() ?? DateTime.MinValue;

        var latestGroupUpdate = await _context.OwnershipGroups
            .OrderByDescending(g => g.UpdatedAt)
            .Select(g => (DateTime?)g.UpdatedAt)
            .FirstOrDefaultAsync() ?? DateTime.MinValue;

        var latestVehicleUpdate = await _context.Vehicles
            .OrderByDescending(v => v.UpdatedAt)
            .Select(v => (DateTime?)v.UpdatedAt)
            .FirstOrDefaultAsync() ?? DateTime.MinValue;

        var latestBookingUpdate = await _context.Bookings
            .OrderByDescending(b => b.UpdatedAt)
            .Select(b => (DateTime?)b.UpdatedAt)
            .FirstOrDefaultAsync() ?? DateTime.MinValue;

        var latestPaymentUpdate = await _context.Payments
            .OrderByDescending(p => p.UpdatedAt)
            .Select(p => (DateTime?)p.UpdatedAt)
            .FirstOrDefaultAsync() ?? DateTime.MinValue;

        var latestExpenseUpdate = await _context.Expenses
            .OrderByDescending(e => e.UpdatedAt)
            .Select(e => (DateTime?)e.UpdatedAt)
            .FirstOrDefaultAsync() ?? DateTime.MinValue;

        return string.Join('_',
            baseKey,
            userCount,
            groupCount,
            vehicleCount,
            bookingCount,
            paymentCount,
            expenseCount,
            latestUserUpdate.Ticks,
            latestGroupUpdate.Ticks,
            latestVehicleUpdate.Ticks,
            latestBookingUpdate.Ticks,
            latestPaymentUpdate.Ticks,
            latestExpenseUpdate.Ticks);
    }

    private async Task<UserMetricsDto> GetUserMetricsAsync(TimePeriod period)
    {
        var now = DateTime.UtcNow;
        var periodStart = GetPeriodStart(now, period);
        var previousPeriodStart = GetPeriodStart(periodStart.AddDays(-1), period);

        var userCounts = await GetUserCountsAsync();
        var periodCounts = await GetPeriodUserCountsAsync(periodStart, previousPeriodStart, now);
        var growthPercentage = CalculateGrowthPercentage(periodCounts.Previous, periodCounts.Current);

        return new UserMetricsDto
        {
            TotalUsers = userCounts.Total,
            ActiveUsers = userCounts.Active,
            InactiveUsers = userCounts.Inactive,
            PendingKyc = userCounts.PendingKyc,
            ApprovedKyc = userCounts.ApprovedKyc,
            RejectedKyc = userCounts.RejectedKyc,
            UserGrowthPercentage = growthPercentage,
            NewUsersThisMonth = periodCounts.ThisMonth,
            NewUsersThisWeek = periodCounts.ThisWeek
        };
    }

    private async Task<(int Total, int Active, int Inactive, int PendingKyc, int ApprovedKyc, int RejectedKyc)> GetUserCountsAsync()
    {
        var now = DateTime.UtcNow;
        
        var totalUsers = await _context.Users.CountAsync();
        var activeUsers = await _context.Users.CountAsync(u => u.LockoutEnd == null || u.LockoutEnd < now);
        var inactiveUsers = await _context.Users.CountAsync(u => u.LockoutEnd != null && u.LockoutEnd > now);
        var pendingKyc = await _context.Users.CountAsync(u => u.KycStatus == KycStatus.Pending);
        var approvedKyc = await _context.Users.CountAsync(u => u.KycStatus == KycStatus.Approved);
        var rejectedKyc = await _context.Users.CountAsync(u => u.KycStatus == KycStatus.Rejected);

        return (totalUsers, activeUsers, inactiveUsers, pendingKyc, approvedKyc, rejectedKyc);
    }

    private async Task<(int Current, int Previous, int ThisMonth, int ThisWeek)> GetPeriodUserCountsAsync(DateTime periodStart, DateTime previousPeriodStart, DateTime now)
    {
        var newUsersThisPeriod = await _context.Users.CountAsync(u => u.CreatedAt >= periodStart);
        var newUsersPreviousPeriod = await _context.Users.CountAsync(u => u.CreatedAt >= previousPeriodStart && u.CreatedAt < periodStart);
        var newUsersThisMonth = await _context.Users.CountAsync(u => u.CreatedAt >= now.AddDays(-30));
        var newUsersThisWeek = await _context.Users.CountAsync(u => u.CreatedAt >= now.AddDays(-7));

        return (newUsersThisPeriod, newUsersPreviousPeriod, newUsersThisMonth, newUsersThisWeek);
    }

    private async Task<GroupMetricsDto> GetGroupMetricsAsync(TimePeriod period)
    {
        var now = DateTime.UtcNow;
        var periodStart = GetPeriodStart(now, period);
        var previousPeriodStart = GetPeriodStart(periodStart.AddDays(-1), period);

        var totalGroups = await _context.OwnershipGroups.CountAsync();
        var activeGroups = await _context.OwnershipGroups.CountAsync(g => g.Status == GroupStatus.Active);
        var inactiveGroups = await _context.OwnershipGroups.CountAsync(g => g.Status == GroupStatus.Inactive);
        var dissolvedGroups = await _context.OwnershipGroups.CountAsync(g => g.Status == GroupStatus.Dissolved);

        var newGroupsThisPeriod = await _context.OwnershipGroups.CountAsync(g => g.CreatedAt >= periodStart);
        var newGroupsPreviousPeriod = await _context.OwnershipGroups.CountAsync(g => g.CreatedAt >= previousPeriodStart && g.CreatedAt < periodStart);

        var growthPercentage = CalculateGrowthPercentage(newGroupsPreviousPeriod, newGroupsThisPeriod);

        return new GroupMetricsDto
        {
            TotalGroups = totalGroups,
            ActiveGroups = activeGroups,
            InactiveGroups = inactiveGroups,
            DissolvedGroups = dissolvedGroups,
            GroupGrowthPercentage = growthPercentage,
            NewGroupsThisMonth = await _context.OwnershipGroups.CountAsync(g => g.CreatedAt >= now.AddDays(-30)),
            NewGroupsThisWeek = await _context.OwnershipGroups.CountAsync(g => g.CreatedAt >= now.AddDays(-7))
        };
    }

    private async Task<VehicleMetricsDto> GetVehicleMetricsAsync(TimePeriod period)
    {
        var now = DateTime.UtcNow;
        var periodStart = GetPeriodStart(now, period);
        var previousPeriodStart = GetPeriodStart(periodStart.AddDays(-1), period);

        var totalVehicles = await _context.Vehicles.CountAsync();
        var availableVehicles = await _context.Vehicles.CountAsync(v => v.Status == VehicleStatus.Available);
        var inUseVehicles = await _context.Vehicles.CountAsync(v => v.Status == VehicleStatus.InUse);
        var maintenanceVehicles = await _context.Vehicles.CountAsync(v => v.Status == VehicleStatus.Maintenance);
        var unavailableVehicles = await _context.Vehicles.CountAsync(v => v.Status == VehicleStatus.Unavailable);

        var newVehiclesThisPeriod = await _context.Vehicles.CountAsync(v => v.CreatedAt >= periodStart);
        var newVehiclesPreviousPeriod = await _context.Vehicles.CountAsync(v => v.CreatedAt >= previousPeriodStart && v.CreatedAt < periodStart);

        var growthPercentage = CalculateGrowthPercentage(newVehiclesPreviousPeriod, newVehiclesThisPeriod);

        return new VehicleMetricsDto
        {
            TotalVehicles = totalVehicles,
            AvailableVehicles = availableVehicles,
            InUseVehicles = inUseVehicles,
            MaintenanceVehicles = maintenanceVehicles,
            UnavailableVehicles = unavailableVehicles,
            VehicleGrowthPercentage = growthPercentage,
            NewVehiclesThisMonth = await _context.Vehicles.CountAsync(v => v.CreatedAt >= now.AddDays(-30)),
            NewVehiclesThisWeek = await _context.Vehicles.CountAsync(v => v.CreatedAt >= now.AddDays(-7))
        };
    }

    private async Task<BookingMetricsDto> GetBookingMetricsAsync(TimePeriod period)
    {
        var now = DateTime.UtcNow;
        var periodStart = GetPeriodStart(now, period);
        var previousPeriodStart = GetPeriodStart(periodStart.AddDays(-1), period);

        var totalBookings = await _context.Bookings.CountAsync();
        var pendingBookings = await _context.Bookings.CountAsync(b => b.Status == BookingStatus.Pending);
        var confirmedBookings = await _context.Bookings.CountAsync(b => b.Status == BookingStatus.Confirmed);
        var inProgressBookings = await _context.Bookings.CountAsync(b => b.Status == BookingStatus.InProgress);
        var completedBookings = await _context.Bookings.CountAsync(b => b.Status == BookingStatus.Completed);
        var cancelledBookings = await _context.Bookings.CountAsync(b => b.Status == BookingStatus.Cancelled);
        var activeTrips = await _context.Bookings.CountAsync(b => b.Status == BookingStatus.InProgress);

        var newBookingsThisPeriod = await _context.Bookings.CountAsync(b => b.CreatedAt >= periodStart);
        var newBookingsPreviousPeriod = await _context.Bookings.CountAsync(b => b.CreatedAt >= previousPeriodStart && b.CreatedAt < periodStart);

        var growthPercentage = CalculateGrowthPercentage(newBookingsPreviousPeriod, newBookingsThisPeriod);

        return new BookingMetricsDto
        {
            TotalBookings = totalBookings,
            PendingBookings = pendingBookings,
            ConfirmedBookings = confirmedBookings,
            InProgressBookings = inProgressBookings,
            CompletedBookings = completedBookings,
            CancelledBookings = cancelledBookings,
            ActiveTrips = activeTrips,
            BookingGrowthPercentage = growthPercentage,
            NewBookingsThisMonth = await _context.Bookings.CountAsync(b => b.CreatedAt >= now.AddDays(-30)),
            NewBookingsThisWeek = await _context.Bookings.CountAsync(b => b.CreatedAt >= now.AddDays(-7))
        };
    }

    private async Task<RevenueMetricsDto> GetRevenueMetricsAsync(TimePeriod period, int totalUsers, int totalGroups, int totalVehicles)
    {
        var now = DateTime.UtcNow;
        var periodStart = GetPeriodStart(now, period);
        var previousPeriodStart = GetPeriodStart(periodStart.AddDays(-1), period);

        var revenueData = await GetRevenueDataAsync(now, periodStart, previousPeriodStart);
        var growthPercentage = CalculateGrowthPercentage((double)revenueData.PreviousPeriod, (double)revenueData.ThisPeriod);

        return new RevenueMetricsDto
        {
            TotalRevenue = revenueData.Total,
            MonthlyRevenue = revenueData.Monthly,
            WeeklyRevenue = revenueData.Weekly,
            DailyRevenue = revenueData.Daily,
            RevenueGrowthPercentage = growthPercentage,
            AverageRevenuePerUser = totalUsers > 0 ? revenueData.Total / totalUsers : 0,
            AverageRevenuePerGroup = totalGroups > 0 ? revenueData.Total / totalGroups : 0,
            AverageRevenuePerVehicle = totalVehicles > 0 ? revenueData.Total / totalVehicles : 0
        };
    }

    private async Task<(decimal Total, decimal Monthly, decimal Weekly, decimal Daily, decimal ThisPeriod, decimal PreviousPeriod)> GetRevenueDataAsync(DateTime now, DateTime periodStart, DateTime previousPeriodStart)
    {
        var totalRevenue = await _context.Payments
            .Where(p => p.Status == PaymentStatus.Completed)
            .SumAsync(p => p.Amount);

        var monthlyRevenue = await _context.Payments
            .Where(p => p.Status == PaymentStatus.Completed && p.PaidAt >= now.AddDays(-30))
            .SumAsync(p => p.Amount);

        var weeklyRevenue = await _context.Payments
            .Where(p => p.Status == PaymentStatus.Completed && p.PaidAt >= now.AddDays(-7))
            .SumAsync(p => p.Amount);

        var dailyRevenue = await _context.Payments
            .Where(p => p.Status == PaymentStatus.Completed && p.PaidAt >= now.AddDays(-1))
            .SumAsync(p => p.Amount);

        var revenueThisPeriod = await _context.Payments
            .Where(p => p.Status == PaymentStatus.Completed && p.PaidAt >= periodStart)
            .SumAsync(p => p.Amount);

        var revenuePreviousPeriod = await _context.Payments
            .Where(p => p.Status == PaymentStatus.Completed && p.PaidAt >= previousPeriodStart && p.PaidAt < periodStart)
            .SumAsync(p => p.Amount);

        return (totalRevenue, monthlyRevenue, weeklyRevenue, dailyRevenue, revenueThisPeriod, revenuePreviousPeriod);
    }

    public async Task<SystemHealthDto> GetSystemHealthAsync()
    {
        var now = DateTime.UtcNow;
        
        // Check database connection
        var databaseConnected = await _context.Database.CanConnectAsync();
        
        // Count pending approvals
        var pendingApprovals = await _context.Bookings.CountAsync(b => b.Status == BookingStatus.PendingApproval) +
                              await _context.Users.CountAsync(u => u.KycStatus == KycStatus.InReview);
        
        // Count overdue maintenance (vehicles that haven't been serviced in 6 months)
        var overdueMaintenance = await _context.Vehicles.CountAsync(v => 
            v.LastServiceDate == null || v.LastServiceDate < now.AddMonths(-6));
        
        // Count disputes (this would need to be implemented based on your dispute system)
        var disputes = 0; // Placeholder
        
        // Count system errors (this would come from your logging system)
        var systemErrors = 0; // Placeholder

        return new SystemHealthDto
        {
            DatabaseConnected = databaseConnected,
            AllServicesHealthy = databaseConnected, // Add more health checks as needed
            PendingApprovals = pendingApprovals,
            OverdueMaintenance = overdueMaintenance,
            Disputes = disputes,
            SystemErrors = systemErrors,
            LastHealthCheck = now
        };
    }

    public async Task<List<ActivityFeedItemDto>> GetRecentActivityAsync(int count = 20)
    {
        return await _context.AuditLogs
            .OrderByDescending(a => a.Timestamp)
            .Take(count)
            .Select(a => new ActivityFeedItemDto
            {
                Id = a.Id,
                Entity = a.Entity,
                Action = a.Action,
                UserName = a.User.FirstName + " " + a.User.LastName,
                Timestamp = a.Timestamp,
                Details = a.Details
            })
            .ToListAsync();
    }

    public async Task<List<AlertDto>> GetAlertsAsync()
    {
        var alerts = new List<AlertDto>();
        var now = DateTime.UtcNow;

        // Overdue maintenance alerts
        var overdueMaintenance = await _context.Vehicles
            .Where(v => v.LastServiceDate == null || v.LastServiceDate < now.AddMonths(-6))
            .CountAsync();

        if (overdueMaintenance > 0)
        {
            alerts.Add(new AlertDto
            {
                Type = "Maintenance",
                Title = "Overdue Maintenance",
                Message = $"{overdueMaintenance} vehicles require maintenance",
                Severity = "Warning",
                CreatedAt = now,
                IsRead = false
            });
        }

        // Pending KYC alerts
        var pendingKyc = await _context.Users.CountAsync(u => u.KycStatus == KycStatus.Pending);
        if (pendingKyc > 0)
        {
            alerts.Add(new AlertDto
            {
                Type = "KYC",
                Title = "Pending KYC Approvals",
                Message = $"{pendingKyc} users awaiting KYC approval",
                Severity = "Info",
                CreatedAt = now,
                IsRead = false
            });
        }

        // Pending booking approvals
        var pendingBookings = await _context.Bookings.CountAsync(b => b.Status == BookingStatus.PendingApproval);
        if (pendingBookings > 0)
        {
            alerts.Add(new AlertDto
            {
                Type = "Booking",
                Title = "Pending Booking Approvals",
                Message = $"{pendingBookings} bookings awaiting approval",
                Severity = "Info",
                CreatedAt = now,
                IsRead = false
            });
        }

        return alerts;
    }

    public Task<byte[]> ExportDashboardToPdfAsync(DashboardRequestDto request)
    {
        // TODO: Implement PDF export using QuestPDF or iText7 (both .NET 8.0 compatible)
        // For now, return empty byte array
        return Task.FromResult(Array.Empty<byte>());
    }

    public async Task<byte[]> ExportDashboardToExcelAsync(DashboardRequestDto request)
    {
        // This would implement Excel export using EPPlus
        // For now, return empty byte array
        await Task.CompletedTask;
        return Array.Empty<byte>();
    }

    // Financial endpoints implementation
    public async Task<FinancialOverviewDto> GetFinancialOverviewAsync()
    {
        var now = DateTime.UtcNow;
        var allTimeRevenue = await _context.Payments.Where(p => p.Status == PaymentStatus.Completed).SumAsync(p => p.Amount);
        var yearStart = new DateTime(now.Year, 1, 1);
        var rollingMonthStart = now.AddDays(-30);
        var weekStart = now.AddDays(-7);
        var dayStart = now.AddDays(-1);

        var yearRevenue = await _context.Payments.Where(p => p.Status == PaymentStatus.Completed && p.PaidAt >= yearStart).SumAsync(p => p.Amount);
        var monthRevenue = await _context.Payments.Where(p => p.Status == PaymentStatus.Completed && p.PaidAt >= rollingMonthStart).SumAsync(p => p.Amount);
        var weekRevenue = await _context.Payments.Where(p => p.Status == PaymentStatus.Completed && p.PaidAt >= weekStart).SumAsync(p => p.Amount);
        var dayRevenue = await _context.Payments.Where(p => p.Status == PaymentStatus.Completed && p.PaidAt >= dayStart).SumAsync(p => p.Amount);

        // Revenue by source using ledger entry types and keyword heuristics
        var revenueBySource = new List<KeyValuePair<string, decimal>>();
        var ledger = await _context.LedgerEntries.ToListAsync();
        revenueBySource.Add(new KeyValuePair<string, decimal>("Deposits", ledger.Where(l => l.Type == LedgerEntryType.Deposit).Sum(l => l.Amount)));
        revenueBySource.Add(new KeyValuePair<string, decimal>("Fees", ledger.Where(l => l.Type == LedgerEntryType.Fee || (l.Description != null && l.Description.Contains("fee", StringComparison.OrdinalIgnoreCase))).Sum(l => l.Amount)));
        revenueBySource.Add(new KeyValuePair<string, decimal>("RefundsReceived", ledger.Where(l => l.Type == LedgerEntryType.RefundReceived).Sum(l => l.Amount)));

        var totalExpenses = await _context.Expenses.SumAsync(e => e.Amount);

        // Total fund balances = sum latest balance per group
        var latestLedgerPerGroup =
            from l in _context.LedgerEntries
            join m in _context.LedgerEntries
                .GroupBy(x => x.GroupId)
                .Select(g => new { GroupId = g.Key, MaxCreatedAt = g.Max(x => x.CreatedAt) })
            on new { l.GroupId, l.CreatedAt } equals new { m.GroupId, CreatedAt = m.MaxCreatedAt }
            select l;
        var totalFundBalances = await latestLedgerPerGroup.SumAsync(x => x.BalanceAfter);

        var totalPayments = await _context.Payments.CountAsync();
        var successPayments = await _context.Payments.CountAsync(p => p.Status == PaymentStatus.Completed);
        var failedPaymentsCount = await _context.Payments.CountAsync(p => p.Status == PaymentStatus.Failed);
        var failedPaymentsAmount = await _context.Payments.Where(p => p.Status == PaymentStatus.Failed).SumAsync(p => p.Amount);
        var pendingPayments = await _context.Payments.CountAsync(p => p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.Processing);
        var successRate = totalPayments == 0 ? 0 : (double)successPayments / totalPayments * 100d;

        // Revenue trend (last 30 days)
        var trendStart = now.AddDays(-30).Date;
        var revenueTrend = await _context.Payments
            .Where(p => p.Status == PaymentStatus.Completed && p.PaidAt >= trendStart)
            .GroupBy(p => p.PaidAt!.Value.Date)
            .Select(g => new TimeSeriesPointDto<decimal> { Date = g.Key, Value = g.Sum(p => p.Amount) })
            .ToListAsync();

        // Top spending groups (by expenses)
        var topGroups = await _context.Expenses
            .GroupBy(e => new { e.GroupId })
            .Select(g => new { g.Key.GroupId, Total = g.Sum(x => x.Amount) })
            .OrderByDescending(x => x.Total)
            .Take(10)
            .Join(_context.OwnershipGroups, g => g.GroupId, og => og.Id, (g, og) => new GroupSpendSummaryDto { GroupId = g.GroupId, GroupName = og.Name, TotalExpenses = g.Total })
            .ToListAsync();

        // Financial health score (simple composite)
        var groupsCount = await _context.OwnershipGroups.CountAsync();
        var negativeGroups = await GetNegativeBalanceGroupsAsync();
        var healthScore = Math.Max(0, 100 - (int)(negativeGroups.Count * 100.0 / Math.Max(1, groupsCount)) - (int)Math.Min(50, failedPaymentsCount));

        return new FinancialOverviewDto
        {
            TotalRevenueAllTime = allTimeRevenue,
            TotalRevenueYear = yearRevenue,
            TotalRevenueMonth = monthRevenue,
            TotalRevenueWeek = weekRevenue,
            TotalRevenueDay = dayRevenue,
            RevenueBySource = revenueBySource,
            TotalExpensesAllGroups = totalExpenses,
            TotalFundBalances = totalFundBalances,
            PaymentSuccessRate = Math.Round(successRate, 2),
            FailedPaymentsCount = failedPaymentsCount,
            FailedPaymentsAmount = failedPaymentsAmount,
            PendingPaymentsCount = pendingPayments,
            RevenueTrend = revenueTrend.OrderBy(x => x.Date).ToList(),
            TopSpendingGroups = topGroups,
            FinancialHealthScore = healthScore
        };
    }

    public async Task<FinancialGroupBreakdownDto> GetFinancialByGroupsAsync()
    {
        var groups = await _context.OwnershipGroups
            .AsNoTracking()
            .Select(g => new { g.Id, g.Name })
            .ToListAsync();
        var expenses = await _context.Expenses
            .AsNoTracking()
            .Select(e => new { e.GroupId, e.ExpenseType, e.Amount, e.CreatedAt })
            .ToListAsync();
        var payments = await _context.Payments
            .AsNoTracking()
            .Select(p => new { p.Id, p.InvoiceId, p.Status })
            .ToListAsync();
        var invoiceRows = await _context.Invoices
            .AsNoTracking()
            .Select(i => new { i.Id, i.ExpenseId })
            .ToListAsync();
        var expenseIdToGroup = await _context.Expenses
            .AsNoTracking()
            .Select(e => new { e.Id, e.GroupId })
            .ToListAsync();
        var invoiceGroupMap = invoiceRows
            .Join(expenseIdToGroup, i => i.ExpenseId, e => e.Id, (i, e) => new { i.Id, e.GroupId })
            .ToList();

        // Latest ledger balance per group
        var balances = await _context.LedgerEntries
            .GroupBy(l => l.GroupId)
            .Select(g => new { GroupId = g.Key, Balance = g.OrderByDescending(x => x.CreatedAt).First().BalanceAfter })
            .ToListAsync();

        var items = new List<GroupFinancialItemDto>();
        foreach (var g in groups)
        {
            var gExpenses = expenses.Where(e => e.GroupId == g.Id).ToList();
            var byType = gExpenses
                .GroupBy(e => e.ExpenseType)
                .ToDictionary(k => k.Key.ToString(), v => v.Sum(x => x.Amount));
            var balance = balances.FirstOrDefault(b => b.GroupId == g.Id)?.Balance ?? 0m;

            var groupInvoiceIds = invoiceGroupMap.Where(x => x.GroupId == g.Id).Select(x => x.Id).ToHashSet();
            var groupPayments = payments.Where(p => groupInvoiceIds.Contains(p.InvoiceId)).ToList();
            var total = groupPayments.Count;
            var completed = groupPayments.Count(p => p.Status == PaymentStatus.Completed);
            var compliance = total == 0 ? 100d : (double)completed / total * 100d;

            items.Add(new GroupFinancialItemDto
            {
                GroupId = g.Id,
                GroupName = g.Name,
                TotalExpenses = gExpenses.Sum(e => e.Amount),
                ExpensesByType = byType,
                FundBalance = balance,
                HasFinancialIssues = balance < 0,
                PaymentComplianceRate = Math.Round(compliance, 2)
            });
        }

        return new FinancialGroupBreakdownDto { Groups = items.OrderByDescending(i => i.TotalExpenses).ToList() };
    }

    public async Task<PaymentStatisticsDto> GetPaymentStatisticsAsync()
    {
        var all = await _context.Payments.ToListAsync();
        var total = all.Count;
        var success = all.Count(p => p.Status == PaymentStatus.Completed);
        var failed = all.Count(p => p.Status == PaymentStatus.Failed);
        var avg = total == 0 ? 0 : all.Average(p => (double)p.Amount);
        var methodCounts = all.GroupBy(p => p.Method).ToDictionary(g => g.Key, g => g.Count());

        var start = DateTime.UtcNow.AddDays(-30).Date;
        var volumeTrend = all.Where(p => p.CreatedAt >= start)
            .GroupBy(p => p.CreatedAt.Date)
            .Select(g => new TimeSeriesPointDto<int> { Date = g.Key, Value = g.Count() })
            .OrderBy(x => x.Date)
            .ToList();

        // Failed reasons heuristic: parse from Notes or TransactionReference keywords
        var failedReasons = all.Where(p => p.Status == PaymentStatus.Failed)
            .Select(p => (Reason: ExtractFailureReason(p.Notes, p.TransactionReference), p))
            .GroupBy(x => x.Reason)
            .ToDictionary(g => g.Key, g => g.Count());

        // VNPay summary: treat EWallet method with VNPAY token in reference as VNPay
        var vnp = all.Where(p => p.Method == PaymentMethod.EWallet && (p.TransactionReference ?? string.Empty).Contains("VNPAY", StringComparison.OrdinalIgnoreCase));
        var vnSummary = new VnPaySummaryDto
        {
            SuccessCount = vnp.Count(p => p.Status == PaymentStatus.Completed),
            FailedCount = vnp.Count(p => p.Status == PaymentStatus.Failed),
            TotalAmount = vnp.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount)
        };

        return new PaymentStatisticsDto
        {
            SuccessRate = total == 0 ? 0 : Math.Round((double)success / total * 100d, 2),
            FailureRate = total == 0 ? 0 : Math.Round((double)failed / total * 100d, 2),
            MethodCounts = methodCounts,
            AverageAmount = (decimal)avg,
            VolumeTrend = volumeTrend,
            FailedReasons = failedReasons,
            VnPay = vnSummary
        };
    }

    public async Task<ExpenseAnalysisDto> GetExpenseAnalysisAsync()
    {
        // Select only necessary columns to avoid EF generating joins with mismatched shadow FKs
        var expenseRows = await _context.Expenses
            .AsNoTracking()
            .Select(e => new { e.ExpenseType, e.Amount, e.CreatedAt, e.GroupId })
            .ToListAsync();

        var byType = expenseRows
            .GroupBy(e => e.ExpenseType)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var start = DateTime.UtcNow.AddDays(-90).Date;
        var trend = expenseRows
            .Where(e => e.CreatedAt.Date >= start)
            .GroupBy(e => e.CreatedAt.Date)
            .Select(g => new TimeSeriesPointDto<decimal> { Date = g.Key, Value = g.Sum(x => x.Amount) })
            .OrderBy(x => x.Date)
            .ToList();

        var vehicleCount = await _context.Vehicles.AsNoTracking().CountAsync();
        var avgPerVehicle = vehicleCount == 0 ? 0 : expenseRows.Sum(e => e.Amount) / vehicleCount;

        var costPerGroup = expenseRows
            .GroupBy(e => e.GroupId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var optimizations = new List<string>();
        var topType = byType.OrderByDescending(x => x.Value).FirstOrDefault();
        if (topType.Value > 0)
            optimizations.Add($"High spend in {topType.Key}. Review vendors and maintenance schedules.");

        return new ExpenseAnalysisDto
        {
            TotalByType = byType,
            ExpenseTrend = trend,
            AverageCostPerVehicle = avgPerVehicle,
            CostPerGroup = costPerGroup,
            OptimizationOpportunities = optimizations
        };
    }

    public async Task<FinancialAnomaliesDto> GetFinancialAnomaliesAsync()
    {
        var completed = await _context.Payments.Where(p => p.Status == PaymentStatus.Completed).ToListAsync();
        var amounts = completed.Select(p => (double)p.Amount).ToList();
        var mean = amounts.Count == 0 ? 0 : amounts.Average();
        var std = amounts.Count <= 1 ? 0 : Math.Sqrt(amounts.Sum(a => Math.Pow(a - mean, 2)) / (amounts.Count - 1));
        var anomalies = new List<PaymentAnomalyDto>();
        if (std > 0)
        {
            foreach (var p in completed)
            {
                var z = ((double)p.Amount - mean) / std;
                if (Math.Abs(z) >= 3)
                {
                    anomalies.Add(new PaymentAnomalyDto { PaymentId = p.Id, Amount = p.Amount, ZScore = Math.Round(z, 2), PaidAt = p.PaidAt, Method = p.Method });
                }
            }
        }

        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
        var suspicious = await _context.Payments
            .Where(p => p.Status == PaymentStatus.Failed && p.CreatedAt >= sevenDaysAgo)
            .GroupBy(p => p.PayerId)
            .Select(g => new SuspiciousPatternDto { PayerId = g.Key, FailedCount7Days = g.Count() })
            .Where(x => x.FailedCount7Days >= 3)
            .ToListAsync();

        var negative = await GetNegativeBalanceGroupsAsync();

        return new FinancialAnomaliesDto
        {
            UnusualTransactions = anomalies,
            SuspiciousPaymentPatterns = suspicious,
            NegativeBalanceGroups = negative
        };
    }

    public async Task<byte[]> GenerateFinancialPdfAsync(FinancialReportRequestDto request)
    {
        // Simple JSON-as-PDF placeholder
        var overview = await GetFinancialOverviewAsync();
        var groups = await GetFinancialByGroupsAsync();
        var payments = await GetPaymentStatisticsAsync();
        var expenses = await GetExpenseAnalysisAsync();
        var anomalies = await GetFinancialAnomaliesAsync();
        var payload = System.Text.Json.JsonSerializer.Serialize(new { request, overview, groups, payments, expenses, anomalies }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        return System.Text.Encoding.UTF8.GetBytes($"PDF Financial Report\n\n{payload}");
    }

    public async Task<byte[]> GenerateFinancialExcelAsync(FinancialReportRequestDto request)
    {
        var overview = await GetFinancialOverviewAsync();
        var groups = await GetFinancialByGroupsAsync();
        var payments = await GetPaymentStatisticsAsync();
        var expenses = await GetExpenseAnalysisAsync();
        var payload = System.Text.Json.JsonSerializer.Serialize(new { request, overview, groups, payments, expenses }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        return System.Text.Encoding.UTF8.GetBytes($"Excel Financial Report\n\n{payload}");
    }

    private string ExtractFailureReason(string? notes, string? reference)
    {
        var source = (notes ?? string.Empty) + " " + (reference ?? string.Empty);
        if (source.Contains("insufficient", StringComparison.OrdinalIgnoreCase)) return "Insufficient funds";
        if (source.Contains("timeout", StringComparison.OrdinalIgnoreCase)) return "Gateway timeout";
        if (source.Contains("declined", StringComparison.OrdinalIgnoreCase)) return "Card declined";
        if (source.Contains("network", StringComparison.OrdinalIgnoreCase)) return "Network error";
        return "Unknown";
    }

    private async Task<List<GroupNegativeBalanceDto>> GetNegativeBalanceGroupsAsync()
    {
        var latestNegative =
            from l in _context.LedgerEntries
            join m in _context.LedgerEntries
                .GroupBy(x => x.GroupId)
                .Select(g => new { GroupId = g.Key, MaxCreatedAt = g.Max(x => x.CreatedAt) })
            on new { l.GroupId, l.CreatedAt } equals new { m.GroupId, CreatedAt = m.MaxCreatedAt }
            where l.BalanceAfter < 0
            select new { l.GroupId, l.BalanceAfter };
        var latest = await latestNegative.ToListAsync();
        var map = await _context.OwnershipGroups.Where(g => latest.Select(l => l.GroupId).Contains(g.Id))
            .Select(g => new { g.Id, g.Name })
            .ToListAsync();
        return latest.Join(map, l => l.GroupId, g => g.Id, (l, g) => new GroupNegativeBalanceDto { GroupId = l.GroupId, GroupName = g.Name, Balance = l.BalanceAfter }).ToList();
    }

    private async Task<(KycStatus Status, string Reason)> DetermineOverallKycStatusAsync(Guid userId, KycDocument? updatedDocument = null)
    {
        var documents = await _context.KycDocuments
            .Where(d => d.UserId == userId && (updatedDocument == null || d.Id != updatedDocument.Id))
            .ToListAsync();

        if (updatedDocument != null)
        {
            documents.Add(updatedDocument);
        }

        if (!documents.Any())
        {
            return (KycStatus.Pending, "No KYC documents uploaded");
        }

        if (documents.Any(d => d.Status == KycDocumentStatus.Rejected))
        {
            return (KycStatus.Rejected, "One or more documents rejected");
        }

        if (documents.Any(d => d.Status == KycDocumentStatus.RequiresUpdate))
        {
            return (KycStatus.Pending, "Documents require updates");
        }

        if (documents.Any(d => d.Status == KycDocumentStatus.Pending || d.Status == KycDocumentStatus.UnderReview))
        {
            return (KycStatus.InReview, "Documents under review");
        }

        if (documents.All(d => d.Status == KycDocumentStatus.Approved) && documents.Count >= 2)
        {
            return (KycStatus.Approved, "All documents approved");
        }

        return (KycStatus.InReview, "Awaiting additional documents");
    }

    private void ApplyUserKycStatusChange(User user, KycStatus newStatus, Guid adminUserId, string? reason)
    {
        var now = DateTime.UtcNow;
        var oldStatus = user.KycStatus;

        if (oldStatus == newStatus && string.IsNullOrWhiteSpace(reason))
        {
            return;
        }

        if (oldStatus != newStatus)
        {
            user.KycStatus = newStatus;
            user.UpdatedAt = now;
        }

        var baseDetail = oldStatus == newStatus
            ? $"KYC status confirmed as {newStatus}."
            : $"KYC status changed from {oldStatus} to {newStatus}.";

        var details = string.IsNullOrWhiteSpace(reason) ? baseDetail : $"{baseDetail} Reason: {reason}";

        _context.AuditLogs.Add(new AuditLog
        {
            Entity = "User",
            EntityId = user.Id,
            Action = "KycStatusUpdated",
            Details = details,
            PerformedBy = adminUserId,
            Timestamp = now,
            IpAddress = "Admin API",
            UserAgent = "Admin API"
        });
    }

    private DateTime GetPeriodStart(DateTime date, TimePeriod period)
    {
        return period switch
        {
            TimePeriod.Daily => date.Date,
            TimePeriod.Weekly => date.AddDays(-(int)date.DayOfWeek).Date,
            TimePeriod.Monthly => new DateTime(date.Year, date.Month, 1),
            TimePeriod.Quarterly => new DateTime(date.Year, ((date.Month - 1) / 3) * 3 + 1, 1),
            TimePeriod.Yearly => new DateTime(date.Year, 1, 1),
            _ => date.Date
        };
    }

    private double CalculateGrowthPercentage(double previous, double current)
    {
        if (previous == 0) return current > 0 ? 100 : 0;
        return Math.Round(((current - previous) / previous) * 100, 2);
    }

    // User Management Methods
    public async Task<UserListResponseDto> GetUsersAsync(UserListRequestDto request)
    {
        var query = _context.Users.AsQueryable();

        // Apply search filter
        if (!string.IsNullOrEmpty(request.Search))
        {
            var searchTerm = request.Search.ToLower();
            query = query.Where(u => 
                u.FirstName.ToLower().Contains(searchTerm) ||
                u.LastName.ToLower().Contains(searchTerm) ||
                u.Email.ToLower().Contains(searchTerm) ||
                (u.Phone != null && u.Phone.Contains(searchTerm)));
        }

        // Apply role filter
        if (request.Role.HasValue)
        {
            query = query.Where(u => u.Role == request.Role.Value);
        }

        // Apply KYC status filter
        if (request.KycStatus.HasValue)
        {
            query = query.Where(u => u.KycStatus == request.KycStatus.Value);
        }

        // Apply account status filter
        if (request.AccountStatus.HasValue)
        {
            var now = DateTime.UtcNow;
            query = request.AccountStatus.Value switch
            {
                UserAccountStatus.Active => query.Where(u => u.LockoutEnd == null || u.LockoutEnd < now),
                UserAccountStatus.Inactive => query.Where(u => u.LockoutEnd != null && u.LockoutEnd > now),
                UserAccountStatus.Suspended => query.Where(u => u.LockoutEnd != null && u.LockoutEnd > now),
                UserAccountStatus.Banned => query.Where(u => u.LockoutEnd != null && u.LockoutEnd > now),
                _ => query
            };
        }

        // Apply sorting
        query = request.SortBy?.ToLower() switch
        {
            "email" => request.SortDirection == "asc" ? query.OrderBy(u => u.Email) : query.OrderByDescending(u => u.Email),
            "firstname" => request.SortDirection == "asc" ? query.OrderBy(u => u.FirstName) : query.OrderByDescending(u => u.FirstName),
            "lastname" => request.SortDirection == "asc" ? query.OrderBy(u => u.LastName) : query.OrderByDescending(u => u.LastName),
            "role" => request.SortDirection == "asc" ? query.OrderBy(u => u.Role) : query.OrderByDescending(u => u.Role),
            "kycstatus" => request.SortDirection == "asc" ? query.OrderBy(u => u.KycStatus) : query.OrderByDescending(u => u.KycStatus),
            _ => request.SortDirection == "asc" ? query.OrderBy(u => u.CreatedAt) : query.OrderByDescending(u => u.CreatedAt)
        };

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);

        var users = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(u => new UserSummaryDto
            {
                Id = u.Id,
                Email = u.Email,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Phone = u.Phone,
                Role = u.Role,
                KycStatus = u.KycStatus,
                AccountStatus = GetUserAccountStatus(u),
                CreatedAt = u.CreatedAt,
                LastLoginAt = null // This would need to be tracked separately
            })
            .ToListAsync();

        return new UserListResponseDto
        {
            Users = users,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalPages = totalPages
        };
    }

    public async Task<UserDetailsDto> GetUserDetailsAsync(Guid userId)
    {
        var user = await _context.Users
            .Include(u => u.GroupMemberships)
                .ThenInclude(gm => gm.Group)
            .Include(u => u.KycDocuments)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            throw new ArgumentException("User not found");

        var groupMemberships = user.GroupMemberships.Select(gm => new GroupMembershipDto
        {
            GroupId = gm.GroupId,
            GroupName = gm.Group.Name,
            Role = gm.RoleInGroup.ToString(),
            JoinedAt = gm.JoinedAt,
            IsActive = true // Group memberships are considered active by default
        }).ToList();

        var statistics = await GetUserStatisticsAsync(userId);

        var kycDocuments = user.KycDocuments.Select(kd => new KycDocumentDto
        {
            Id = kd.Id,
            UserId = kd.UserId,
            UserName = user.FirstName + " " + user.LastName,
            DocumentType = kd.DocumentType,
            FileName = kd.FileName,
            StorageUrl = kd.StorageUrl,
            Status = kd.Status,
            ReviewNotes = kd.ReviewNotes,
            ReviewedBy = kd.ReviewedBy,
            ReviewerName = kd.Reviewer != null ? kd.Reviewer.FirstName + " " + kd.Reviewer.LastName : null,
            ReviewedAt = kd.ReviewedAt,
            UploadedAt = kd.CreatedAt
        }).ToList();

        var recentActivity = await _context.AuditLogs
            .Where(a => a.PerformedBy == userId)
            .OrderByDescending(a => a.Timestamp)
            .Take(10)
            .Select(a => new UserActivityDto
            {
                Id = a.Id,
                Action = a.Action,
                Entity = a.Entity,
                Details = a.Details,
                Timestamp = a.Timestamp
            })
            .ToListAsync();

        return new UserDetailsDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Phone = user.Phone,
            Address = user.Address,
            City = user.City,
            Country = user.Country,
            PostalCode = user.PostalCode,
            DateOfBirth = user.DateOfBirth,
            Role = user.Role,
            KycStatus = user.KycStatus,
            AccountStatus = GetUserAccountStatus(user),
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            LastLoginAt = null, // This would need to be tracked separately
            GroupMemberships = groupMemberships,
            Statistics = statistics,
            KycDocuments = kycDocuments,
            RecentActivity = recentActivity
        };
    }

    public async Task<bool> UpdateUserStatusAsync(Guid userId, UpdateUserStatusDto request, Guid adminUserId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            return false;

        var oldStatus = GetUserAccountStatus(user);
        
        // Update user status based on the request
        var now = DateTime.UtcNow;
        switch (request.Status)
        {
            case UserAccountStatus.Active:
                user.LockoutEnd = null;
                break;
            case UserAccountStatus.Inactive:
            case UserAccountStatus.Suspended:
            case UserAccountStatus.Banned:
                user.LockoutEnd = now.AddYears(1); // Set lockout for 1 year
                break;
        }

        // Create audit log
        var auditLog = new AuditLog
        {
            Entity = "User",
            EntityId = userId,
            Action = "StatusUpdated",
            Details = $"Status changed from {oldStatus} to {request.Status}. Reason: {request.Reason ?? "No reason provided"}",
            PerformedBy = adminUserId,
            Timestamp = now,
            IpAddress = "127.0.0.1", // This should be passed from the controller
            UserAgent = "Admin API"
        };

        _context.AuditLogs.Add(auditLog);
        await _context.SaveChangesAsync();

        // TODO: Publish UserStatusChangedEvent
        // TODO: Send notification to user

        return true;
    }

    public async Task<bool> UpdateUserRoleAsync(Guid userId, UpdateUserRoleDto request, Guid adminUserId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
            return false;

        var oldRole = user.Role;
        user.Role = request.Role;

        // Create audit log
        var auditLog = new AuditLog
        {
            Entity = "User",
            EntityId = userId,
            Action = "RoleUpdated",
            Details = $"Role changed from {oldRole} to {request.Role}. Reason: {request.Reason ?? "No reason provided"}",
            PerformedBy = adminUserId,
            Timestamp = DateTime.UtcNow,
            IpAddress = "127.0.0.1", // This should be passed from the controller
            UserAgent = "Admin API"
        };

        _context.AuditLogs.Add(auditLog);
        await _context.SaveChangesAsync();

        // TODO: Publish RoleChangedEvent

        return true;
    }

    public async Task<List<PendingKycUserDto>> GetPendingKycUsersAsync()
    {
        var users = await _context.Users
            .Where(u => u.KycStatus == KycStatus.Pending || u.KycStatus == KycStatus.InReview)
            .Include(u => u.KycDocuments)
            .OrderBy(u => u.CreatedAt)
            .Select(u => new PendingKycUserDto
            {
                Id = u.Id,
                Email = u.Email,
                FirstName = u.FirstName,
                LastName = u.LastName,
                KycStatus = u.KycStatus,
                SubmittedAt = u.CreatedAt,
                Documents = u.KycDocuments.Select(kd => new KycDocumentDto
                {
                    Id = kd.Id,
                    UserId = kd.UserId,
                    UserName = u.FirstName + " " + u.LastName,
                    DocumentType = kd.DocumentType,
                    FileName = kd.FileName,
                    StorageUrl = kd.StorageUrl,
                    Status = kd.Status,
                    ReviewNotes = kd.ReviewNotes,
                    ReviewedBy = kd.ReviewedBy,
                    ReviewerName = kd.Reviewer != null ? kd.Reviewer.FirstName + " " + kd.Reviewer.LastName : null,
                    ReviewedAt = kd.ReviewedAt,
                    UploadedAt = kd.CreatedAt
                }).ToList()
            })
            .ToListAsync();

        return users;
    }

    public async Task<KycDocumentDto> ReviewKycDocumentAsync(Guid documentId, ReviewKycDocumentDto request, Guid adminUserId)
    {
        var document = await _context.KycDocuments
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document == null)
            throw new ArgumentException("KYC document not found");

        var reviewer = await _context.Users.FirstOrDefaultAsync(u => u.Id == adminUserId);
        if (reviewer == null)
            throw new ArgumentException("Reviewer not found");

        var now = DateTime.UtcNow;
        document.Status = request.Status;
        document.ReviewNotes = request.ReviewNotes;
        document.ReviewedBy = adminUserId;
        document.ReviewedAt = now;
        document.UpdatedAt = now;

        _context.AuditLogs.Add(new AuditLog
        {
            Entity = "KycDocument",
            EntityId = document.Id,
            Action = "Reviewed",
            Details = $"KYC document {document.DocumentType} reviewed with status {request.Status}.",
            PerformedBy = adminUserId,
            Timestamp = now,
            IpAddress = "Admin API",
            UserAgent = "Admin API"
        });

        var (overallStatus, reason) = await DetermineOverallKycStatusAsync(document.UserId, document);

        var user = document.User ?? await _context.Users.FirstAsync(u => u.Id == document.UserId);

        ApplyUserKycStatusChange(user, overallStatus, adminUserId, reason);

        await _context.SaveChangesAsync();

        return new KycDocumentDto
        {
            Id = document.Id,
            UserId = document.UserId,
            UserName = $"{user.FirstName} {user.LastName}",
            DocumentType = document.DocumentType,
            FileName = document.FileName,
            StorageUrl = document.StorageUrl,
            Status = document.Status,
            ReviewNotes = document.ReviewNotes,
            ReviewedBy = document.ReviewedBy,
            ReviewerName = $"{reviewer.FirstName} {reviewer.LastName}",
            ReviewedAt = document.ReviewedAt,
            UploadedAt = document.CreatedAt
        };
    }

    public async Task<bool> UpdateUserKycStatusAsync(Guid userId, UpdateUserKycStatusDto request, Guid adminUserId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            return false;

        ApplyUserKycStatusChange(user, request.Status, adminUserId, request.Reason);
        await _context.SaveChangesAsync();
        return true;
    }

    private UserAccountStatus GetUserAccountStatus(User user)
    {
        var now = DateTime.UtcNow;
        if (user.LockoutEnd == null || user.LockoutEnd < now)
            return UserAccountStatus.Active;
        
        // For simplicity, we'll treat all locked accounts as suspended
        // In a real implementation, you might have a separate field to distinguish between suspended and banned
        return UserAccountStatus.Suspended;
    }

    private async Task<UserStatisticsDto> GetUserStatisticsAsync(Guid userId)
    {
        var totalBookings = await _context.Bookings.CountAsync(b => b.UserId == userId);
        var completedBookings = await _context.Bookings.CountAsync(b => b.UserId == userId && b.Status == BookingStatus.Completed);
        var cancelledBookings = await _context.Bookings.CountAsync(b => b.UserId == userId && b.Status == BookingStatus.Cancelled);
        var totalPayments = await _context.Payments
            .Where(p => p.PayerId == userId && p.Status == PaymentStatus.Completed)
            .SumAsync(p => p.Amount);
        var groupMemberships = await _context.GroupMembers.CountAsync(gm => gm.UserId == userId);
        var activeGroupMemberships = await _context.GroupMembers.CountAsync(gm => gm.UserId == userId); // All memberships are considered active

        return new UserStatisticsDto
        {
            TotalBookings = totalBookings,
            CompletedBookings = completedBookings,
            CancelledBookings = cancelledBookings,
            TotalPayments = totalPayments,
            GroupMemberships = groupMemberships,
            ActiveGroupMemberships = activeGroupMemberships
        };
    }

    // Group Management Methods
    public async Task<GroupListResponseDto> GetGroupsAsync(GroupListRequestDto request)
    {
        var query = _context.OwnershipGroups.AsQueryable();

        // Apply search filter
        if (!string.IsNullOrEmpty(request.Search))
        {
            var searchTerm = request.Search.ToLower();
            query = query.Where(g => g.Name.ToLower().Contains(searchTerm) ||
                                   (g.Description != null && g.Description.ToLower().Contains(searchTerm)));
        }

        // Apply status filter
        if (request.Status.HasValue)
        {
            query = query.Where(g => g.Status == request.Status.Value);
        }

        // Apply member count filters
        if (request.MinMemberCount.HasValue || request.MaxMemberCount.HasValue)
        {
            query = query.Where(g => g.Members.Count >= (request.MinMemberCount ?? 0) &&
                                   g.Members.Count <= (request.MaxMemberCount ?? int.MaxValue));
        }

        // Apply sorting
        query = request.SortBy?.ToLower() switch
        {
            "name" => request.SortDirection == "asc" ? query.OrderBy(g => g.Name) : query.OrderByDescending(g => g.Name),
            "membercount" => request.SortDirection == "asc" ? query.OrderBy(g => g.Members.Count) : query.OrderByDescending(g => g.Members.Count),
            "activityscore" => request.SortDirection == "asc" ? query.OrderBy(g => CalculateGroupActivityScore(g)) : query.OrderByDescending(g => CalculateGroupActivityScore(g)),
            _ => request.SortDirection == "asc" ? query.OrderBy(g => g.CreatedAt) : query.OrderByDescending(g => g.CreatedAt)
        };

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);

        var groups = await query
            .Include(g => g.Creator)
            .Include(g => g.Members)
            .Include(g => g.Vehicles)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(g => new GroupSummaryDto
            {
                Id = g.Id,
                Name = g.Name,
                Description = g.Description,
                Status = g.Status,
                MemberCount = g.Members.Count,
                VehicleCount = g.Vehicles.Count,
                CreatedAt = g.CreatedAt,
                ActivityScore = CalculateGroupActivityScore(g),
                HealthStatus = GetGroupHealthStatus(g),
                CreatorName = g.Creator.FirstName + " " + g.Creator.LastName
            })
            .ToListAsync();

        return new GroupListResponseDto
        {
            Groups = groups,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalPages = totalPages
        };
    }

    public async Task<GroupDetailsDto> GetGroupDetailsAsync(Guid groupId)
    {
        var group = await _context.OwnershipGroups
            .Include(g => g.Creator)
            .Include(g => g.Members)
                .ThenInclude(m => m.User)
            .Include(g => g.Vehicles)
            .Include(g => g.Proposals)
                .ThenInclude(p => p.Creator)
            .Include(g => g.Proposals)
                .ThenInclude(p => p.Votes)
            .FirstOrDefaultAsync(g => g.Id == groupId);

        if (group == null)
            throw new ArgumentException("Group not found");

        var members = group.Members.Select(m => new GroupMemberDetailsDto
        {
            UserId = m.UserId,
            UserName = m.User.FirstName + " " + m.User.LastName,
            Email = m.User.Email,
            Role = m.RoleInGroup,
            SharePercentage = m.SharePercentage,
            JoinedAt = m.JoinedAt,
            IsActive = true, // All members are considered active
            TotalBookings = 0, // This would need to be calculated separately
            TotalPayments = 0 // This would need to be calculated separately
        }).ToList();

        var vehicles = group.Vehicles.Select(v => new GroupVehicleDto
        {
            Id = v.Id,
            Vin = v.Vin,
            PlateNumber = v.PlateNumber,
            Model = v.Model,
            Year = v.Year,
            Color = v.Color,
            Status = v.Status,
            LastServiceDate = v.LastServiceDate,
            Odometer = v.Odometer,
            TotalBookings = 0, // This would need to be calculated separately
            TotalRevenue = 0 // This would need to be calculated separately
        }).ToList();

        var financialSummary = await GetGroupFinancialSummaryAsync(groupId);
        var bookingStatistics = await GetGroupBookingStatisticsAsync(groupId);
        var recentActivity = await GetGroupRecentActivityAsync(groupId);
        var proposals = group.Proposals.Select(p => new GroupProposalDto
        {
            Id = p.Id,
            Title = p.Title,
            Description = p.Description,
            Type = p.Type,
            Status = p.Status,
            Amount = p.Amount,
            CreatedAt = p.CreatedAt,
            VotingEndDate = p.VotingEndDate,
            CreatorName = p.Creator.FirstName + " " + p.Creator.LastName,
            TotalVotes = p.Votes.Count,
            YesVotes = p.Votes.Count(v => v.Choice == VoteChoice.Yes),
            NoVotes = p.Votes.Count(v => v.Choice == VoteChoice.No),
            ApprovalPercentage = p.Votes.Count > 0 ? (decimal)p.Votes.Count(v => v.Choice == VoteChoice.Yes) / p.Votes.Count * 100 : 0
        }).ToList();

        var disputes = new List<GroupDisputeDto>(); // This would need to be implemented based on your dispute system

        var health = await GetGroupHealthAsync(groupId);

        return new GroupDetailsDto
        {
            Id = group.Id,
            Name = group.Name,
            Description = group.Description,
            Status = group.Status,
            CreatedAt = group.CreatedAt,
            UpdatedAt = group.UpdatedAt,
            CreatorName = group.Creator.FirstName + " " + group.Creator.LastName,
            Members = members,
            Vehicles = vehicles,
            FinancialSummary = financialSummary,
            BookingStatistics = bookingStatistics,
            RecentActivity = recentActivity,
            Proposals = proposals,
            Disputes = disputes,
            Health = health
        };
    }

    public async Task<bool> UpdateGroupStatusAsync(Guid groupId, UpdateGroupStatusDto request, Guid adminUserId)
    {
        var group = await _context.OwnershipGroups.FindAsync(groupId);
        if (group == null)
            return false;

        var oldStatus = group.Status;
        group.Status = request.Status;

        // Handle status change implications
        await HandleGroupStatusChangeImplicationsAsync(groupId, oldStatus, request.Status);

        // Create audit log
        var auditLog = new AuditLog
        {
            Entity = "OwnershipGroup",
            EntityId = groupId,
            Action = "StatusUpdated",
            Details = $"Group status changed from {oldStatus} to {request.Status}. Reason: {request.Reason}",
            PerformedBy = adminUserId,
            Timestamp = DateTime.UtcNow,
            IpAddress = "127.0.0.1",
            UserAgent = "Admin API"
        };

        _context.AuditLogs.Add(auditLog);
        await _context.SaveChangesAsync();

        // TODO: Publish GroupStatusChangedEvent
        // TODO: Send notification to all members if requested

        return true;
    }

    public async Task<GroupAuditResponseDto> GetGroupAuditTrailAsync(Guid groupId, GroupAuditRequestDto request)
    {
        var query = _context.AuditLogs
            .Where(a => a.Entity == "OwnershipGroup" && a.EntityId == groupId);

        // Apply filters
        if (!string.IsNullOrEmpty(request.Search))
        {
            var searchTerm = request.Search.ToLower();
            query = query.Where(a => a.Details.ToLower().Contains(searchTerm) ||
                                   a.Action.ToLower().Contains(searchTerm));
        }

        if (!string.IsNullOrEmpty(request.Action))
        {
            query = query.Where(a => a.Action == request.Action);
        }

        if (request.UserId.HasValue)
        {
            query = query.Where(a => a.PerformedBy == request.UserId.Value);
        }

        if (request.FromDate.HasValue)
        {
            query = query.Where(a => a.Timestamp >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            query = query.Where(a => a.Timestamp <= request.ToDate.Value);
        }

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);

        var entries = await query
            .Include(a => a.User)
            .OrderByDescending(a => a.Timestamp)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new GroupAuditEntryDto
            {
                Id = a.Id,
                Action = a.Action,
                Entity = a.Entity,
                Details = a.Details,
                UserName = a.User.FirstName + " " + a.User.LastName,
                Timestamp = a.Timestamp,
                IpAddress = a.IpAddress,
                UserAgent = a.UserAgent
            })
            .ToListAsync();

        return new GroupAuditResponseDto
        {
            Entries = entries,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalPages = totalPages
        };
    }

    public async Task<bool> InterveneInGroupAsync(Guid groupId, GroupInterventionDto request, Guid adminUserId)
    {
        var group = await _context.OwnershipGroups.FindAsync(groupId);
        if (group == null)
            return false;

        // Handle intervention based on type
        switch (request.Type)
        {
            case InterventionType.Freeze:
                group.Status = GroupStatus.Inactive;
                // TODO: Implement freeze logic (cancel bookings, prevent new expenses)
                break;
            case InterventionType.Unfreeze:
                group.Status = GroupStatus.Active;
                // TODO: Implement unfreeze logic
                break;
            case InterventionType.AppointAdmin:
                // TODO: Implement appoint temporary admin logic
                break;
            case InterventionType.Message:
                // TODO: Send message to all group members
                break;
        }

        // Create audit log
        var auditLog = new AuditLog
        {
            Entity = "OwnershipGroup",
            EntityId = groupId,
            Action = "Intervention",
            Details = $"Admin intervention: {request.Type}. Reason: {request.Reason}. Message: {request.Message}",
            PerformedBy = adminUserId,
            Timestamp = DateTime.UtcNow,
            IpAddress = "127.0.0.1",
            UserAgent = "Admin API"
        };

        _context.AuditLogs.Add(auditLog);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<GroupHealthDto> GetGroupHealthAsync(Guid groupId)
    {
        var group = await _context.OwnershipGroups
            .Include(g => g.Members)
            .Include(g => g.Vehicles)
            .Include(g => g.Bookings)
            .FirstOrDefaultAsync(g => g.Id == groupId);

        if (group == null)
            throw new ArgumentException("Group not found");

        var health = new GroupHealthDto
        {
            Status = GetGroupHealthStatus(group),
            Score = CalculateGroupHealthScore(group),
            Issues = new List<string>(),
            Warnings = new List<string>(),
            LastActivity = group.UpdatedAt,
            DaysInactive = (int)(DateTime.UtcNow - group.UpdatedAt).TotalDays,
            HasActiveDisputes = false, // This would need to be implemented based on your dispute system
            IsUsageBalanced = await IsGroupUsageBalancedAsync(groupId),
            Recommendation = GetGroupHealthRecommendation(group)
        };

        // Add health issues and warnings
        if (health.DaysInactive > 30)
        {
            health.Issues.Add("Group has been inactive for more than 30 days");
        }

        if (group.Members.Count < 2)
        {
            health.Warnings.Add("Group has fewer than 2 members");
        }

        if (!health.IsUsageBalanced)
        {
            health.Warnings.Add("Group usage appears unbalanced");
        }

        return health;
    }

    // Helper methods for group management
    private decimal CalculateGroupActivityScore(OwnershipGroup group)
    {
        // This is a simplified calculation - in reality, you'd want more sophisticated scoring
        var daysSinceCreation = (DateTime.UtcNow - group.CreatedAt).TotalDays;
        var memberCount = group.Members.Count;
        var vehicleCount = group.Vehicles.Count;
        
        // Basic activity score based on group age, size, and activity
        return Math.Min(100, (decimal)(memberCount * 10 + vehicleCount * 5 + (100 - daysSinceCreation)));
    }

    private GroupHealthStatus GetGroupHealthStatus(OwnershipGroup group)
    {
        var daysInactive = (DateTime.UtcNow - group.UpdatedAt).TotalDays;
        
        if (daysInactive > 30 || group.Members.Count < 2)
            return GroupHealthStatus.Critical;
        if (daysInactive > 14 || group.Members.Count < 3)
            return GroupHealthStatus.Unhealthy;
        if (daysInactive > 7)
            return GroupHealthStatus.Warning;
        
        return GroupHealthStatus.Healthy;
    }

    private decimal CalculateGroupHealthScore(OwnershipGroup group)
    {
        var daysInactive = (DateTime.UtcNow - group.UpdatedAt).TotalDays;
        var score = 100m;
        
        // Deduct points for inactivity
        score -= Math.Min(50, (decimal)daysInactive * 2);
        
        // Deduct points for low member count
        if (group.Members.Count < 2)
            score -= 30;
        else if (group.Members.Count < 3)
            score -= 15;
        
        return Math.Max(0, score);
    }

    private async Task<bool> IsGroupUsageBalancedAsync(Guid groupId)
    {
        // This would need to be implemented based on your usage tracking
        // For now, return true as a placeholder
        await Task.CompletedTask;
        return true;
    }

    private string GetGroupHealthRecommendation(OwnershipGroup group)
    {
        var daysInactive = (DateTime.UtcNow - group.UpdatedAt).TotalDays;
        
        if (daysInactive > 30)
            return "Group appears dormant. Consider reaching out to members or dissolving the group.";
        if (group.Members.Count < 2)
            return "Group needs more members to function effectively.";
        if (daysInactive > 14)
            return "Group has been inactive. Consider sending a reminder to members.";
        
        return "Group is healthy and functioning well.";
    }

    private async Task<GroupFinancialSummaryDto> GetGroupFinancialSummaryAsync(Guid groupId)
    {
        var expenses = await _context.Expenses
            .Where(e => e.GroupId == groupId)
            .ToListAsync();

        var totalExpenses = expenses.Sum(e => e.Amount);
        var monthlyExpenses = expenses
            .Where(e => e.CreatedAt >= DateTime.UtcNow.AddDays(-30))
            .Sum(e => e.Amount);

        var memberCount = await _context.GroupMembers.CountAsync(gm => gm.GroupId == groupId);
        var averageExpensePerMember = memberCount > 0 ? totalExpenses / memberCount : 0;

        return new GroupFinancialSummaryDto
        {
            TotalExpenses = totalExpenses,
            FundBalance = 0, // This would need to be calculated based on your fund system
            MonthlyExpenses = monthlyExpenses,
            AverageExpensePerMember = averageExpensePerMember,
            TotalRevenue = 0, // This would need to be calculated
            NetBalance = 0, // This would need to be calculated
            ExpenseCategories = expenses
                .GroupBy(e => e.ExpenseType.ToString())
                .Select(g => new GroupExpenseCategoryDto
                {
                    Category = g.Key,
                    Amount = g.Sum(e => e.Amount),
                    Count = g.Count(),
                    Percentage = totalExpenses > 0 ? (g.Sum(e => e.Amount) / totalExpenses) * 100 : 0
                })
                .ToList()
        };
    }

    private async Task<GroupBookingStatisticsDto> GetGroupBookingStatisticsAsync(Guid groupId)
    {
        var bookings = await _context.Bookings
            .Where(b => b.GroupId == groupId)
            .ToListAsync();

        var totalBookings = bookings.Count;
        var completedBookings = bookings.Count(b => b.Status == BookingStatus.Completed);
        var cancelledBookings = bookings.Count(b => b.Status == BookingStatus.Cancelled);
        var activeBookings = bookings.Count(b => b.Status == BookingStatus.InProgress);
        var totalRevenue = 0m; // This would need to be calculated based on your payment system

        var thisMonth = DateTime.UtcNow.AddDays(-30);
        var lastMonth = DateTime.UtcNow.AddDays(-60);
        var bookingsThisMonth = bookings.Count(b => b.CreatedAt >= thisMonth);
        var bookingsLastMonth = bookings.Count(b => b.CreatedAt >= lastMonth && b.CreatedAt < thisMonth);

        var bookingGrowthPercentage = bookingsLastMonth > 0 
            ? ((bookingsThisMonth - bookingsLastMonth) / (double)bookingsLastMonth) * 100 
            : 0;

        return new GroupBookingStatisticsDto
        {
            TotalBookings = totalBookings,
            CompletedBookings = completedBookings,
            CancelledBookings = cancelledBookings,
            ActiveBookings = activeBookings,
            TotalRevenue = totalRevenue,
            AverageBookingValue = totalBookings > 0 ? totalRevenue / totalBookings : 0,
            BookingsThisMonth = bookingsThisMonth,
            BookingsLastMonth = bookingsLastMonth,
            BookingGrowthPercentage = (decimal)bookingGrowthPercentage
        };
    }

    private async Task<List<GroupActivityDto>> GetGroupRecentActivityAsync(Guid groupId)
    {
        return await _context.AuditLogs
            .Where(a => a.Entity == "OwnershipGroup" && a.EntityId == groupId)
            .Include(a => a.User)
            .OrderByDescending(a => a.Timestamp)
            .Take(20)
            .Select(a => new GroupActivityDto
            {
                Id = a.Id,
                Action = a.Action,
                Entity = a.Entity,
                Details = a.Details,
                UserName = a.User.FirstName + " " + a.User.LastName,
                Timestamp = a.Timestamp
            })
            .ToListAsync();
    }

    private async Task HandleGroupStatusChangeImplicationsAsync(Guid groupId, GroupStatus oldStatus, GroupStatus newStatus)
    {
        switch (newStatus)
        {
            case GroupStatus.Inactive:
                // Cancel future bookings
                var futureBookings = await _context.Bookings
                    .Where(b => b.GroupId == groupId && b.Status == BookingStatus.Confirmed && b.StartAt > DateTime.UtcNow)
                    .ToListAsync();
                
                foreach (var booking in futureBookings)
                {
                    booking.Status = BookingStatus.Cancelled;
                }
                break;
                
            case GroupStatus.Dissolved:
                // Cancel all future bookings and freeze all activities
                var allFutureBookings = await _context.Bookings
                    .Where(b => b.GroupId == groupId && b.StartAt > DateTime.UtcNow)
                    .ToListAsync();
                
                foreach (var booking in allFutureBookings)
                {
                    booking.Status = BookingStatus.Cancelled;
                }
                break;
        }
    }

    // Dispute Management Methods
    public async Task<Guid> CreateDisputeAsync(CreateDisputeDto request, Guid adminUserId)
    {
        try
        {
            // Validate group exists
            var group = await _context.OwnershipGroups
                .FirstOrDefaultAsync(g => g.Id == request.GroupId);
            if (group == null)
                throw new ArgumentException("Group not found");

            // Determine reporter - use provided or admin
            var reporterId = request.ReportedBy ?? adminUserId;

            // Validate reporter is member of group (if not admin creating on behalf)
            if (request.ReportedBy.HasValue)
            {
                var isMember = await _context.GroupMembers
                    .AnyAsync(gm => gm.GroupId == request.GroupId && gm.UserId == reporterId);
                if (!isMember)
                    throw new ArgumentException("User is not a member of the specified group");
            }

            // Auto-assign based on workload (optional)
            var assignedTo = await GetLeastBusyStaffAsync();

            var dispute = new Dispute
            {
                Id = Guid.NewGuid(),
                GroupId = request.GroupId,
                ReportedBy = reporterId,
                Subject = request.Subject,
                Description = request.Description,
                Category = request.Category,
                Priority = request.Priority,
                Status = DisputeStatus.Open,
                AssignedTo = assignedTo,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Disputes.Add(dispute);

            // Create audit log
            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                Action = "DisputeCreated",
                Entity = "Dispute",
                EntityId = dispute.Id,
                Details = $"Dispute created: {request.Subject}",
                PerformedBy = adminUserId,
                Timestamp = DateTime.UtcNow,
                IpAddress = "Admin API",
                UserAgent = "Admin API"
            };
            _context.AuditLogs.Add(auditLog);

            await _context.SaveChangesAsync();

            // TODO: Publish DisputeCreatedEvent
            // TODO: Send notification to admins

            return dispute.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating dispute for group {GroupId}", request.GroupId);
            throw;
        }
    }

    public async Task<DisputeListResponseDto> GetDisputesAsync(DisputeListRequestDto request)
    {
        try
        {
            var query = _context.Disputes
                .Include(d => d.Group)
                .Include(d => d.Reporter)
                .Include(d => d.AssignedStaff)
                .AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(request.Search))
            {
                query = query.Where(d => d.Subject.Contains(request.Search) || 
                                   d.Description.Contains(request.Search) ||
                                   d.Group.Name.Contains(request.Search));
            }

            if (request.Status.HasValue)
                query = query.Where(d => d.Status == request.Status.Value);

            if (request.Priority.HasValue)
                query = query.Where(d => d.Priority == request.Priority.Value);

            if (request.Category.HasValue)
                query = query.Where(d => d.Category == request.Category.Value);

            if (request.AssignedTo.HasValue)
                query = query.Where(d => d.AssignedTo == request.AssignedTo.Value);

            if (request.GroupId.HasValue)
                query = query.Where(d => d.GroupId == request.GroupId.Value);

            // Apply sorting
            query = request.SortBy.ToLower() switch
            {
                "subject" => request.SortDirection.ToLower() == "asc" 
                    ? query.OrderBy(d => d.Subject) 
                    : query.OrderByDescending(d => d.Subject),
                "priority" => request.SortDirection.ToLower() == "asc" 
                    ? query.OrderBy(d => d.Priority) 
                    : query.OrderByDescending(d => d.Priority),
                "status" => request.SortDirection.ToLower() == "asc" 
                    ? query.OrderBy(d => d.Status) 
                    : query.OrderByDescending(d => d.Status),
                "createdat" => request.SortDirection.ToLower() == "asc" 
                    ? query.OrderBy(d => d.CreatedAt) 
                    : query.OrderByDescending(d => d.CreatedAt),
                _ => query.OrderByDescending(d => d.CreatedAt)
            };

            var totalCount = await query.CountAsync();

            var disputes = await query
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(d => new DisputeSummaryDto
                {
                    Id = d.Id,
                    GroupId = d.GroupId,
                    GroupName = d.Group.Name,
                    Subject = d.Subject,
                    Category = d.Category,
                    Priority = d.Priority,
                    Status = d.Status,
                    ReporterName = d.Reporter.FirstName + " " + d.Reporter.LastName,
                    AssignedToName = d.AssignedStaff != null ? d.AssignedStaff.FirstName + " " + d.AssignedStaff.LastName : null,
                    CreatedAt = d.CreatedAt,
                    ResolvedAt = d.ResolvedAt,
                    CommentCount = d.Comments.Count
                })
                .ToListAsync();

            return new DisputeListResponseDto
            {
                Disputes = disputes,
                TotalCount = totalCount,
                Page = request.Page,
                PageSize = request.PageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / request.PageSize)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving disputes");
            throw;
        }
    }

    public async Task<DisputeDetailsDto> GetDisputeDetailsAsync(Guid disputeId)
    {
        try
        {
            var dispute = await _context.Disputes
                .Include(d => d.Group)
                .Include(d => d.Reporter)
                .Include(d => d.AssignedStaff)
                .Include(d => d.Resolver)
                .Include(d => d.Comments)
                    .ThenInclude(c => c.Commenter)
                .FirstOrDefaultAsync(d => d.Id == disputeId);

            if (dispute == null)
                throw new ArgumentException("Dispute not found");

            // Get audit trail for this dispute
            var actions = await _context.AuditLogs
                .Where(al => al.Entity == "Dispute" && al.EntityId == disputeId)
                .OrderBy(al => al.Timestamp)
                .Select(al => new DisputeActionDto
                {
                    Action = al.Action,
                    Details = al.Details,
                    UserName = "Admin User", // Placeholder
                    Timestamp = al.Timestamp
                })
                .ToListAsync();

            return new DisputeDetailsDto
            {
                Id = dispute.Id,
                GroupId = dispute.GroupId,
                GroupName = dispute.Group.Name,
                Subject = dispute.Subject,
                Description = dispute.Description,
                Category = dispute.Category,
                Priority = dispute.Priority,
                Status = dispute.Status,
                ReporterName = dispute.Reporter.FirstName + " " + dispute.Reporter.LastName,
                ReporterEmail = dispute.Reporter.Email,
                AssignedToName = dispute.AssignedStaff != null ? dispute.AssignedStaff.FirstName + " " + dispute.AssignedStaff.LastName : null,
                AssignedToEmail = dispute.AssignedStaff?.Email,
                Resolution = dispute.Resolution,
                ResolverName = dispute.Resolver != null ? dispute.Resolver.FirstName + " " + dispute.Resolver.LastName : null,
                CreatedAt = dispute.CreatedAt,
                UpdatedAt = dispute.UpdatedAt,
                ResolvedAt = dispute.ResolvedAt,
                Comments = dispute.Comments.Select(c => new DisputeCommentDto
                {
                    Id = c.Id,
                    Comment = c.Comment,
                    CommenterName = c.Commenter.FirstName + " " + c.Commenter.LastName,
                    CommenterEmail = c.Commenter.Email,
                    IsInternal = c.IsInternal,
                    CreatedAt = c.CreatedAt
                }).ToList(),
                Actions = actions
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving dispute details for {DisputeId}", disputeId);
            throw;
        }
    }

    public async Task<bool> AssignDisputeAsync(Guid disputeId, AssignDisputeDto request, Guid adminUserId)
    {
        try
        {
            var dispute = await _context.Disputes.FindAsync(disputeId);
            if (dispute == null)
                throw new ArgumentException("Dispute not found");

            // Validate assigned user exists and is staff
            var assignedUser = await _context.Users.FindAsync(request.AssignedTo);
            if (assignedUser == null)
                throw new ArgumentException("Assigned user not found");

            var oldAssignee = dispute.AssignedTo;
            dispute.AssignedTo = request.AssignedTo;
            dispute.Status = DisputeStatus.UnderReview;
            dispute.UpdatedAt = DateTime.UtcNow;

            // Create audit log
            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                Action = "DisputeAssigned",
                Entity = "Dispute",
                EntityId = disputeId,
                Details = $"Dispute assigned to {assignedUser.FirstName} {assignedUser.LastName}",
                PerformedBy = adminUserId,
                Timestamp = DateTime.UtcNow,
                IpAddress = "Admin API",
                UserAgent = "Admin API"
            };
            _context.AuditLogs.Add(auditLog);

            await _context.SaveChangesAsync();

            // TODO: Send notification to assigned staff

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning dispute {DisputeId}", disputeId);
            throw;
        }
    }

    public async Task<bool> AddDisputeCommentAsync(Guid disputeId, AddDisputeCommentDto request, Guid userId)
    {
        try
        {
            var dispute = await _context.Disputes.FindAsync(disputeId);
            if (dispute == null)
                throw new ArgumentException("Dispute not found");

            // Validate user has access to this dispute
            var hasAccess = await _context.GroupMembers
                .AnyAsync(gm => gm.GroupId == dispute.GroupId && gm.UserId == userId);
            
            if (!hasAccess)
            {
                // Check if user is staff/admin
                var user = await _context.Users.FindAsync(userId);
                if (user == null || (user.Role != UserRole.SystemAdmin && user.Role != UserRole.Staff))
                    throw new UnauthorizedAccessException("User does not have access to this dispute");
            }

            var comment = new DisputeComment
            {
                Id = Guid.NewGuid(),
                DisputeId = disputeId,
                CommentedBy = userId,
                Comment = request.Comment,
                IsInternal = request.IsInternal,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.DisputeComments.Add(comment);

            // Update dispute updated time
            dispute.UpdatedAt = DateTime.UtcNow;

            // Create audit log
            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                Action = "CommentAdded",
                Entity = "Dispute",
                EntityId = disputeId,
                Details = "Comment added to dispute",
                PerformedBy = userId,
                Timestamp = DateTime.UtcNow,
                IpAddress = "Admin API",
                UserAgent = "Admin API"
            };
            _context.AuditLogs.Add(auditLog);

            await _context.SaveChangesAsync();

            // TODO: Send notification to participants

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding comment to dispute {DisputeId}", disputeId);
            throw;
        }
    }

    public async Task<bool> ResolveDisputeAsync(Guid disputeId, ResolveDisputeDto request, Guid adminUserId)
    {
        try
        {
            var dispute = await _context.Disputes.FindAsync(disputeId);
            if (dispute == null)
                throw new ArgumentException("Dispute not found");

            dispute.Status = DisputeStatus.Resolved;
            dispute.Resolution = request.Resolution;
            dispute.ResolvedBy = adminUserId;
            dispute.ResolvedAt = DateTime.UtcNow;
            dispute.UpdatedAt = DateTime.UtcNow;

            // Create audit log
            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                Action = "DisputeResolved",
                Entity = "Dispute",
                EntityId = disputeId,
                Details = $"Dispute resolved: {request.Resolution}",
                PerformedBy = adminUserId,
                Timestamp = DateTime.UtcNow,
                IpAddress = "Admin API",
                UserAgent = "Admin API"
            };
            _context.AuditLogs.Add(auditLog);

            await _context.SaveChangesAsync();

            // TODO: Publish DisputeResolvedEvent
            // TODO: Send notification to all participants

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving dispute {DisputeId}", disputeId);
            throw;
        }
    }

    public async Task<DisputeStatisticsDto> GetDisputeStatisticsAsync()
    {
        try
        {
            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);

            var disputes = await _context.Disputes.ToListAsync();

            var resolvedDisputes = disputes.Where(d => d.Status == DisputeStatus.Resolved).ToList();
            var averageResolutionTime = resolvedDisputes.Any()
                ? resolvedDisputes.Where(d => d.ResolvedAt.HasValue)
                    .Average(d => (d.ResolvedAt.Value - d.CreatedAt).TotalHours)
                : 0;

            return new DisputeStatisticsDto
            {
                TotalDisputes = disputes.Count,
                OpenDisputes = disputes.Count(d => d.Status == DisputeStatus.Open),
                UnderReviewDisputes = disputes.Count(d => d.Status == DisputeStatus.UnderReview),
                ResolvedDisputes = disputes.Count(d => d.Status == DisputeStatus.Resolved),
                ClosedDisputes = disputes.Count(d => d.Status == DisputeStatus.Closed),
                EscalatedDisputes = disputes.Count(d => d.Status == DisputeStatus.Escalated),
                UrgentDisputes = disputes.Count(d => d.Priority == DisputePriority.Urgent),
                HighPriorityDisputes = disputes.Count(d => d.Priority == DisputePriority.High),
                VehicleDamageDisputes = disputes.Count(d => d.Category == DisputeCategory.VehicleDamage),
                LateFeesDisputes = disputes.Count(d => d.Category == DisputeCategory.LateFees),
                UsageDisputes = disputes.Count(d => d.Category == DisputeCategory.Usage),
                FinancialDisputes = disputes.Count(d => d.Category == DisputeCategory.Financial),
                OtherDisputes = disputes.Count(d => d.Category == DisputeCategory.Other),
                AverageResolutionTimeHours = averageResolutionTime,
                DisputesResolvedThisMonth = disputes.Count(d => d.Status == DisputeStatus.Resolved && d.ResolvedAt >= startOfMonth),
                DisputesCreatedThisMonth = disputes.Count(d => d.CreatedAt >= startOfMonth)
            };
        }
        catch (Microsoft.Data.SqlClient.SqlException sex) when (sex.Number == 208)
        {
            // Disputes table missing: return empty stats instead of failing in environments not yet migrated
            _logger.LogWarning("Disputes table not found. Returning empty statistics.");
            return new DisputeStatisticsDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving dispute statistics");
            throw;
        }
    }

    private async Task<Guid?> GetLeastBusyStaffAsync()
    {
        try
        {
            // Get staff members with their dispute counts
            var staffWorkload = await _context.Users
                .Where(u => u.Role == UserRole.Staff || u.Role == UserRole.SystemAdmin)
                .Select(u => new
                {
                    UserId = u.Id,
                    OpenDisputes = _context.Disputes.Count(d => d.AssignedTo == u.Id && d.Status == DisputeStatus.Open),
                    UnderReviewDisputes = _context.Disputes.Count(d => d.AssignedTo == u.Id && d.Status == DisputeStatus.UnderReview)
                })
                .ToListAsync();

            // Find staff member with least workload
            var leastBusy = staffWorkload
                .OrderBy(s => s.OpenDisputes + s.UnderReviewDisputes)
                .FirstOrDefault();

            return leastBusy?.UserId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting least busy staff member");
            return null;
        }
    }
}
