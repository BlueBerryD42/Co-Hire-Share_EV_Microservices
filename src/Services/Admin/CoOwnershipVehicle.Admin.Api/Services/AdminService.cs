using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using CoOwnershipVehicle.Admin.Api.Data;
using CoOwnershipVehicle.Admin.Api.Services.HttpClients;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Admin.Api.Services;

public class AdminService : IAdminService
{
    private readonly AdminDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AdminService> _logger;
    private readonly IUserServiceClient _userServiceClient;
    private readonly IGroupServiceClient _groupServiceClient;
    private readonly IVehicleServiceClient _vehicleServiceClient;
    private readonly IBookingServiceClient _bookingServiceClient;
    private readonly IPaymentServiceClient _paymentServiceClient;
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(5);

    public AdminService(
        AdminDbContext context, 
        IMemoryCache cache, 
        ILogger<AdminService> logger,
        IUserServiceClient userServiceClient,
        IGroupServiceClient groupServiceClient,
        IVehicleServiceClient vehicleServiceClient,
        IBookingServiceClient bookingServiceClient,
        IPaymentServiceClient paymentServiceClient)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
        _userServiceClient = userServiceClient;
        _groupServiceClient = groupServiceClient;
        _vehicleServiceClient = vehicleServiceClient;
        _bookingServiceClient = bookingServiceClient;
        _paymentServiceClient = paymentServiceClient;
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

        // Get counts from HTTP clients
        var users = await _userServiceClient.GetUsersAsync();
        var groups = await _groupServiceClient.GetGroupsAsync();
        var vehicles = await _vehicleServiceClient.GetVehiclesAsync();
        var bookings = await _bookingServiceClient.GetBookingsAsync();
        var payments = await _paymentServiceClient.GetPaymentsAsync();

        var userCount = users.Count;
        var groupCount = groups.Count;
        var vehicleCount = vehicles.Count;
        var bookingCount = bookings.Count;
        var paymentCount = payments.Count;
        var expenseCount = 0; // Expenses will be fetched separately if needed

        // Use current timestamp for cache invalidation since we can't easily get latest update times via HTTP
        var now = DateTime.UtcNow;
        var latestUserUpdate = users.OrderByDescending(u => u.CreatedAt).FirstOrDefault()?.CreatedAt ?? now;
        var latestGroupUpdate = groups.OrderByDescending(g => g.CreatedAt).FirstOrDefault()?.CreatedAt ?? now;
        var latestVehicleUpdate = vehicles.OrderByDescending(v => v.CreatedAt).FirstOrDefault()?.CreatedAt ?? now;
        var latestBookingUpdate = bookings.OrderByDescending(b => b.CreatedAt).FirstOrDefault()?.CreatedAt ?? now;
        var latestPaymentUpdate = payments.OrderByDescending(p => p.CreatedAt).FirstOrDefault()?.CreatedAt ?? now;
        var latestExpenseUpdate = now; // Expenses fetched separately

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
        
        var users = await _userServiceClient.GetUsersAsync();
        var totalUsers = users.Count;
        
        // Note: UserProfileDto may not have LockoutEnd, so we'll approximate based on available data
        var activeUsers = totalUsers; // Assume all are active unless we have more info
        var inactiveUsers = 0; // Would need LockoutEnd from User service
        
        var pendingKyc = users.Count(u => u.KycStatus == KycStatus.Pending);
        var approvedKyc = users.Count(u => u.KycStatus == KycStatus.Approved);
        var rejectedKyc = users.Count(u => u.KycStatus == KycStatus.Rejected);

        return (totalUsers, activeUsers, inactiveUsers, pendingKyc, approvedKyc, rejectedKyc);
    }

    private async Task<(int Current, int Previous, int ThisMonth, int ThisWeek)> GetPeriodUserCountsAsync(DateTime periodStart, DateTime previousPeriodStart, DateTime now)
    {
        var users = await _userServiceClient.GetUsersAsync();
        
        var newUsersThisPeriod = users.Count(u => u.CreatedAt >= periodStart);
        var newUsersPreviousPeriod = users.Count(u => u.CreatedAt >= previousPeriodStart && u.CreatedAt < periodStart);
        var newUsersThisMonth = users.Count(u => u.CreatedAt >= now.AddDays(-30));
        var newUsersThisWeek = users.Count(u => u.CreatedAt >= now.AddDays(-7));

        return (newUsersThisPeriod, newUsersPreviousPeriod, newUsersThisMonth, newUsersThisWeek);
    }

    private async Task<GroupMetricsDto> GetGroupMetricsAsync(TimePeriod period)
    {
        var now = DateTime.UtcNow;
        var periodStart = GetPeriodStart(now, period);
        var previousPeriodStart = GetPeriodStart(periodStart.AddDays(-1), period);

        var groups = await _groupServiceClient.GetGroupsAsync();
        var totalGroups = groups.Count;
        var activeGroups = groups.Count(g => g.Status == GroupStatus.Active);
        var inactiveGroups = groups.Count(g => g.Status == GroupStatus.Inactive);
        var dissolvedGroups = groups.Count(g => g.Status == GroupStatus.Dissolved);

        var newGroupsThisPeriod = groups.Count(g => g.CreatedAt >= periodStart);
        var newGroupsPreviousPeriod = groups.Count(g => g.CreatedAt >= previousPeriodStart && g.CreatedAt < periodStart);

        var growthPercentage = CalculateGrowthPercentage(newGroupsPreviousPeriod, newGroupsThisPeriod);

        return new GroupMetricsDto
        {
            TotalGroups = totalGroups,
            ActiveGroups = activeGroups,
            InactiveGroups = inactiveGroups,
            DissolvedGroups = dissolvedGroups,
            GroupGrowthPercentage = growthPercentage,
            NewGroupsThisMonth = groups.Count(g => g.CreatedAt >= now.AddDays(-30)),
            NewGroupsThisWeek = groups.Count(g => g.CreatedAt >= now.AddDays(-7))
        };
    }

    private async Task<VehicleMetricsDto> GetVehicleMetricsAsync(TimePeriod period)
    {
        var now = DateTime.UtcNow;
        var periodStart = GetPeriodStart(now, period);
        var previousPeriodStart = GetPeriodStart(periodStart.AddDays(-1), period);

        var vehicles = await _vehicleServiceClient.GetVehiclesAsync();
        var totalVehicles = vehicles.Count;
        var availableVehicles = vehicles.Count(v => v.Status == VehicleStatus.Available);
        var inUseVehicles = vehicles.Count(v => v.Status == VehicleStatus.InUse);
        var maintenanceVehicles = vehicles.Count(v => v.Status == VehicleStatus.Maintenance);
        var unavailableVehicles = vehicles.Count(v => v.Status == VehicleStatus.Unavailable);

        var newVehiclesThisPeriod = vehicles.Count(v => v.CreatedAt >= periodStart);
        var newVehiclesPreviousPeriod = vehicles.Count(v => v.CreatedAt >= previousPeriodStart && v.CreatedAt < periodStart);

        var growthPercentage = CalculateGrowthPercentage(newVehiclesPreviousPeriod, newVehiclesThisPeriod);

        return new VehicleMetricsDto
        {
            TotalVehicles = totalVehicles,
            AvailableVehicles = availableVehicles,
            InUseVehicles = inUseVehicles,
            MaintenanceVehicles = maintenanceVehicles,
            UnavailableVehicles = unavailableVehicles,
            VehicleGrowthPercentage = growthPercentage,
            NewVehiclesThisMonth = vehicles.Count(v => v.CreatedAt >= now.AddDays(-30)),
            NewVehiclesThisWeek = vehicles.Count(v => v.CreatedAt >= now.AddDays(-7))
        };
    }

    private async Task<BookingMetricsDto> GetBookingMetricsAsync(TimePeriod period)
    {
        var now = DateTime.UtcNow;
        var periodStart = GetPeriodStart(now, period);
        var previousPeriodStart = GetPeriodStart(periodStart.AddDays(-1), period);

        var bookings = await _bookingServiceClient.GetBookingsAsync();
        var totalBookings = bookings.Count;
        var pendingBookings = bookings.Count(b => b.Status == BookingStatus.Pending);
        var confirmedBookings = bookings.Count(b => b.Status == BookingStatus.Confirmed);
        var inProgressBookings = bookings.Count(b => b.Status == BookingStatus.InProgress);
        var completedBookings = bookings.Count(b => b.Status == BookingStatus.Completed);
        var cancelledBookings = bookings.Count(b => b.Status == BookingStatus.Cancelled);
        var activeTrips = bookings.Count(b => b.Status == BookingStatus.InProgress);

        var newBookingsThisPeriod = bookings.Count(b => b.CreatedAt >= periodStart);
        var newBookingsPreviousPeriod = bookings.Count(b => b.CreatedAt >= previousPeriodStart && b.CreatedAt < periodStart);

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
            NewBookingsThisMonth = bookings.Count(b => b.CreatedAt >= now.AddDays(-30)),
            NewBookingsThisWeek = bookings.Count(b => b.CreatedAt >= now.AddDays(-7))
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
        var allPayments = await _paymentServiceClient.GetPaymentsAsync(status: PaymentStatus.Completed);
        
        var totalRevenue = allPayments.Sum(p => p.Amount);
        var monthlyRevenue = allPayments.Where(p => p.PaidAt >= now.AddDays(-30)).Sum(p => p.Amount);
        var weeklyRevenue = allPayments.Where(p => p.PaidAt >= now.AddDays(-7)).Sum(p => p.Amount);
        var dailyRevenue = allPayments.Where(p => p.PaidAt >= now.AddDays(-1)).Sum(p => p.Amount);
        var revenueThisPeriod = allPayments.Where(p => p.PaidAt >= periodStart).Sum(p => p.Amount);
        var revenuePreviousPeriod = allPayments.Where(p => p.PaidAt >= previousPeriodStart && p.PaidAt < periodStart).Sum(p => p.Amount);

        return (totalRevenue, monthlyRevenue, weeklyRevenue, dailyRevenue, revenueThisPeriod, revenuePreviousPeriod);
    }

    public async Task<SystemHealthDto> GetSystemHealthAsync()
    {
        var now = DateTime.UtcNow;
        
        // Check database connection (Admin DB only)
        var databaseConnected = await _context.Database.CanConnectAsync();
        
        // Count pending approvals via HTTP clients
        var bookings = await _bookingServiceClient.GetBookingsAsync();
        var users = await _userServiceClient.GetUsersAsync();
        var pendingApprovals = bookings.Count(b => b.Status == BookingStatus.PendingApproval) +
                              users.Count(u => u.KycStatus == KycStatus.InReview);
        
        // Count overdue maintenance (vehicles that haven't been serviced in 6 months)
        var vehicles = await _vehicleServiceClient.GetVehiclesAsync();
        var overdueMaintenance = vehicles.Count(v => 
            v.LastServiceDate == null || v.LastServiceDate < now.AddMonths(-6));
        
        // Count disputes (from Admin DB)
        var disputes = await _context.Disputes.CountAsync(d => d.Status == DisputeStatus.Open || d.Status == DisputeStatus.UnderReview);
        
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
        var auditLogs = await _context.AuditLogs
            .OrderByDescending(a => a.Timestamp)
            .Take(count)
            .ToListAsync();

        // Fetch user names via User service
        var userIds = auditLogs.Select(a => a.PerformedBy).Distinct().ToList();
        var users = await _userServiceClient.GetUsersAsync();
        var userMap = users.Where(u => userIds.Contains(u.Id)).ToDictionary(u => u.Id, u => $"{u.FirstName} {u.LastName}");

        return auditLogs.Select(a => new ActivityFeedItemDto
        {
            Id = a.Id,
            Entity = a.Entity,
            Action = a.Action,
            UserName = userMap.GetValueOrDefault(a.PerformedBy, "Unknown"),
            Timestamp = a.Timestamp,
            Details = a.Details
        }).ToList();
    }

    public async Task<List<AlertDto>> GetAlertsAsync()
    {
        var alerts = new List<AlertDto>();
        var now = DateTime.UtcNow;

        // Overdue maintenance alerts
        var vehicles = await _vehicleServiceClient.GetVehiclesAsync();
        var overdueMaintenance = vehicles.Count(v => v.LastServiceDate == null || v.LastServiceDate < now.AddMonths(-6));

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
        var users = await _userServiceClient.GetUsersAsync();
        var pendingKyc = users.Count(u => u.KycStatus == KycStatus.Pending);
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
        var bookings = await _bookingServiceClient.GetBookingsAsync();
        var pendingBookings = bookings.Count(b => b.Status == BookingStatus.PendingApproval);
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
        var yearStart = new DateTime(now.Year, 1, 1);
        var rollingMonthStart = now.AddDays(-30);
        var weekStart = now.AddDays(-7);
        var dayStart = now.AddDays(-1);

        // Get payments via HTTP client
        var allPayments = await _paymentServiceClient.GetPaymentsAsync(status: PaymentStatus.Completed);
        var allTimeRevenue = allPayments.Sum(p => p.Amount);
        var yearRevenue = allPayments.Where(p => p.PaidAt >= yearStart).Sum(p => p.Amount);
        var monthRevenue = allPayments.Where(p => p.PaidAt >= rollingMonthStart).Sum(p => p.Amount);
        var weekRevenue = allPayments.Where(p => p.PaidAt >= weekStart).Sum(p => p.Amount);
        var dayRevenue = allPayments.Where(p => p.PaidAt >= dayStart).Sum(p => p.Amount);

        // Revenue by source - simplified (would need Payment service to provide more details)
        var revenueBySource = new List<KeyValuePair<string, decimal>>();
        revenueBySource.Add(new KeyValuePair<string, decimal>("Payments", allTimeRevenue));
        // Note: Ledger entries not available via HTTP - would need Payment service to expose this

        // Get expenses via HTTP client
        var allExpenses = await _paymentServiceClient.GetExpensesAsync();
        var totalExpenses = allExpenses.Sum(e => e.Amount);

        // Total fund balances - not available via HTTP (would need Payment service endpoint)
        var totalFundBalances = 0m;

        // Payment statistics
        var allPaymentsAllStatuses = await _paymentServiceClient.GetPaymentsAsync();
        var totalPayments = allPaymentsAllStatuses.Count;
        var successPayments = allPaymentsAllStatuses.Count(p => p.Status == PaymentStatus.Completed);
        var failedPaymentsCount = allPaymentsAllStatuses.Count(p => p.Status == PaymentStatus.Failed);
        var failedPaymentsAmount = allPaymentsAllStatuses.Where(p => p.Status == PaymentStatus.Failed).Sum(p => p.Amount);
        var pendingPayments = allPaymentsAllStatuses.Count(p => p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.Processing);
        var successRate = totalPayments == 0 ? 0 : (double)successPayments / totalPayments * 100d;

        // Revenue trend (last 30 days)
        var trendStart = now.AddDays(-30).Date;
        var revenueTrend = allPayments
            .Where(p => p.PaidAt >= trendStart)
            .GroupBy(p => p.PaidAt!.Value.Date)
            .Select(g => new TimeSeriesPointDto<decimal> { Date = g.Key, Value = g.Sum(p => p.Amount) })
            .OrderBy(x => x.Date)
            .ToList();

        // Top spending groups (by expenses)
        var groups = await _groupServiceClient.GetGroupsAsync();
        var topGroups = allExpenses
            .GroupBy(e => e.GroupId)
            .Select(g => new { GroupId = g.Key, Total = g.Sum(x => x.Amount) })
            .OrderByDescending(x => x.Total)
            .Take(10)
            .Join(groups, g => g.GroupId, og => og.Id, (g, og) => new GroupSpendSummaryDto { GroupId = g.GroupId, GroupName = og.Name, TotalExpenses = g.Total })
            .ToList();

        // Financial health score (simplified)
        var groupsCount = groups.Count;
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
            RevenueTrend = revenueTrend,
            TopSpendingGroups = topGroups,
            FinancialHealthScore = healthScore
        };
    }

    public async Task<FinancialGroupBreakdownDto> GetFinancialByGroupsAsync()
    {
        var groups = await _groupServiceClient.GetGroupsAsync();
        var expenses = await _paymentServiceClient.GetExpensesAsync();
        var payments = await _paymentServiceClient.GetPaymentsAsync();

        var items = new List<GroupFinancialItemDto>();
        foreach (var g in groups)
        {
            var gExpenses = expenses.Where(e => e.GroupId == g.Id).ToList();
            var byType = gExpenses
                .GroupBy(e => e.ExpenseType)
                .ToDictionary(k => k.Key.ToString(), v => v.Sum(x => x.Amount));
            
            // Fund balance not available via HTTP - would need Payment service endpoint
            var balance = 0m;

            // Payment compliance - simplified (would need invoice mapping from Payment service)
            var groupPayments = payments.Where(p => gExpenses.Any(e => e.GroupId == g.Id)).ToList();
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
        var all = await _paymentServiceClient.GetPaymentsAsync();
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
        var expenses = await _paymentServiceClient.GetExpensesAsync();
        var expenseRows = expenses.Select(e => new { e.ExpenseType, e.Amount, CreatedAt = e.DateIncurred, e.GroupId }).ToList();

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

        var vehicles = await _vehicleServiceClient.GetVehiclesAsync();
        var vehicleCount = vehicles.Count;
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
        var allPayments = await _paymentServiceClient.GetPaymentsAsync(status: PaymentStatus.Completed);
        var completed = allPayments.ToList();
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
        var allPaymentsAllStatuses = await _paymentServiceClient.GetPaymentsAsync();
        var suspicious = allPaymentsAllStatuses
            .Where(p => p.Status == PaymentStatus.Failed && p.CreatedAt >= sevenDaysAgo)
            .GroupBy(p => p.PayerId)
            .Select(g => new SuspiciousPatternDto { PayerId = g.Key, FailedCount7Days = g.Count() })
            .Where(x => x.FailedCount7Days >= 3)
            .ToList();

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
        // Note: Ledger entries not available via HTTP - would need Payment service to expose group balances
        // For now, return empty list - this functionality would need to be implemented in Payment service
        var groups = await _groupServiceClient.GetGroupsAsync();
        return new List<GroupNegativeBalanceDto>();
    }

    // Note: DetermineOverallKycStatusAsync and ApplyUserKycStatusChange are no longer used
    // KYC status is now managed via User service HTTP calls
    // These methods are kept for reference but should be removed if not needed

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
        _logger.LogInformation("GetUsersAsync called with request: Page={Page}, PageSize={PageSize}, Search={Search}, Role={Role}, KycStatus={KycStatus}", 
            request.Page, request.PageSize, request.Search, request.Role, request.KycStatus);
        
        // Get ALL users from User service first (without pagination), then filter and paginate in Admin service
        // This ensures we have all data to filter properly
        // Note: User service will handle its own filtering, we just need to get all pages
        var requestWithoutPagination = new UserListRequestDto
        {
            // Don't pass filters to User service - let it return all users, we'll filter here
            Page = 1,
            PageSize = 10000 // Get all users
        };
        
        var allUsers = await _userServiceClient.GetUsersAsync(requestWithoutPagination);
        
        _logger.LogInformation("Received {Count} users from UserServiceClient", allUsers.Count);
        
        // Apply filters in memory
        var filtered = allUsers.AsEnumerable();
        
        if (!string.IsNullOrEmpty(request.Search))
        {
            var searchTerm = request.Search.ToLower();
            filtered = filtered.Where(u => 
                (u.FirstName?.ToLower().Contains(searchTerm) ?? false) ||
                (u.LastName?.ToLower().Contains(searchTerm) ?? false) ||
                (u.Email?.ToLower().Contains(searchTerm) ?? false) ||
                (u.Phone != null && u.Phone.Contains(searchTerm)));
        }

        if (request.Role.HasValue)
        {
            filtered = filtered.Where(u => u.Role == request.Role.Value);
        }

        if (request.KycStatus.HasValue)
        {
            filtered = filtered.Where(u => u.KycStatus == request.KycStatus.Value);
        }

        // Account status filter - note: UserProfileDto may not have LockoutEnd, so this may not work perfectly
        // Would need User service to expose account status or LockoutEnd

        // Apply sorting
        filtered = request.SortBy?.ToLower() switch
        {
            "email" => request.SortDirection == "asc" ? filtered.OrderBy(u => u.Email) : filtered.OrderByDescending(u => u.Email),
            "firstname" => request.SortDirection == "asc" ? filtered.OrderBy(u => u.FirstName) : filtered.OrderByDescending(u => u.FirstName),
            "lastname" => request.SortDirection == "asc" ? filtered.OrderBy(u => u.LastName) : filtered.OrderByDescending(u => u.LastName),
            "role" => request.SortDirection == "asc" ? filtered.OrderBy(u => u.Role) : filtered.OrderByDescending(u => u.Role),
            "kycstatus" => request.SortDirection == "asc" ? filtered.OrderBy(u => u.KycStatus) : filtered.OrderByDescending(u => u.KycStatus),
            _ => request.SortDirection == "asc" ? filtered.OrderBy(u => u.CreatedAt) : filtered.OrderByDescending(u => u.CreatedAt)
        };

        var totalCount = filtered.Count();
        var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);

        var users = filtered
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
                AccountStatus = UserAccountStatus.Active, // Would need User service to provide this
                CreatedAt = u.CreatedAt,
                LastLoginAt = null // This would need to be tracked separately
            })
            .ToList();

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
        // Get user profile via HTTP client
        var userProfile = await _userServiceClient.GetUserProfileAsync(userId);
        if (userProfile == null)
            throw new ArgumentException("User not found");

        // Get group memberships via Group service (would need Group service endpoint for user's groups)
        var groups = await _groupServiceClient.GetGroupsAsync();
        var groupMemberships = new List<GroupMembershipDto>(); // Would need Group service to provide user's group memberships

        var statistics = await GetUserStatisticsAsync(userId);

        // Get KYC documents via User service
        var kycDocuments = await _userServiceClient.GetPendingKycDocumentsAsync();
        var userKycDocs = kycDocuments.Where(d => d.UserId == userId).ToList();

        // Get recent activity from Admin audit logs (Admin-specific)
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
            Id = userProfile.Id,
            Email = userProfile.Email,
            FirstName = userProfile.FirstName,
            LastName = userProfile.LastName,
            Phone = userProfile.Phone,
            Address = null, // Would need User service to expose full profile
            City = null,
            Country = null,
            PostalCode = null,
            DateOfBirth = null,
            Role = userProfile.Role,
            KycStatus = userProfile.KycStatus,
            AccountStatus = UserAccountStatus.Active, // Would need User service to provide this
            CreatedAt = userProfile.CreatedAt,
            UpdatedAt = userProfile.CreatedAt, // Would need User service to expose UpdatedAt
            LastLoginAt = null, // This would need to be tracked separately
            GroupMemberships = groupMemberships,
            Statistics = statistics,
            KycDocuments = userKycDocs,
            RecentActivity = recentActivity
        };
    }

    public async Task<bool> UpdateUserStatusAsync(Guid userId, UpdateUserStatusDto request, Guid adminUserId)
    {
        // TODO: This method needs User service endpoint PUT /api/User/users/{userId}/status
        // For now, this operation cannot be performed via HTTP - User service needs to expose this endpoint
        // The User service should handle the status update and return success/failure
        
        _logger.LogWarning("UpdateUserStatusAsync called but User service endpoint not available. UserId: {UserId}, Status: {Status}", userId, request.Status);
        
        // Create audit log in Admin DB
        var now = DateTime.UtcNow;
        var auditLog = new AuditLog
        {
            Entity = "User",
            EntityId = userId,
            Action = "StatusUpdated",
            Details = $"Status update requested: {request.Status}. Reason: {request.Reason ?? "No reason provided"}. NOTE: User service endpoint needed.",
            PerformedBy = adminUserId,
            Timestamp = now,
            IpAddress = "Admin API",
            UserAgent = "Admin API"
        };

        _context.AuditLogs.Add(auditLog);
        await _context.SaveChangesAsync();

        // TODO: Call User service endpoint when available
        // TODO: Publish UserStatusChangedEvent
        // TODO: Send notification to user

        return false; // Return false until User service endpoint is implemented
    }

    public async Task<bool> UpdateUserRoleAsync(Guid userId, UpdateUserRoleDto request, Guid adminUserId)
    {
        // TODO: This method needs User service endpoint PUT /api/User/users/{userId}/role
        // For now, this operation cannot be performed via HTTP - User service needs to expose this endpoint
        // The User service should handle the role update and return success/failure
        
        _logger.LogWarning("UpdateUserRoleAsync called but User service endpoint not available. UserId: {UserId}, Role: {Role}", userId, request.Role);
        
        // Create audit log in Admin DB
        var now = DateTime.UtcNow;
        var auditLog = new AuditLog
        {
            Entity = "User",
            EntityId = userId,
            Action = "RoleUpdated",
            Details = $"Role update requested: {request.Role}. Reason: {request.Reason ?? "No reason provided"}. NOTE: User service endpoint needed.",
            PerformedBy = adminUserId,
            Timestamp = now,
            IpAddress = "Admin API",
            UserAgent = "Admin API"
        };

        _context.AuditLogs.Add(auditLog);
        await _context.SaveChangesAsync();

        // TODO: Call User service endpoint when available
        // TODO: Publish RoleChangedEvent

        return false; // Return false until User service endpoint is implemented
    }

    public async Task<List<PendingKycUserDto>> GetPendingKycUsersAsync()
    {
        // Get pending KYC documents via User service
        var kycDocuments = await _userServiceClient.GetPendingKycDocumentsAsync();
        
        // Get users with pending KYC
        var allUsers = await _userServiceClient.GetUsersAsync();
        var pendingUsers = allUsers.Where(u => u.KycStatus == KycStatus.Pending || u.KycStatus == KycStatus.InReview)
            .OrderBy(u => u.CreatedAt)
            .Select(u => new PendingKycUserDto
            {
                Id = u.Id,
                Email = u.Email,
                FirstName = u.FirstName,
                LastName = u.LastName,
                KycStatus = u.KycStatus,
                SubmittedAt = u.CreatedAt,
                Documents = kycDocuments.Where(d => d.UserId == u.Id).ToList()
            })
            .ToList();

        return pendingUsers;
    }

    public async Task<KycDocumentDto> ReviewKycDocumentAsync(Guid documentId, ReviewKycDocumentDto request, Guid adminUserId)
    {
        // Review KYC document via User service
        var reviewedDocument = await _userServiceClient.ReviewKycDocumentAsync(documentId, request);

        // Create audit log in Admin DB
        var now = DateTime.UtcNow;
        _context.AuditLogs.Add(new AuditLog
        {
            Entity = "KycDocument",
            EntityId = documentId,
            Action = "Reviewed",
            Details = $"KYC document {reviewedDocument.DocumentType} reviewed with status {request.Status}.",
            PerformedBy = adminUserId,
            Timestamp = now,
            IpAddress = "Admin API",
            UserAgent = "Admin API"
        });

        await _context.SaveChangesAsync();

        return reviewedDocument;
    }

    public async Task<bool> UpdateUserKycStatusAsync(Guid userId, UpdateUserKycStatusDto request, Guid adminUserId)
    {
        // Update KYC status via User service
        var success = await _userServiceClient.UpdateKycStatusAsync(userId, request.Status, request.Reason);
        
        if (success)
        {
            // Create audit log in Admin DB
            var now = DateTime.UtcNow;
            _context.AuditLogs.Add(new AuditLog
            {
                Entity = "User",
                EntityId = userId,
                Action = "KycStatusUpdated",
                Details = $"KYC status updated to {request.Status}. Reason: {request.Reason ?? "No reason provided"}",
                PerformedBy = adminUserId,
                Timestamp = now,
                IpAddress = "Admin API",
                UserAgent = "Admin API"
            });
            await _context.SaveChangesAsync();
        }
        
        return success;
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
        // Get bookings via Booking service
        var bookings = await _bookingServiceClient.GetBookingsAsync(userId: userId);
        var totalBookings = bookings.Count;
        var completedBookings = bookings.Count(b => b.Status == BookingStatus.Completed);
        var cancelledBookings = bookings.Count(b => b.Status == BookingStatus.Cancelled);
        
        // Get payments via Payment service
        var payments = await _paymentServiceClient.GetPaymentsAsync();
        var userPayments = payments.Where(p => p.PayerId == userId && p.Status == PaymentStatus.Completed);
        var totalPayments = userPayments.Sum(p => p.Amount);
        
        // Get group memberships via Group service (would need Group service endpoint for user's groups)
        var groups = await _groupServiceClient.GetGroupsAsync();
        var groupMemberships = 0; // Would need Group service to provide user's group count
        var activeGroupMemberships = 0;

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
        // Get groups via HTTP client - note: filtering/sorting done in memory (may need Group service to support server-side filtering)
        var allGroups = await _groupServiceClient.GetGroupsAsync(request);
        
        // Apply filters in memory (ideally Group service should support these filters)
        var filtered = allGroups.AsEnumerable();
        
        if (!string.IsNullOrEmpty(request.Search))
        {
            var searchTerm = request.Search.ToLower();
            filtered = filtered.Where(g => 
                (g.Name?.ToLower().Contains(searchTerm) ?? false) ||
                (g.Description != null && g.Description.ToLower().Contains(searchTerm)));
        }

        if (request.Status.HasValue)
        {
            filtered = filtered.Where(g => g.Status == request.Status.Value);
        }

        // Member count filters - would need Group service to expose member count
        // Note: GroupDto may not have MemberCount, so this filter may not work

        // Apply sorting
        filtered = request.SortBy?.ToLower() switch
        {
            "name" => request.SortDirection == "asc" ? filtered.OrderBy(g => g.Name) : filtered.OrderByDescending(g => g.Name),
            "membercount" => request.SortDirection == "asc" ? filtered.OrderBy(g => 0) : filtered.OrderByDescending(g => 0), // Would need member count
            "activityscore" => request.SortDirection == "asc" ? filtered.OrderBy(g => 0) : filtered.OrderByDescending(g => 0), // Would need activity score
            _ => request.SortDirection == "asc" ? filtered.OrderBy(g => g.CreatedAt) : filtered.OrderByDescending(g => g.CreatedAt)
        };

        var totalCount = filtered.Count();
        var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);

        var groups = filtered
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(g => new GroupSummaryDto
            {
                Id = g.Id,
                Name = g.Name,
                Description = g.Description,
                Status = g.Status,
                MemberCount = 0, // Would need Group service to provide this
                VehicleCount = 0, // Would need Vehicle service to provide this
                CreatedAt = g.CreatedAt,
                ActivityScore = 0, // Would need to calculate from Group service data
                HealthStatus = GroupHealthStatus.Healthy, // Would need to calculate from Group service data
                CreatorName = null // Would need Group service to provide creator name
            })
            .ToList();

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
        // Get group details via HTTP client
        var group = await _groupServiceClient.GetGroupDetailsAsync(groupId);
        if (group == null)
            throw new ArgumentException("Group not found");

        // Get vehicles for this group via Vehicle service
        var vehicles = await _vehicleServiceClient.GetVehiclesAsync();
        var groupVehicles = vehicles.Where(v => v.GroupId == groupId).Select(v => new GroupVehicleDto
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
            TotalBookings = 0, // Would need Booking service to provide this
            TotalRevenue = 0 // Would need Payment service to provide this
        }).ToList();

        var financialSummary = await GetGroupFinancialSummaryAsync(groupId);
        var bookingStatistics = await GetGroupBookingStatisticsAsync(groupId);
        var recentActivity = await GetGroupRecentActivityAsync(groupId);
        
        // Proposals - would need Group service to expose proposals
        var proposals = new List<GroupProposalDto>();

        // Disputes - from Admin DB
        var disputes = await _context.Disputes
            .Where(d => d.GroupId == groupId)
            .Select(d => new GroupDisputeDto
            {
                Id = d.Id,
                Title = d.Subject,
                Description = d.Description,
                Status = d.Status,
                CreatedAt = d.CreatedAt,
                InitiatorName = "Unknown", // Would need to fetch via User service
                Resolution = d.Resolution,
                ResolvedAt = d.ResolvedAt
            })
            .ToListAsync();

        var health = await GetGroupHealthAsync(groupId);

        return new GroupDetailsDto
        {
            Id = group.Id,
            Name = group.Name,
            Description = group.Description,
            Status = group.Status,
            CreatedAt = group.CreatedAt,
            UpdatedAt = group.UpdatedAt,
            CreatorName = null, // Would need Group service to provide creator name
            Members = group.Members ?? new List<GroupMemberDetailsDto>(), // Would need Group service to provide members
            Vehicles = groupVehicles,
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
        // Update group status via Group service
        var success = await _groupServiceClient.UpdateGroupStatusAsync(groupId, request);
        
        if (success)
        {
            // Handle status change implications (cancel bookings, etc.)
            await HandleGroupStatusChangeImplicationsAsync(groupId, GroupStatus.Active, request.Status); // Would need to get old status from Group service

            // Create audit log in Admin DB
            var now = DateTime.UtcNow;
            var auditLog = new AuditLog
            {
                Entity = "OwnershipGroup",
                EntityId = groupId,
                Action = "StatusUpdated",
                Details = $"Group status changed to {request.Status}. Reason: {request.Reason}",
                PerformedBy = adminUserId,
                Timestamp = now,
                IpAddress = "Admin API",
                UserAgent = "Admin API"
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();

            // TODO: Publish GroupStatusChangedEvent
            // TODO: Send notification to all members if requested
        }

        return success;
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

        var auditLogs = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync();

        // Fetch user names via User service
        var userIds = auditLogs.Select(a => a.PerformedBy).Distinct().ToList();
        var users = await _userServiceClient.GetUsersAsync();
        var userMap = users.Where(u => userIds.Contains(u.Id)).ToDictionary(u => u.Id, u => $"{u.FirstName} {u.LastName}");

        var entries = auditLogs.Select(a => new GroupAuditEntryDto
        {
            Id = a.Id,
            Action = a.Action,
            Entity = a.Entity,
            Details = a.Details,
            UserName = userMap.GetValueOrDefault(a.PerformedBy, "Unknown"),
            Timestamp = a.Timestamp,
            IpAddress = a.IpAddress,
            UserAgent = a.UserAgent
        }).ToList();

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
        // Validate group exists via Group service
        var group = await _groupServiceClient.GetGroupDetailsAsync(groupId);
        if (group == null)
            return false;

        // Handle intervention based on type
        switch (request.Type)
        {
            case InterventionType.Freeze:
                // Update group status via Group service
                await _groupServiceClient.UpdateGroupStatusAsync(groupId, new UpdateGroupStatusDto 
                { 
                    Status = GroupStatus.Inactive, 
                    Reason = request.Reason 
                });
                // TODO: Implement freeze logic (cancel bookings, prevent new expenses) via Booking/Payment services
                break;
            case InterventionType.Unfreeze:
                // Update group status via Group service
                await _groupServiceClient.UpdateGroupStatusAsync(groupId, new UpdateGroupStatusDto 
                { 
                    Status = GroupStatus.Active, 
                    Reason = request.Reason 
                });
                // TODO: Implement unfreeze logic
                break;
            case InterventionType.AppointAdmin:
                // TODO: Implement appoint temporary admin logic via Group service
                break;
            case InterventionType.Message:
                // TODO: Send message to all group members via Notification service
                break;
        }

        // Create audit log in Admin DB
        var auditLog = new AuditLog
        {
            Entity = "OwnershipGroup",
            EntityId = groupId,
            Action = "Intervention",
            Details = $"Admin intervention: {request.Type}. Reason: {request.Reason}. Message: {request.Message}",
            PerformedBy = adminUserId,
            Timestamp = DateTime.UtcNow,
            IpAddress = "Admin API",
            UserAgent = "Admin API"
        };

        _context.AuditLogs.Add(auditLog);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<GroupHealthDto> GetGroupHealthAsync(Guid groupId)
    {
        // Get group details via Group service
        var group = await _groupServiceClient.GetGroupDetailsAsync(groupId);
        if (group == null)
            throw new ArgumentException("Group not found");

        var lastActivity = group.UpdatedAt;
        var daysInactive = (int)(DateTime.UtcNow - lastActivity).TotalDays;
        var memberCount = group.Members?.Count ?? 0;

        var health = new GroupHealthDto
        {
            Status = GetGroupHealthStatus(daysInactive, memberCount),
            Score = CalculateGroupHealthScore(daysInactive, memberCount),
            Issues = new List<string>(),
            Warnings = new List<string>(),
            LastActivity = lastActivity,
            DaysInactive = daysInactive,
            HasActiveDisputes = await _context.Disputes.AnyAsync(d => d.GroupId == groupId && (d.Status == DisputeStatus.Open || d.Status == DisputeStatus.UnderReview)),
            IsUsageBalanced = await IsGroupUsageBalancedAsync(groupId),
            Recommendation = GetGroupHealthRecommendation(daysInactive, memberCount)
        };

        // Add health issues and warnings
        if (health.DaysInactive > 30)
        {
            health.Issues.Add("Group has been inactive for more than 30 days");
        }

        if (memberCount < 2)
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
    private GroupHealthStatus GetGroupHealthStatus(int daysInactive, int memberCount)
    {
        if (daysInactive > 30 || memberCount < 2)
            return GroupHealthStatus.Critical;
        if (daysInactive > 14 || memberCount < 3)
            return GroupHealthStatus.Unhealthy;
        if (daysInactive > 7)
            return GroupHealthStatus.Warning;
        
        return GroupHealthStatus.Healthy;
    }

    private decimal CalculateGroupHealthScore(int daysInactive, int memberCount)
    {
        var score = 100m;
        
        // Deduct points for inactivity
        score -= Math.Min(50, (decimal)daysInactive * 2);
        
        // Deduct points for low member count
        if (memberCount < 2)
            score -= 30;
        else if (memberCount < 3)
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

    private string GetGroupHealthRecommendation(int daysInactive, int memberCount)
    {
        if (daysInactive > 30)
            return "Group appears dormant. Consider reaching out to members or dissolving the group.";
        if (memberCount < 2)
            return "Group needs more members to function effectively.";
        if (daysInactive > 14)
            return "Group has been inactive. Consider sending a reminder to members.";
        
        return "Group is healthy and functioning well.";
    }

    // Vehicle Management Methods
    public async Task<VehicleListResponseDto> GetVehiclesAsync(VehicleListRequestDto request)
    {
        // Get vehicles via HTTP client
        var allVehicles = await _vehicleServiceClient.GetVehiclesAsync();
        
        // Apply filters in memory
        var filtered = allVehicles.AsEnumerable();
        
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var searchLower = request.Search.ToLower();
            filtered = filtered.Where(v =>
                (v.Model?.ToLower().Contains(searchLower) ?? false) ||
                (v.PlateNumber?.ToLower().Contains(searchLower) ?? false) ||
                (v.Vin?.ToLower().Contains(searchLower) ?? false) ||
                (v.Color != null && v.Color.ToLower().Contains(searchLower)));
        }

        if (request.Status.HasValue)
        {
            filtered = filtered.Where(v => v.Status == request.Status.Value);
        }

        if (request.GroupId.HasValue)
        {
            filtered = filtered.Where(v => v.GroupId == request.GroupId.Value);
        }

        // Apply sorting
        var sortBy = request.SortBy?.ToLower() ?? "createdat";
        var sortDirection = request.SortDirection?.ToLower() ?? "desc";

        filtered = sortBy switch
        {
            "model" => sortDirection == "asc" ? filtered.OrderBy(v => v.Model) : filtered.OrderByDescending(v => v.Model),
            "platenumber" => sortDirection == "asc" ? filtered.OrderBy(v => v.PlateNumber) : filtered.OrderByDescending(v => v.PlateNumber),
            "year" => sortDirection == "asc" ? filtered.OrderBy(v => v.Year) : filtered.OrderByDescending(v => v.Year),
            "status" => sortDirection == "asc" ? filtered.OrderBy(v => v.Status) : filtered.OrderByDescending(v => v.Status),
            _ => sortDirection == "asc" ? filtered.OrderBy(v => v.CreatedAt) : filtered.OrderByDescending(v => v.CreatedAt)
        };

        var totalCount = filtered.Count();
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Max(1, Math.Min(100, request.PageSize));

        var vehicles = filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        // Get booking counts for each vehicle via Booking service
        var vehicleIds = vehicles.Select(v => v.Id).ToList();
        var bookings = await _bookingServiceClient.GetBookingsAsync();
        var bookingCounts = bookings
            .Where(b => vehicleIds.Contains(b.VehicleId))
            .GroupBy(b => b.VehicleId)
            .ToDictionary(g => g.Key, g => g.Count());

        // Get group names via Group service
        var groups = await _groupServiceClient.GetGroupsAsync();
        var groupMap = groups.ToDictionary(g => g.Id, g => g.Name);

        var vehicleDtos = vehicles.Select(v => new VehicleSummaryDto
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
            GroupId = v.GroupId,
            GroupName = v.GroupId.HasValue && groupMap.ContainsKey(v.GroupId.Value) ? groupMap[v.GroupId.Value] : null,
            CreatedAt = v.CreatedAt,
            TotalBookings = bookingCounts.GetValueOrDefault(v.Id, 0)
        }).ToList();

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return new VehicleListResponseDto
        {
            Vehicles = vehicleDtos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages
        };
    }

    public async Task<VehicleSummaryDto> GetVehicleDetailsAsync(Guid vehicleId)
    {
        // Get vehicle via HTTP client
        var vehicle = await _vehicleServiceClient.GetVehicleAsync(vehicleId);
        if (vehicle == null)
            throw new ArgumentException("Vehicle not found");

        // Get booking count via Booking service
        var bookings = await _bookingServiceClient.GetBookingsAsync();
        var bookingCount = bookings.Count(b => b.VehicleId == vehicleId);

        // Get group name via Group service
        var groupName = (string?)null;
        if (vehicle.GroupId.HasValue)
        {
            var group = await _groupServiceClient.GetGroupDetailsAsync(vehicle.GroupId.Value);
            groupName = group?.Name;
        }

        return new VehicleSummaryDto
        {
            Id = vehicle.Id,
            Vin = vehicle.Vin,
            PlateNumber = vehicle.PlateNumber,
            Model = vehicle.Model,
            Year = vehicle.Year,
            Color = vehicle.Color,
            Status = vehicle.Status,
            LastServiceDate = vehicle.LastServiceDate,
            Odometer = vehicle.Odometer,
            GroupId = vehicle.GroupId,
            GroupName = groupName,
            CreatedAt = vehicle.CreatedAt,
            TotalBookings = bookingCount
        };
    }

    public async Task<bool> UpdateVehicleStatusAsync(Guid vehicleId, VehicleStatus status, Guid adminUserId)
    {
        // TODO: This method needs Vehicle service endpoint PUT /api/Vehicle/{vehicleId}/status
        // For now, this operation cannot be performed via HTTP - Vehicle service needs to expose this endpoint
        
        _logger.LogWarning("UpdateVehicleStatusAsync called but Vehicle service endpoint not available. VehicleId: {VehicleId}, Status: {Status}", vehicleId, status);
        
        // Create audit log in Admin DB
        var now = DateTime.UtcNow;
        var auditLog = new AuditLog
        {
            Entity = "Vehicle",
            EntityId = vehicleId,
            Action = "StatusUpdate",
            Details = $"Vehicle status update requested: {status}. NOTE: Vehicle service endpoint needed.",
            PerformedBy = adminUserId,
            Timestamp = now,
            IpAddress = "Admin API",
            UserAgent = "Admin API"
        };

        _context.AuditLogs.Add(auditLog);
        await _context.SaveChangesAsync();

        // TODO: Call Vehicle service endpoint when available

        return false; // Return false until Vehicle service endpoint is implemented
    }

    private async Task<GroupFinancialSummaryDto> GetGroupFinancialSummaryAsync(Guid groupId)
    {
        // Get expenses via Payment service
        var expenses = await _paymentServiceClient.GetExpensesAsync(groupId: groupId);
        var expenseList = expenses.ToList();

        var totalExpenses = expenseList.Sum(e => e.Amount);
        var monthlyExpenses = expenseList
            .Where(e => e.DateIncurred >= DateTime.UtcNow.AddDays(-30))
            .Sum(e => e.Amount);

        // Get member count via Group service (would need Group service endpoint)
        var group = await _groupServiceClient.GetGroupDetailsAsync(groupId);
        var memberCount = group?.Members?.Count ?? 0;
        var averageExpensePerMember = memberCount > 0 ? totalExpenses / memberCount : 0;

        return new GroupFinancialSummaryDto
        {
            TotalExpenses = totalExpenses,
            FundBalance = 0, // Would need Payment service to expose fund balance
            MonthlyExpenses = monthlyExpenses,
            AverageExpensePerMember = averageExpensePerMember,
            TotalRevenue = 0, // Would need Payment service to calculate
            NetBalance = 0, // Would need Payment service to calculate
            ExpenseCategories = expenseList
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
        // Get bookings via Booking service
        var bookings = await _bookingServiceClient.GetBookingsAsync(groupId: groupId);
        var bookingList = bookings.ToList();

        var totalBookings = bookingList.Count;
        var completedBookings = bookingList.Count(b => b.Status == BookingStatus.Completed);
        var cancelledBookings = bookingList.Count(b => b.Status == BookingStatus.Cancelled);
        var activeBookings = bookingList.Count(b => b.Status == BookingStatus.InProgress);
        
        // Calculate revenue from completed bookings (using TripFeeAmount from bookings)
        var totalRevenue = bookingList
            .Where(b => b.Status == BookingStatus.Completed)
            .Sum(b => b.TripFeeAmount);

        var thisMonth = DateTime.UtcNow.AddDays(-30);
        var lastMonth = DateTime.UtcNow.AddDays(-60);
        var bookingsThisMonth = bookingList.Count(b => b.CreatedAt >= thisMonth);
        var bookingsLastMonth = bookingList.Count(b => b.CreatedAt >= lastMonth && b.CreatedAt < thisMonth);

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
        var auditLogs = await _context.AuditLogs
            .Where(a => a.Entity == "OwnershipGroup" && a.EntityId == groupId)
            .OrderByDescending(a => a.Timestamp)
            .Take(20)
            .ToListAsync();

        // Fetch user names via User service
        var userIds = auditLogs.Select(a => a.PerformedBy).Distinct().ToList();
        var users = await _userServiceClient.GetUsersAsync();
        var userMap = users.Where(u => userIds.Contains(u.Id)).ToDictionary(u => u.Id, u => $"{u.FirstName} {u.LastName}");

        return auditLogs.Select(a => new GroupActivityDto
        {
            Id = a.Id,
            Action = a.Action,
            Entity = a.Entity,
            Details = a.Details,
            UserName = userMap.GetValueOrDefault(a.PerformedBy, "Unknown"),
            Timestamp = a.Timestamp
        }).ToList();
    }

    private async Task HandleGroupStatusChangeImplicationsAsync(Guid groupId, GroupStatus oldStatus, GroupStatus newStatus)
    {
        // TODO: This would need Booking service endpoints to cancel bookings
        // For now, we can only log the requirement - Booking service should handle cancellation
        switch (newStatus)
        {
            case GroupStatus.Inactive:
            case GroupStatus.Dissolved:
                // Cancel future bookings - would need Booking service endpoint
                // POST /api/Booking/cancel-by-group/{groupId} or similar
                _logger.LogInformation("Group {GroupId} status changed to {Status}. Future bookings should be cancelled via Booking service.", groupId, newStatus);
                break;
        }
    }

    // Dispute Management Methods
    public async Task<Guid> CreateDisputeAsync(CreateDisputeDto request, Guid adminUserId)
    {
        try
        {
            // Validate group exists via Group service
            var group = await _groupServiceClient.GetGroupDetailsAsync(request.GroupId);
            if (group == null)
                throw new ArgumentException("Group not found");

            // Determine reporter - use provided or admin
            var reporterId = request.ReportedBy ?? adminUserId;

            // Validate reporter is member of group (if not admin creating on behalf)
            if (request.ReportedBy.HasValue)
            {
                // Check if user is a member via Group service
                var isMember = group.Members?.Any(m => m.UserId == reporterId) ?? false;
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
            var query = _context.Disputes.AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(request.Search))
            {
                query = query.Where(d => d.Subject.Contains(request.Search) || 
                                   d.Description.Contains(request.Search));
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
                .Include(d => d.Comments)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync();

            // Fetch user and group names via HTTP clients
            var userIds = disputes.Select(d => d.ReportedBy).Union(disputes.Where(d => d.AssignedTo.HasValue).Select(d => d.AssignedTo!.Value)).Distinct().ToList();
            var groupIds = disputes.Select(d => d.GroupId).Distinct().ToList();
            
            var users = await _userServiceClient.GetUsersAsync();
            var userMap = users.Where(u => userIds.Contains(u.Id)).ToDictionary(u => u.Id, u => $"{u.FirstName} {u.LastName}");
            
            var groups = await _groupServiceClient.GetGroupsAsync();
            var groupMap = groups.Where(g => groupIds.Contains(g.Id)).ToDictionary(g => g.Id, g => g.Name ?? "Unknown");

            var disputeDtos = disputes.Select(d => new DisputeSummaryDto
            {
                Id = d.Id,
                GroupId = d.GroupId,
                GroupName = groupMap.GetValueOrDefault(d.GroupId, "Unknown"),
                Subject = d.Subject,
                Category = d.Category,
                Priority = d.Priority,
                Status = d.Status,
                ReporterName = userMap.GetValueOrDefault(d.ReportedBy, "Unknown"),
                AssignedToName = d.AssignedTo.HasValue ? userMap.GetValueOrDefault(d.AssignedTo.Value, "Unknown") : null,
                CreatedAt = d.CreatedAt,
                ResolvedAt = d.ResolvedAt,
                CommentCount = d.Comments.Count
            }).ToList();

            return new DisputeListResponseDto
            {
                Disputes = disputeDtos,
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
                .Include(d => d.Comments)
                .FirstOrDefaultAsync(d => d.Id == disputeId);

            if (dispute == null)
                throw new ArgumentException("Dispute not found");

            // Fetch user and group names via HTTP clients
            var userIds = new List<Guid> { dispute.ReportedBy };
            if (dispute.AssignedTo.HasValue) userIds.Add(dispute.AssignedTo.Value);
            if (dispute.ResolvedBy.HasValue) userIds.Add(dispute.ResolvedBy.Value);
            userIds.AddRange(dispute.Comments.Select(c => c.CommentedBy));
            userIds = userIds.Distinct().ToList();

            var users = await _userServiceClient.GetUsersAsync();
            var userMap = users.Where(u => userIds.Contains(u.Id)).ToDictionary(u => u.Id, u => new { Name = $"{u.FirstName} {u.LastName}", Email = u.Email });

            var group = await _groupServiceClient.GetGroupDetailsAsync(dispute.GroupId);
            var groupName = group?.Name ?? "Unknown";

            // Get audit trail for this dispute
            var auditLogs = await _context.AuditLogs
                .Where(al => al.Entity == "Dispute" && al.EntityId == disputeId)
                .OrderBy(al => al.Timestamp)
                .ToListAsync();

            var auditUserIds = auditLogs.Select(al => al.PerformedBy).Distinct().ToList();
            var auditUsers = await _userServiceClient.GetUsersAsync();
            var auditUserMap = auditUsers.Where(u => auditUserIds.Contains(u.Id)).ToDictionary(u => u.Id, u => $"{u.FirstName} {u.LastName}");

            var actions = auditLogs.Select(al => new DisputeActionDto
            {
                Action = al.Action,
                Details = al.Details,
                UserName = auditUserMap.GetValueOrDefault(al.PerformedBy, "Unknown"),
                Timestamp = al.Timestamp
            }).ToList();

            return new DisputeDetailsDto
            {
                Id = dispute.Id,
                GroupId = dispute.GroupId,
                GroupName = groupName,
                Subject = dispute.Subject,
                Description = dispute.Description,
                Category = dispute.Category,
                Priority = dispute.Priority,
                Status = dispute.Status,
                ReporterName = userMap.GetValueOrDefault(dispute.ReportedBy)?.Name ?? "Unknown",
                ReporterEmail = userMap.GetValueOrDefault(dispute.ReportedBy)?.Email,
                AssignedToName = dispute.AssignedTo.HasValue ? userMap.GetValueOrDefault(dispute.AssignedTo.Value)?.Name : null,
                AssignedToEmail = dispute.AssignedTo.HasValue ? userMap.GetValueOrDefault(dispute.AssignedTo.Value)?.Email : null,
                Resolution = dispute.Resolution,
                ResolverName = dispute.ResolvedBy.HasValue ? userMap.GetValueOrDefault(dispute.ResolvedBy.Value)?.Name : null,
                CreatedAt = dispute.CreatedAt,
                UpdatedAt = dispute.UpdatedAt,
                ResolvedAt = dispute.ResolvedAt,
                Comments = dispute.Comments.Select(c => new DisputeCommentDto
                {
                    Id = c.Id,
                    Comment = c.Comment,
                    CommenterName = userMap.GetValueOrDefault(c.CommentedBy)?.Name ?? "Unknown",
                    CommenterEmail = userMap.GetValueOrDefault(c.CommentedBy)?.Email,
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

            // Validate assigned user exists and is staff via User service
            var assignedUser = await _userServiceClient.GetUserProfileAsync(request.AssignedTo);
            if (assignedUser == null)
                throw new ArgumentException("Assigned user not found");
            
            if (assignedUser.Role != UserRole.Staff && assignedUser.Role != UserRole.SystemAdmin)
                throw new ArgumentException("Assigned user must be staff or admin");

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
            var group = await _groupServiceClient.GetGroupDetailsAsync(dispute.GroupId);
            var hasAccess = group?.Members?.Any(m => m.UserId == userId) ?? false;
            
            if (!hasAccess)
            {
                // Check if user is staff/admin via User service
                var user = await _userServiceClient.GetUserProfileAsync(userId);
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
            // Get staff members via User service
            var users = await _userServiceClient.GetUsersAsync();
            var staffUsers = users.Where(u => u.Role == UserRole.Staff || u.Role == UserRole.SystemAdmin).ToList();

            // Get disputes from Admin DB
            var disputes = await _context.Disputes.ToListAsync();
            
            // Calculate workload for each staff member
            var staffWorkload = staffUsers.Select(u => new
            {
                UserId = u.Id,
                OpenDisputes = disputes.Count(d => d.AssignedTo == u.Id && d.Status == DisputeStatus.Open),
                UnderReviewDisputes = disputes.Count(d => d.AssignedTo == u.Id && d.Status == DisputeStatus.UnderReview)
            }).ToList();

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
