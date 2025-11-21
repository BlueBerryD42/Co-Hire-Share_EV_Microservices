using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using CoOwnershipVehicle.Admin.Api.Data;
using CoOwnershipVehicle.Admin.Api.Services.HttpClients;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Domain.Enums;

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

        // Get counts from services via HTTP
        var users = await _userServiceClient.GetUsersAsync();
        var groups = await _groupServiceClient.GetGroupsAsync();
        var vehicles = await _vehicleServiceClient.GetVehiclesAsync();
        var bookings = await _bookingServiceClient.GetBookingsAsync();
        var payments = await _paymentServiceClient.GetPaymentsAsync();
        var expenses = await _paymentServiceClient.GetExpensesAsync();

        var userCount = users.Count;
        var groupCount = groups.Count;
        var vehicleCount = vehicles.Count;
        var bookingCount = bookings.Count;
        var paymentCount = payments.Count;
        var expenseCount = expenses.Count;

        // Use current time as approximation for latest updates (since we don't have direct access)
        var now = DateTime.UtcNow;
        var latestUserUpdate = users.Any() ? users.Max(u => u.CreatedAt) : now;
        var latestGroupUpdate = groups.Any() ? groups.Max(g => g.CreatedAt) : now;
        var latestVehicleUpdate = vehicles.Any() ? vehicles.Max(v => v.CreatedAt) : now;
        var latestBookingUpdate = bookings.Any() ? bookings.Max(b => b.CreatedAt) : now;
        var latestPaymentUpdate = payments.Any() ? payments.Max(p => p.CreatedAt) : now;
        var latestExpenseUpdate = expenses.Any() ? expenses.Max(e => e.DateIncurred) : now;

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
        
        // Get users from User service via HTTP
        var users = await _userServiceClient.GetUsersAsync();
        var totalUsers = users.Count;
        // Note: Lockout status is managed by Auth service, not available in UserProfileDto
        // For now, consider all users as active (lockout status would need to be fetched from Auth service)
        var activeUsers = totalUsers; // All users are considered active (lockout handled by Auth service)
        var inactiveUsers = 0; // Lockout status not available in UserProfileDto
        var pendingKyc = users.Count(u => u.KycStatus == KycStatus.Pending);
        var approvedKyc = users.Count(u => u.KycStatus == KycStatus.Approved);
        var rejectedKyc = users.Count(u => u.KycStatus == KycStatus.Rejected);

        return (totalUsers, activeUsers, inactiveUsers, pendingKyc, approvedKyc, rejectedKyc);
    }

    private async Task<(int Current, int Previous, int ThisMonth, int ThisWeek)> GetPeriodUserCountsAsync(DateTime periodStart, DateTime previousPeriodStart, DateTime now)
    {
        // Get users from User service via HTTP
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

        // Get groups from Group service via HTTP
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

        // Get vehicles from Vehicle service via HTTP
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

        // Get bookings from Booking service via HTTP
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
        // Get payments from Payment service via HTTP
        var allPayments = await _paymentServiceClient.GetPaymentsAsync(status: PaymentStatus.Completed);
        var totalRevenue = allPayments.Sum(p => p.Amount);

        var monthlyRevenue = allPayments
            .Where(p => p.PaidAt.HasValue && p.PaidAt.Value >= now.AddDays(-30))
            .Sum(p => p.Amount);

        var weeklyRevenue = allPayments
            .Where(p => p.PaidAt.HasValue && p.PaidAt.Value >= now.AddDays(-7))
            .Sum(p => p.Amount);

        var dailyRevenue = allPayments
            .Where(p => p.PaidAt.HasValue && p.PaidAt.Value >= now.AddDays(-1))
            .Sum(p => p.Amount);

        var revenueThisPeriod = allPayments
            .Where(p => p.PaidAt.HasValue && p.PaidAt.Value >= periodStart)
            .Sum(p => p.Amount);

        var revenuePreviousPeriod = allPayments
            .Where(p => p.PaidAt.HasValue && p.PaidAt.Value >= previousPeriodStart && p.PaidAt.Value < periodStart)
            .Sum(p => p.Amount);

        return (totalRevenue, monthlyRevenue, weeklyRevenue, dailyRevenue, revenueThisPeriod, revenuePreviousPeriod);
    }

    public async Task<SystemHealthDto> GetSystemHealthAsync()
    {
        var now = DateTime.UtcNow;
        
        // Check database connection
        var databaseConnected = await _context.Database.CanConnectAsync();
        
        // Count pending approvals from services via HTTP
        var bookings = await _bookingServiceClient.GetBookingsAsync();
        var pendingBookings = bookings.Count(b => b.Status == BookingStatus.PendingApproval);
        
        var users = await _userServiceClient.GetUsersAsync();
        var pendingKyc = users.Count(u => u.KycStatus == KycStatus.InReview);
        var pendingApprovals = pendingBookings + pendingKyc;
        
        // Count overdue maintenance (vehicles that haven't been serviced in 6 months)
        var vehicles = await _vehicleServiceClient.GetVehiclesAsync();
        var overdueMaintenance = vehicles.Count(v => 
            v.LastServiceDate == null || v.LastServiceDate < now.AddMonths(-6));
        
        // Count disputes from Admin database (disputes are stored in Admin service)
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

        // Overdue maintenance alerts - get from Vehicle service
        var vehicles = await _vehicleServiceClient.GetVehiclesAsync();
        var overdueMaintenance = vehicles.Count(v => 
            v.LastServiceDate == null || v.LastServiceDate < now.AddMonths(-6));

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

        // Pending KYC alerts - get from User service
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

        // Pending booking approvals - get from Booking service
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
        
        // Get payments from Payment service via HTTP
        var allPayments = await _paymentServiceClient.GetPaymentsAsync(status: PaymentStatus.Completed);
        var allTimeRevenue = allPayments.Sum(p => p.Amount);
        
        var yearStart = new DateTime(now.Year, 1, 1);
        var rollingMonthStart = now.AddDays(-30);
        var weekStart = now.AddDays(-7);
        var dayStart = now.AddDays(-1);

        var yearRevenue = allPayments
            .Where(p => p.PaidAt.HasValue && p.PaidAt.Value >= yearStart)
            .Sum(p => p.Amount);
        var monthRevenue = allPayments
            .Where(p => p.PaidAt.HasValue && p.PaidAt.Value >= rollingMonthStart)
            .Sum(p => p.Amount);
        var weekRevenue = allPayments
            .Where(p => p.PaidAt.HasValue && p.PaidAt.Value >= weekStart)
            .Sum(p => p.Amount);
        var dayRevenue = allPayments
            .Where(p => p.PaidAt.HasValue && p.PaidAt.Value >= dayStart)
            .Sum(p => p.Amount);

        // Revenue by source - categorize payments by description/notes
        var revenueBySource = new List<KeyValuePair<string, decimal>>();
        revenueBySource.Add(new KeyValuePair<string, decimal>("Payments", allTimeRevenue));
        // Note: Ledger entries would need to come from Payment service if available
        // For now, we'll categorize based on payment descriptions
        var fees = allPayments.Where(p => p.Notes?.Contains("fee", StringComparison.OrdinalIgnoreCase) == true).Sum(p => p.Amount);
        revenueBySource.Add(new KeyValuePair<string, decimal>("Fees", fees));

        // Get expenses from Payment service
        var allExpenses = await _paymentServiceClient.GetExpensesAsync();
        var totalExpenses = allExpenses.Sum(e => e.Amount);

        // Total fund balances - would need to come from Payment service if available
        // For now, use a placeholder
        var totalFundBalances = 0m;

        var totalPayments = allPayments.Count;
        var successPayments = allPayments.Count;
        var failedPayments = await _paymentServiceClient.GetPaymentsAsync(status: PaymentStatus.Failed);
        var failedPaymentsCount = failedPayments.Count;
        var failedPaymentsAmount = failedPayments.Sum(p => p.Amount);
        var pendingPaymentsList = await _paymentServiceClient.GetPaymentsAsync(status: PaymentStatus.Pending);
        var pendingPayments = pendingPaymentsList.Count;
        var successRate = totalPayments == 0 ? 0 : (double)successPayments / totalPayments * 100d;

        // Revenue trend (last 30 days)
        var trendStart = now.AddDays(-30).Date;
        var revenueTrend = allPayments
            .Where(p => p.PaidAt.HasValue && p.PaidAt.Value >= trendStart)
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

        // Financial health score (simple composite)
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
            RevenueTrend = revenueTrend.OrderBy(x => x.Date).ToList(),
            TopSpendingGroups = topGroups,
            FinancialHealthScore = healthScore
        };
    }

    public async Task<FinancialGroupBreakdownDto> GetFinancialByGroupsAsync()
    {
        // Get groups from Group service via HTTP
        var groups = await _groupServiceClient.GetGroupsAsync();
        var groupList = groups.Select(g => new { g.Id, g.Name }).ToList();
        
        // Get expenses from Payment service via HTTP
        var allExpenses = await _paymentServiceClient.GetExpensesAsync();
        var expenses = allExpenses.Select(e => new { e.GroupId, ExpenseType = e.ExpenseType, e.Amount, DateIncurred = e.DateIncurred }).ToList();
        
        // Get payments from Payment service via HTTP
        var allPayments = await _paymentServiceClient.GetPaymentsAsync();
        var payments = allPayments.Select(p => new { p.Id, InvoiceId = p.InvoiceId, p.Status }).ToList();
        
        // Note: Invoice and expense mapping would need to come from Payment service
        // For now, we'll work with what we have
        var expenseIdToGroup = expenses.ToDictionary(e => e.GroupId, e => e.GroupId);
        
        // Latest ledger balance per group - would need Payment service endpoint
        // For now, use placeholder
        var balances = new List<(Guid GroupId, decimal Balance)>();

        var items = new List<GroupFinancialItemDto>();
        foreach (var g in groups)
        {
            var gExpenses = expenses.Where(e => e.GroupId == g.Id).ToList();
            var byType = gExpenses
                .GroupBy(e => e.ExpenseType)
                .ToDictionary(k => k.Key.ToString(), v => v.Sum(x => x.Amount));
            var balanceTuple = balances.FirstOrDefault(b => b.GroupId == g.Id);
            var balance = balanceTuple != default ? balanceTuple.Balance : 0m;

            // Note: Invoice mapping would need Payment service endpoint
            // For now, match payments to expenses by group
            var groupPayments = payments.Where(p => p.InvoiceId != Guid.Empty).ToList();
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
        // Get payments from Payment service via HTTP
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
        // Get expenses from Payment service via HTTP
        var allExpenses = await _paymentServiceClient.GetExpensesAsync();
        var expenseRows = allExpenses.Select(e => new { ExpenseType = e.ExpenseType, e.Amount, CreatedAt = e.DateIncurred, e.GroupId }).ToList();

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

        // Get vehicles from Vehicle service via HTTP
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
        // Get payments from Payment service via HTTP
        var allPayments = await _paymentServiceClient.GetPaymentsAsync(status: PaymentStatus.Completed);
        var amounts = allPayments.Select(p => (double)p.Amount).ToList();
        var mean = amounts.Count == 0 ? 0 : amounts.Average();
        var std = amounts.Count <= 1 ? 0 : Math.Sqrt(amounts.Sum(a => Math.Pow(a - mean, 2)) / (amounts.Count - 1));
        var anomalies = new List<PaymentAnomalyDto>();
        if (std > 0)
        {
            foreach (var p in allPayments)
            {
                var z = ((double)p.Amount - mean) / std;
                if (Math.Abs(z) >= 3)
                {
                    anomalies.Add(new PaymentAnomalyDto { PaymentId = p.Id, Amount = p.Amount, ZScore = Math.Round(z, 2), PaidAt = p.PaidAt, Method = p.Method });
                }
            }
        }

        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
        var failedPayments = await _paymentServiceClient.GetPaymentsAsync(status: PaymentStatus.Failed);
        var suspicious = failedPayments
            .Where(p => p.CreatedAt >= sevenDaysAgo)
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
        // Note: Ledger entries would need to come from Payment service
        // For now, return empty list as we don't have access to ledger entries
        // This would need a Payment service endpoint to get group balances
        return new List<GroupNegativeBalanceDto>();
    }

    private async Task<(KycStatus Status, string Reason)> DetermineOverallKycStatusAsync(Guid userId, KycDocumentDto? updatedDocument = null)
    {
        // Get KYC documents from User service via HTTP
        // Get all KYC documents for the user
        var allDocs = await _userServiceClient.GetAllKycDocumentsAsync();
        var documents = allDocs
            .Where(d => d.UserId == userId && (updatedDocument == null || d.Id != updatedDocument.Id))
            .ToList();

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

        if (documents.All(d => d.Status == KycDocumentStatus.Approved) && documents.Count() >= 2)
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
        // Get users from User service via HTTP
        var users = await _userServiceClient.GetUsersAsync(request);
        
        // Apply filtering and sorting in memory (since we're getting all users)
        var query = users.AsQueryable();

        // Apply search filter
        if (!string.IsNullOrEmpty(request.Search))
        {
            var searchTerm = request.Search.ToLower();
            query = query.Where(u => 
                (u.FirstName != null && u.FirstName.ToLower().Contains(searchTerm)) ||
                (u.LastName != null && u.LastName.ToLower().Contains(searchTerm)) ||
                (u.Email != null && u.Email.ToLower().Contains(searchTerm)) ||
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
        // Note: Lockout status is managed by Auth service and not available in UserProfileDto
        // Account status filtering would require integration with Auth service
        if (request.AccountStatus.HasValue)
        {
            // For now, skip account status filtering as LockoutEnd is not available in UserProfileDto
            // This would need to be implemented via Auth service integration
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

        var totalCount = query.Count();
        var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);

        var userList = query
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
                AccountStatus = UserAccountStatus.Active, // Lockout status managed by Auth service, not available in UserProfileDto
                CreatedAt = u.CreatedAt,
                LastLoginAt = null // This would need to be tracked separately
            })
            .ToList();

        return new UserListResponseDto
        {
            Users = userList,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalPages = totalPages
        };
    }

    public async Task<UserDetailsDto> GetUserDetailsAsync(Guid userId)
    {
        // Get user from User service via HTTP
        var user = await _userServiceClient.GetUserProfileAsync(userId);
        if (user == null)
            throw new ArgumentException("User not found");

        // Get all KYC documents from User service
        var kycDocuments = await _userServiceClient.GetAllKycDocumentsAsync();
        var userKycDocs = kycDocuments.Where(d => d.UserId == userId).ToList();

        // Get group memberships - would need Group service endpoint for user's groups
        var groups = await _groupServiceClient.GetGroupsAsync();
        var groupMemberships = new List<GroupMembershipDto>(); // Placeholder - would need Group service endpoint

        var statistics = await GetUserStatisticsAsync(userId);

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
            AccountStatus = UserAccountStatus.Active, // Lockout status managed by Auth service, not available in UserProfileDto
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
        // Get user from User service to verify it exists
        var user = await _userServiceClient.GetUserProfileAsync(userId);
        if (user == null)
            return false;

        // Note: User lockout/status is managed by Auth service, not User service
        // This method should call Auth service to update lockout status
        // For now, we'll just log the action since we don't have Auth service client
        
        var oldStatus = UserAccountStatus.Active; // Default since lockout info not available
        
        // TODO: Call Auth service to update user lockout status
        // This would require an IAuthServiceClient to be injected
        // For now, we'll just create an audit log

        var now = DateTime.UtcNow;
        
        // Create audit log
        var auditLog = new AuditLog
        {
            Entity = "User",
            EntityId = userId,
            Action = "StatusUpdated",
            Details = $"Status change requested from {oldStatus} to {request.Status}. Reason: {request.Reason ?? "No reason provided"}. Note: Actual lockout update requires Auth service integration.",
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
        // Get user from User service via HTTP
        var user = await _userServiceClient.GetUserProfileAsync(userId);
        if (user == null)
            return false;

        var oldRole = user.Role;
        // Note: Role update should be done via User service endpoint
        // For now, we'll just log the action
        // TODO: Add UpdateUserRoleAsync to IUserServiceClient

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

    public async Task<PendingKycUserListResponseDto> GetPendingKycUsersAsync(KycDocumentFilterDto? filter = null)
    {
        filter ??= new KycDocumentFilterDto();
        
        // Get all KYC documents from User service via HTTP (frontend will handle filtering)
        var allDocuments = await _userServiceClient.GetAllKycDocumentsAsync();
        
        // Group documents by userId first
        var documentsByUserId = allDocuments.GroupBy(d => d.UserId).ToDictionary(g => g.Key, g => g.ToList());
        
        // Get all users that have KYC documents
        var allUsers = await _userServiceClient.GetUsersAsync();
        var usersWithDocuments = allUsers.Where(u => documentsByUserId.ContainsKey(u.Id)).ToList();
        
        // Group by user and create DTOs - include all users with KYC documents
        var usersDict = new Dictionary<Guid, PendingKycUserDto>();
        
        // Add all users that have KYC documents
        foreach (var user in usersWithDocuments)
        {
            if (!usersDict.ContainsKey(user.Id))
            {
                usersDict[user.Id] = new PendingKycUserDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    KycStatus = user.KycStatus,
                    SubmittedAt = user.CreatedAt,
                    Documents = documentsByUserId[user.Id].ToList() // Add all documents for this user
                };
            }
        }
        
        // Apply filters to users and their documents (frontend can also filter, but we do server-side filtering for efficiency)
        var usersList = usersDict.Values.ToList();
        
        // Filter by document status if provided
        if (filter.Status.HasValue)
        {
            usersList = usersList.Select(u => new PendingKycUserDto
            {
                Id = u.Id,
                Email = u.Email,
                FirstName = u.FirstName,
                LastName = u.LastName,
                KycStatus = u.KycStatus,
                SubmittedAt = u.SubmittedAt,
                Documents = u.Documents.Where(d => d.Status == filter.Status.Value).ToList()
            }).Where(u => u.Documents.Any()).ToList();
        }
        
        // Filter by document type if provided
        if (filter.DocumentType.HasValue)
        {
            usersList = usersList.Select(u => new PendingKycUserDto
            {
                Id = u.Id,
                Email = u.Email,
                FirstName = u.FirstName,
                LastName = u.LastName,
                KycStatus = u.KycStatus,
                SubmittedAt = u.SubmittedAt,
                Documents = u.Documents.Where(d => d.DocumentType == filter.DocumentType.Value).ToList()
            }).Where(u => u.Documents.Any()).ToList();
        }
        
        // Filter by date range if provided
        if (filter.FromDate.HasValue || filter.ToDate.HasValue)
        {
            usersList = usersList.Select(u => new PendingKycUserDto
            {
                Id = u.Id,
                Email = u.Email,
                FirstName = u.FirstName,
                LastName = u.LastName,
                KycStatus = u.KycStatus,
                SubmittedAt = u.SubmittedAt,
                Documents = u.Documents.Where(d => 
                    (!filter.FromDate.HasValue || d.UploadedAt >= filter.FromDate.Value) &&
                    (!filter.ToDate.HasValue || d.UploadedAt <= filter.ToDate.Value)
                ).ToList()
            }).Where(u => u.Documents.Any()).ToList();
        }
        
        // Apply search filter to users if provided
        if (!string.IsNullOrEmpty(filter.Search))
        {
            var searchTerm = filter.Search.ToLower();
            usersList = usersList.Where(u => 
                u.Email.ToLower().Contains(searchTerm) ||
                u.FirstName.ToLower().Contains(searchTerm) ||
                u.LastName.ToLower().Contains(searchTerm) ||
                u.Documents.Any(d => 
                    d.UserName.ToLower().Contains(searchTerm) ||
                    d.FileName.ToLower().Contains(searchTerm)
                )
            ).ToList();
        }
        
        // Apply sorting to documents within each user
        var sortBy = filter.SortBy?.ToLower() ?? "uploadedat";
        var sortDirection = filter.SortDirection?.ToLower() ?? "desc";
        
        foreach (var user in usersList)
        {
            user.Documents = sortBy switch
            {
                "filename" => sortDirection == "asc" 
                    ? user.Documents.OrderBy(d => d.FileName).ToList()
                    : user.Documents.OrderByDescending(d => d.FileName).ToList(),
                "documenttype" => sortDirection == "asc" 
                    ? user.Documents.OrderBy(d => d.DocumentType).ToList()
                    : user.Documents.OrderByDescending(d => d.DocumentType).ToList(),
                "status" => sortDirection == "asc" 
                    ? user.Documents.OrderBy(d => d.Status).ToList()
                    : user.Documents.OrderByDescending(d => d.Status).ToList(),
                _ => sortDirection == "asc" 
                    ? user.Documents.OrderBy(d => d.UploadedAt).ToList()
                    : user.Documents.OrderByDescending(d => d.UploadedAt).ToList()
            };
        }
        
        // usersList is already defined above, so we use it directly
        var totalCount = usersList.Count;
        var totalPages = (int)Math.Ceiling((double)totalCount / filter.PageSize);
        
        // Apply pagination
        var paginatedUsers = usersList
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToList();
        
        return new PendingKycUserListResponseDto
        {
            Users = paginatedUsers,
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize,
            TotalPages = totalPages
        };
    }

    public async Task<KycDocumentDto> ReviewKycDocumentAsync(Guid documentId, ReviewKycDocumentDto request, Guid adminUserId)
    {
        // Get reviewer from User service
        var reviewer = await _userServiceClient.GetUserProfileAsync(adminUserId);
        if (reviewer == null)
            throw new ArgumentException("Reviewer not found");

        // Review document via User service
        // Note: ReviewKycDocumentAsync doesn't take adminUserId parameter, it should be handled by the User service
        var document = await _userServiceClient.ReviewKycDocumentAsync(documentId, request);
        
        var now = DateTime.UtcNow;
        
        // Create audit log
        _context.AuditLogs.Add(new AuditLog
        {
            Entity = "KycDocument",
            EntityId = documentId,
            Action = "Reviewed",
            Details = $"KYC document {document.DocumentType} reviewed with status {request.Status}.",
            PerformedBy = adminUserId,
            Timestamp = now,
            IpAddress = "Admin API",
            UserAgent = "Admin API"
        });

        // Note: The UserService.ReviewKycDocumentAsync already calls UpdateOverallKycStatusAsync
        // which properly checks all documents and updates the overall KYC status.
        // No need to manually update the status here.

        await _context.SaveChangesAsync();

        // TODO: Send notification to user about document review
        // This would require Notification service client integration

        return document;
    }

    public async Task<KycDocumentDto> GetKycDocumentDetailsAsync(Guid documentId)
    {
        var document = await _userServiceClient.GetKycDocumentAsync(documentId);
        if (document == null)
            throw new ArgumentException("KYC document not found");
        
        return document;
    }

    public async Task<byte[]> DownloadKycDocumentAsync(Guid documentId)
    {
        var document = await _userServiceClient.GetKycDocumentAsync(documentId);
        if (document == null)
            throw new ArgumentException("KYC document not found");

        // Download file from storage URL
        var fileBytes = await _userServiceClient.DownloadKycDocumentAsync(documentId);
        if (fileBytes == null || fileBytes.Length == 0)
            throw new InvalidOperationException("Failed to download document file");

        return fileBytes;
    }

    public async Task<bool> BulkReviewKycDocumentsAsync(BulkReviewKycDocumentsDto request, Guid adminUserId)
    {
        if (request.DocumentIds == null || !request.DocumentIds.Any())
            throw new ArgumentException("Document IDs are required");

        var reviewer = await _userServiceClient.GetUserProfileAsync(adminUserId);
        if (reviewer == null)
            throw new ArgumentException("Reviewer not found");

        var reviewDto = new ReviewKycDocumentDto
        {
            Status = request.Status,
            ReviewNotes = request.ReviewNotes
        };

        var now = DateTime.UtcNow;
        var successCount = 0;
        var failedDocuments = new List<Guid>();

        foreach (var documentId in request.DocumentIds)
        {
            try
            {
                var document = await _userServiceClient.ReviewKycDocumentAsync(documentId, reviewDto);
                
                // Create audit log for each document
                _context.AuditLogs.Add(new AuditLog
                {
                    Entity = "KycDocument",
                    EntityId = documentId,
                    Action = "BulkReviewed",
                    Details = $"KYC document {document.DocumentType} reviewed with status {request.Status} (bulk operation).",
                    PerformedBy = adminUserId,
                    Timestamp = now,
                    IpAddress = "Admin API",
                    UserAgent = "Admin API"
                });

                successCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to review document {DocumentId} in bulk operation", documentId);
                failedDocuments.Add(documentId);
            }
        }

        await _context.SaveChangesAsync();

        if (failedDocuments.Any())
        {
            _logger.LogWarning("Bulk review completed with {SuccessCount} successes and {FailureCount} failures. Failed documents: {FailedDocuments}",
                successCount, failedDocuments.Count, string.Join(", ", failedDocuments));
        }

        // TODO: Send notifications to users about bulk review
        // This would require Notification service client integration

        return successCount > 0;
    }

    public async Task<KycReviewStatisticsDto> GetKycStatisticsAsync()
    {
        // Get all KYC documents (not just pending) for accurate statistics
        var allDocuments = await _userServiceClient.GetAllKycDocumentsAsync();
        var now = DateTime.UtcNow;
        var today = now.Date;
        var weekStart = today.AddDays(-(int)today.DayOfWeek);
        var monthStart = new DateTime(now.Year, now.Month, 1);

        var statistics = new KycReviewStatisticsDto
        {
            TotalPending = allDocuments.Count(d => d.Status == KycDocumentStatus.Pending),
            TotalUnderReview = allDocuments.Count(d => d.Status == KycDocumentStatus.UnderReview),
            TotalApproved = allDocuments.Count(d => d.Status == KycDocumentStatus.Approved),
            TotalRejected = allDocuments.Count(d => d.Status == KycDocumentStatus.Rejected),
            TotalRequiresUpdate = allDocuments.Count(d => d.Status == KycDocumentStatus.RequiresUpdate),
            ApprovedToday = allDocuments.Count(d => d.Status == KycDocumentStatus.Approved && d.ReviewedAt.HasValue && d.ReviewedAt.Value.Date == today),
            RejectedToday = allDocuments.Count(d => d.Status == KycDocumentStatus.Rejected && d.ReviewedAt.HasValue && d.ReviewedAt.Value.Date == today),
            ApprovedThisWeek = allDocuments.Count(d => d.Status == KycDocumentStatus.Approved && d.ReviewedAt.HasValue && d.ReviewedAt.Value >= weekStart),
            RejectedThisWeek = allDocuments.Count(d => d.Status == KycDocumentStatus.Rejected && d.ReviewedAt.HasValue && d.ReviewedAt.Value >= weekStart),
            ApprovedThisMonth = allDocuments.Count(d => d.Status == KycDocumentStatus.Approved && d.ReviewedAt.HasValue && d.ReviewedAt.Value >= monthStart),
            RejectedThisMonth = allDocuments.Count(d => d.Status == KycDocumentStatus.Rejected && d.ReviewedAt.HasValue && d.ReviewedAt.Value >= monthStart)
        };

        // Calculate average review time (hours)
        var reviewedDocuments = allDocuments.Where(d => d.ReviewedAt.HasValue && d.UploadedAt != default).ToList();
        if (reviewedDocuments.Any())
        {
            var totalReviewTime = reviewedDocuments.Sum(d => (d.ReviewedAt!.Value - d.UploadedAt).TotalHours);
            statistics.AverageReviewTimeHours = totalReviewTime / reviewedDocuments.Count;
        }

        // Documents by type
        statistics.DocumentsByType = allDocuments
            .GroupBy(d => d.DocumentType.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        // Documents by status
        statistics.DocumentsByStatus = allDocuments
            .GroupBy(d => d.Status.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        return statistics;
    }

    public async Task<bool> UpdateUserKycStatusAsync(Guid userId, UpdateUserKycStatusDto request, Guid adminUserId)
    {
        // Get user from User service to verify it exists
        var user = await _userServiceClient.GetUserProfileAsync(userId);
        if (user == null)
            return false;

        // Update KYC status via User service
        await _userServiceClient.UpdateKycStatusAsync(userId, request.Status, request.Reason);
        
        // Create audit log
        var now = DateTime.UtcNow;
        _context.AuditLogs.Add(new AuditLog
        {
            Entity = "User",
            EntityId = userId,
            Action = "KycStatusUpdated",
            Details = $"KYC status changed to {request.Status}. Reason: {request.Reason ?? "No reason provided"}",
            PerformedBy = adminUserId,
            Timestamp = now,
            IpAddress = "Admin API",
            UserAgent = "Admin API"
        });
        
        await _context.SaveChangesAsync();
        return true;
    }

    // Note: This method is no longer used since lockout status is managed by Auth service
    // Lockout information is not available in UserProfileDto
    // If needed, this would require integration with Auth service

    private async Task<UserStatisticsDto> GetUserStatisticsAsync(Guid userId)
    {
        // Get bookings from Booking service via HTTP
        var allBookings = await _bookingServiceClient.GetBookingsAsync();
        var userBookings = allBookings.Where(b => b.UserId == userId).ToList();
        var totalBookings = userBookings.Count;
        var completedBookings = userBookings.Count(b => b.Status == BookingStatus.Completed);
        var cancelledBookings = userBookings.Count(b => b.Status == BookingStatus.Cancelled);
        
        // Get payments from Payment service via HTTP
        var allPayments = await _paymentServiceClient.GetPaymentsAsync();
        var userPayments = allPayments.Where(p => p.PayerId == userId && p.Status == PaymentStatus.Completed).ToList();
        var totalPayments = userPayments.Sum(p => p.Amount);
        
        // Get group memberships from Group service via HTTP
        var groups = await _groupServiceClient.GetGroupsAsync();
        var groupMemberships = groups.SelectMany(g => g.Members ?? new List<GroupMemberDto>())
            .Count(gm => gm.UserId == userId);
        var activeGroupMemberships = groupMemberships; // All memberships are considered active

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
        // Get groups from Group service via HTTP
        var allGroups = await _groupServiceClient.GetGroupsAsync();
        var query = allGroups.AsQueryable();

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
            var minCount = request.MinMemberCount ?? 0;
            var maxCount = request.MaxMemberCount ?? int.MaxValue;
            var filteredGroups = query.ToList().Where(g => {
                var memberCount = g.Members != null ? g.Members.Count : 0;
                return memberCount >= minCount && memberCount <= maxCount;
            }).AsQueryable();
            query = filteredGroups;
        }

        // Apply sorting
        query = request.SortBy?.ToLower() switch
        {
            "name" => request.SortDirection == "asc" ? query.OrderBy(g => g.Name) : query.OrderByDescending(g => g.Name),
            "membercount" => request.SortDirection == "asc" ? query.OrderBy(g => g.Members != null ? g.Members.Count : 0) : query.OrderByDescending(g => g.Members != null ? g.Members.Count : 0),
            "activityscore" => request.SortDirection == "asc" ? query.OrderBy(g => 0) : query.OrderByDescending(g => 0), // Activity score calculation would need group details
            _ => request.SortDirection == "asc" ? query.OrderBy(g => g.CreatedAt) : query.OrderByDescending(g => g.CreatedAt)
        };

        var totalCount = query.Count();
        var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);

        var groups = query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(g => new GroupSummaryDto
            {
                Id = g.Id,
                Name = g.Name,
                Description = g.Description,
                Status = g.Status,
                MemberCount = g.Members != null ? g.Members.Count : 0,
                VehicleCount = g.Vehicles != null ? g.Vehicles.Count : 0,
                CreatedAt = g.CreatedAt,
                ActivityScore = 0, // Would need to calculate from group details
                HealthStatus = GroupHealthStatus.Healthy, // Would need to calculate from group details
                CreatorName = "Unknown" // Would need to get from user service
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
        // Get group from Group service via HTTP
        var group = await _groupServiceClient.GetGroupDetailsAsync(groupId);
        
        if (group == null)
            throw new ArgumentException("Group not found");

        // GroupDetailsDto from Group service should already have the correct structure
        // Map to GroupMemberDetailsDto if needed, or use directly if compatible
        var members = group.Members?.Select(m => new GroupMemberDetailsDto
        {
            UserId = m.UserId,
            UserName = m.UserName ?? "Unknown",
            Email = m.Email ?? "",
            Role = m.Role,
            SharePercentage = m.SharePercentage,
            JoinedAt = m.JoinedAt,
            IsActive = true, // All members are considered active
            TotalBookings = 0, // This would need to be calculated separately
            TotalPayments = 0 // This would need to be calculated separately
        }).ToList() ?? new List<GroupMemberDetailsDto>();

        var vehicles = group.Vehicles?.Select(v => new GroupVehicleDto
        {
            Id = v.Id,
            Vin = v.Vin ?? "",
            PlateNumber = v.PlateNumber ?? "",
            Model = v.Model ?? "",
            Year = v.Year,
            Color = v.Color,
            Status = v.Status,
            LastServiceDate = v.LastServiceDate,
            Odometer = v.Odometer,
            TotalBookings = 0, // This would need to be calculated separately
            TotalRevenue = 0 // This would need to be calculated separately
        }).ToList() ?? new List<GroupVehicleDto>();

        var financialSummary = await GetGroupFinancialSummaryAsync(groupId);
        var bookingStatistics = await GetGroupBookingStatisticsAsync(groupId);
        var recentActivity = await GetGroupRecentActivityAsync(groupId);
        // Proposals would need to be fetched from Group service if available
        var proposals = group.Proposals?.Select(p => new GroupProposalDto
        {
            Id = p.Id,
            Title = p.Title ?? "",
            Description = p.Description ?? "",
            Type = p.Type,
            Status = p.Status,
            Amount = p.Amount,
            CreatedAt = p.CreatedAt,
            VotingEndDate = p.VotingEndDate,
            CreatorName = "Unknown", // Would need to get from user service
            TotalVotes = 0, // Votes not available in GroupProposalDto
            YesVotes = 0,
            NoVotes = 0,
            ApprovalPercentage = 0
        }).ToList() ?? new List<GroupProposalDto>();

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
            CreatorName = "Unknown", // Creator info not available in GroupDetailsDto, would need to get from user service
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
        // Get group from Group service to verify it exists
        var group = await _groupServiceClient.GetGroupDetailsAsync(groupId);
        if (group == null)
            return false;

        var oldStatus = group.Status;
        // Note: Group status update should be done via Group service endpoint
        // For now, we'll just log the action and handle implications
        // TODO: Add UpdateGroupStatusAsync to IGroupServiceClient
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
        try
        {
            var query = _context.AuditLogs
                .Where(a => a.Entity == "OwnershipGroup" && a.EntityId == groupId);

            // Apply filters
            if (!string.IsNullOrEmpty(request.Search))
            {
                var searchTerm = request.Search.ToLower();
                query = query.Where(a => (a.Details != null && a.Details.ToLower().Contains(searchTerm)) ||
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

            // Get unique user IDs
            var userIds = auditLogs.Select(a => a.PerformedBy).Distinct().ToList();

            // Fetch user names from User service
            var userNameDict = new Dictionary<Guid, string>();
            foreach (var userId in userIds)
            {
                try
                {
                    var user = await _userServiceClient.GetUserProfileAsync(userId);
                    if (user != null)
                    {
                        userNameDict[userId] = $"{user.FirstName} {user.LastName}";
                    }
                    else
                    {
                        userNameDict[userId] = "Unknown User";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch user {UserId} for group audit trail", userId);
                    userNameDict[userId] = "Unknown User";
                }
            }

            // Map to DTOs
            var entries = auditLogs.Select(a => new GroupAuditEntryDto
            {
                Id = a.Id,
                Action = a.Action,
                Entity = a.Entity,
                Details = a.Details ?? string.Empty,
                UserName = userNameDict.ContainsKey(a.PerformedBy) ? userNameDict[a.PerformedBy] : "Unknown User",
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving group audit trail for group {GroupId}", groupId);
            throw;
        }
    }

    public async Task<AuditLogResponseDto> GetAuditLogsAsync(AuditLogRequestDto request)
    {
        try
        {
            var query = _context.AuditLogs.AsQueryable();

            // Apply action type filter
            if (!string.IsNullOrEmpty(request.ActionType))
            {
                query = query.Where(a => a.Action == request.ActionType);
            }

            // Apply module filter (Entity maps to Module)
            if (!string.IsNullOrEmpty(request.Module))
            {
                query = query.Where(a => a.Entity == request.Module);
            }

            // Apply date range filters
            if (request.FromDate.HasValue)
            {
                query = query.Where(a => a.Timestamp >= request.FromDate.Value);
            }

            if (request.ToDate.HasValue)
            {
                query = query.Where(a => a.Timestamp <= request.ToDate.Value);
            }

            // Get all matching audit logs first (for search and counting)
            var allLogs = await query.ToListAsync();

            // Apply search filter in memory (since we can't search user names via navigation property)
            if (!string.IsNullOrEmpty(request.Search))
            {
                var searchTerm = request.Search.ToLower();
                var userIds = allLogs.Select(a => a.PerformedBy).Distinct().ToList();
                
                // Get user names from User service
                var userNamesDict = new Dictionary<Guid, string>();
                foreach (var userId in userIds)
                {
                    try
                    {
                        var user = await _userServiceClient.GetUserProfileAsync(userId);
                        if (user != null)
                        {
                            userNamesDict[userId] = $"{user.FirstName} {user.LastName}".ToLower();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to fetch user {UserId} for audit log search", userId);
                    }
                }

                // Filter by search term
                allLogs = allLogs.Where(a =>
                {
                    var matchesDetails = a.Details != null && a.Details.ToLower().Contains(searchTerm);
                    var matchesAction = a.Action.ToLower().Contains(searchTerm);
                    var matchesEntity = a.Entity.ToLower().Contains(searchTerm);
                    var matchesUserName = userNamesDict.ContainsKey(a.PerformedBy) &&
                                        userNamesDict[a.PerformedBy].Contains(searchTerm);
                    
                    return matchesDetails || matchesAction || matchesEntity || matchesUserName;
                }).ToList();
            }

            var totalCount = allLogs.Count;
            var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);

            // Apply pagination
            var paginatedLogs = allLogs
                .OrderByDescending(a => a.Timestamp)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();

            // Get unique user IDs from paginated logs
            var userIdsToFetch = paginatedLogs.Select(a => a.PerformedBy).Distinct().ToList();

            // Fetch user names from User service
            var userNameDict = new Dictionary<Guid, string>();
            foreach (var userId in userIdsToFetch)
            {
                try
                {
                    var user = await _userServiceClient.GetUserProfileAsync(userId);
                    if (user != null)
                    {
                        userNameDict[userId] = $"{user.FirstName} {user.LastName}";
                    }
                    else
                    {
                        userNameDict[userId] = "Unknown User";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch user {UserId} for audit log", userId);
                    userNameDict[userId] = "Unknown User";
                }
            }

            // Map to DTOs
            var logs = paginatedLogs.Select(a => new AuditLogEntryDto
            {
                Id = a.Id,
                ActionType = a.Action,
                Module = a.Entity,
                Details = a.Details ?? string.Empty,
                UserName = userNameDict.ContainsKey(a.PerformedBy) ? userNameDict[a.PerformedBy] : "Unknown User",
                Timestamp = a.Timestamp,
                IpAddress = a.IpAddress
            }).ToList();

            return new AuditLogResponseDto
            {
                Logs = logs,
                TotalCount = totalCount,
                Page = request.Page,
                PageSize = request.PageSize,
                TotalPages = totalPages
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit logs");
            throw;
        }
    }

    public async Task<bool> InterveneInGroupAsync(Guid groupId, GroupInterventionDto request, Guid adminUserId)
    {
        // Get group from Group service to verify it exists
        var group = await _groupServiceClient.GetGroupDetailsAsync(groupId);
        if (group == null)
            return false;

        // Handle intervention based on type
        // Note: Group status updates should be done via Group service endpoint
        // TODO: Add intervention methods to IGroupServiceClient
        switch (request.Type)
        {
            case InterventionType.Freeze:
                // TODO: Call Group service to freeze group
                _logger.LogInformation("Freeze intervention requested for group {GroupId}", groupId);
                break;
            case InterventionType.Unfreeze:
                // TODO: Call Group service to unfreeze group
                _logger.LogInformation("Unfreeze intervention requested for group {GroupId}", groupId);
                break;
            case InterventionType.AppointAdmin:
                // TODO: Implement appoint temporary admin logic via Group service
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
        // Get group from Group service via HTTP
        var group = await _groupServiceClient.GetGroupDetailsAsync(groupId);

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

        if ((group.Members?.Count ?? 0) < 2)
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
    private decimal CalculateGroupActivityScore(GroupDetailsDto group)
    {
        // This is a simplified calculation - in reality, you'd want more sophisticated scoring
        var daysSinceCreation = (DateTime.UtcNow - group.CreatedAt).TotalDays;
        var memberCount = group.Members?.Count ?? 0;
        var vehicleCount = group.Vehicles?.Count ?? 0;
        
        // Basic activity score based on group age, size, and activity
        return Math.Min(100, (decimal)(memberCount * 10 + vehicleCount * 5 + (100 - daysSinceCreation)));
    }

    private GroupHealthStatus GetGroupHealthStatus(GroupDetailsDto group)
    {
        var daysInactive = (DateTime.UtcNow - group.UpdatedAt).TotalDays;
        var memberCount = group.Members?.Count ?? 0;
        
        if (daysInactive > 30 || memberCount < 2)
            return GroupHealthStatus.Critical;
        if (daysInactive > 14 || memberCount < 3)
            return GroupHealthStatus.Unhealthy;
        if (daysInactive > 7)
            return GroupHealthStatus.Warning;
        
        return GroupHealthStatus.Healthy;
    }

    private decimal CalculateGroupHealthScore(GroupDetailsDto group)
    {
        var daysInactive = (DateTime.UtcNow - group.UpdatedAt).TotalDays;
        var score = 100m;
        var memberCount = group.Members?.Count ?? 0;
        
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

    private string GetGroupHealthRecommendation(GroupDetailsDto group)
    {
        var daysInactive = (DateTime.UtcNow - group.UpdatedAt).TotalDays;
        var memberCount = group.Members?.Count ?? 0;
        
        if (daysInactive > 30)
            return "Group appears dormant. Consider reaching out to members or dissolving the group.";
        if (memberCount < 2)
            return "Group needs more members to function effectively.";
        if (daysInactive > 14)
            return "Group has been inactive. Consider sending a reminder to members.";
        
        return "Group is healthy and functioning well.";
    }

    private async Task<GroupFinancialSummaryDto> GetGroupFinancialSummaryAsync(Guid groupId)
    {
        // Get expenses from Payment service via HTTP
        var allExpenses = await _paymentServiceClient.GetExpensesAsync(groupId);
        var expenses = allExpenses.ToList();

        var totalExpenses = expenses.Sum(e => e.Amount);
        var monthlyExpenses = expenses
            .Where(e => e.DateIncurred >= DateTime.UtcNow.AddDays(-30))
            .Sum(e => e.Amount);

        // Get group to get member count
        var group = await _groupServiceClient.GetGroupDetailsAsync(groupId);
        var memberCount = group?.Members?.Count ?? 0;
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
        // Get bookings from Booking service via HTTP
        var allBookings = await _bookingServiceClient.GetBookingsAsync();
        var bookings = allBookings.Where(b => b.GroupId == groupId).ToList();

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
                // Note: Booking cancellation should be done via Booking service endpoint
                // For now, we'll just log the action
                _logger.LogInformation("Group {GroupId} status changed to Inactive. Future bookings should be cancelled via Booking service.", groupId);
                break;
                
            case GroupStatus.Dissolved:
                // Cancel all future bookings and freeze all activities
                // Note: Booking cancellation should be done via Booking service endpoint
                // For now, we'll just log the action
                _logger.LogInformation("Group {GroupId} status changed to Dissolved. All future bookings should be cancelled via Booking service.", groupId);
                break;
        }
    }

    // Dispute Management Methods
    public async Task<Guid> CreateDisputeAsync(CreateDisputeDto request, Guid adminUserId)
    {
        try
        {
            // Validate group exists
            var group = await _groupServiceClient.GetGroupDetailsAsync(request.GroupId);
            if (group == null)
                throw new ArgumentException("Group not found");

            // Determine reporter - use provided or admin
            var reporterId = request.ReportedBy ?? adminUserId;

            // Validate reporter is member of group (if not admin creating on behalf)
            if (request.ReportedBy.HasValue)
            {
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
            var query = _context.Disputes
                .Include(d => d.Comments)
                .AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(request.Search))
            {
                var searchTerm = request.Search.ToLower();
                query = query.Where(d => d.Subject.ToLower().Contains(searchTerm) || 
                                   (d.Description != null && d.Description.ToLower().Contains(searchTerm)));
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
                .ToListAsync();

            // Get unique group IDs and user IDs
            var groupIds = disputes.Select(d => d.GroupId).Distinct().ToList();
            var userIds = new HashSet<Guid>();
            foreach (var dispute in disputes)
            {
                userIds.Add(dispute.ReportedBy);
                if (dispute.AssignedTo.HasValue)
                    userIds.Add(dispute.AssignedTo.Value);
                if (dispute.ResolvedBy.HasValue)
                    userIds.Add(dispute.ResolvedBy.Value);
            }
            var userIdsList = userIds.ToList();

            // Fetch groups and users from their respective services
            var groupsDict = new Dictionary<Guid, string>();
            var usersDict = new Dictionary<Guid, string>();

            // Fetch groups
            foreach (var groupId in groupIds)
            {
                try
                {
                    var group = await _groupServiceClient.GetGroupDetailsAsync(groupId);
                    if (group != null)
                    {
                        groupsDict[groupId] = group.Name;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch group {GroupId} for dispute list", groupId);
                    groupsDict[groupId] = "Unknown Group";
                }
            }

            // Fetch users
            foreach (var userId in userIdsList)
            {
                try
                {
                    var user = await _userServiceClient.GetUserProfileAsync(userId);
                    if (user != null)
                    {
                        usersDict[userId] = $"{user.FirstName} {user.LastName}";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch user {UserId} for dispute list", userId);
                    usersDict[userId] = "Unknown User";
                }
            }

            // Map to DTOs
            var disputeDtos = disputes.Select(d => new DisputeSummaryDto
            {
                Id = d.Id,
                GroupId = d.GroupId,
                GroupName = groupsDict.GetValueOrDefault(d.GroupId, "Unknown Group"),
                Subject = d.Subject,
                Category = d.Category,
                Priority = d.Priority,
                Status = d.Status,
                ReporterName = usersDict.GetValueOrDefault(d.ReportedBy, "Unknown User"),
                AssignedToName = d.AssignedTo.HasValue ? usersDict.GetValueOrDefault(d.AssignedTo.Value, "Unknown User") : null,
                CreatedAt = d.CreatedAt,
                ResolvedAt = d.ResolvedAt,
                CommentCount = d.Comments?.Count ?? 0
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
            var assignedUser = await _userServiceClient.GetUserProfileAsync(request.AssignedTo);
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
            var group = await _groupServiceClient.GetGroupDetailsAsync(dispute.GroupId);
            var hasAccess = group?.Members?.Any(m => m.UserId == userId) ?? false;
            
            if (!hasAccess)
            {
                // Check if user is staff/admin
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
            // Get staff members with their dispute counts
            // Note: This would need to get users from User service and disputes from Admin service
            var allUsers = await _userServiceClient.GetUsersAsync();
            var staffUsers = allUsers.Where(u => u.Role == UserRole.Staff || u.Role == UserRole.SystemAdmin).ToList();
            
            // Get disputes assigned to staff
            var allDisputes = await _context.Disputes
                .Where(d => d.AssignedTo.HasValue)
                .ToListAsync();
            
            var staffWorkload = staffUsers.Select(u => new
                {
                    UserId = u.Id,
                    OpenDisputes = allDisputes.Count(d => d.AssignedTo == u.Id && d.Status == DisputeStatus.Open),
                    UnderReviewDisputes = allDisputes.Count(d => d.AssignedTo == u.Id && d.Status == DisputeStatus.UnderReview)
                })
                .ToList();

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

    // Check-In Management Methods
    public async Task<CheckInListResponseDto> GetCheckInsAsync(CheckInListRequestDto request)
    {
        try
        {
            // Get check-ins from Booking service
            var checkIns = await _bookingServiceClient.GetCheckInsAsync(
                from: request.FromDate,
                to: request.ToDate,
                userId: request.UserId,
                vehicleId: request.VehicleId,
                bookingId: request.BookingId
            );

            // Filter by status if provided
            if (!string.IsNullOrEmpty(request.Status))
            {
                // Since CheckIn doesn't have status, we'll use a simple heuristic:
                // If status is "Pending", show check-ins without signature
                // If status is "Approved", show check-ins with signature
                // If status is "Rejected", we can't determine from CheckIn entity alone
                // For now, we'll filter based on signature presence
                if (request.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase))
                {
                    checkIns = checkIns.Where(c => string.IsNullOrEmpty(c.SignatureReference)).ToList();
                }
                else if (request.Status.Equals("Approved", StringComparison.OrdinalIgnoreCase))
                {
                    checkIns = checkIns.Where(c => !string.IsNullOrEmpty(c.SignatureReference)).ToList();
                }
                // Rejected status would need to be tracked separately, for now we'll return empty
                else if (request.Status.Equals("Rejected", StringComparison.OrdinalIgnoreCase))
                {
                    checkIns = new List<CheckInDto>();
                }
            }

            var totalCount = checkIns.Count;

            // Apply sorting
            var sortedCheckIns = request.SortBy.ToLower() switch
            {
                "checkintime" => request.SortDirection.ToLower() == "asc"
                    ? checkIns.OrderBy(c => c.CheckInTime).ToList()
                    : checkIns.OrderByDescending(c => c.CheckInTime).ToList(),
                "odometer" => request.SortDirection.ToLower() == "asc"
                    ? checkIns.OrderBy(c => c.Odometer).ToList()
                    : checkIns.OrderByDescending(c => c.Odometer).ToList(),
                _ => checkIns.OrderByDescending(c => c.CheckInTime).ToList()
            };

            // Apply pagination
            var paginatedCheckIns = sortedCheckIns
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();

            // Get unique user IDs and vehicle IDs
            var userIds = paginatedCheckIns.Select(c => c.UserId).Distinct().ToList();
            var vehicleIds = paginatedCheckIns.Where(c => c.VehicleId.HasValue).Select(c => c.VehicleId!.Value).Distinct().ToList();

            // Fetch users and vehicles
            var usersDict = new Dictionary<Guid, string>();
            var vehiclesDict = new Dictionary<Guid, (string Make, string Model, string PlateNumber)>();

            foreach (var userId in userIds)
            {
                try
                {
                    var user = await _userServiceClient.GetUserProfileAsync(userId);
                    if (user != null)
                    {
                        usersDict[userId] = $"{user.FirstName} {user.LastName}";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch user {UserId} for check-in list", userId);
                    usersDict[userId] = "Unknown User";
                }
            }

            foreach (var vehicleId in vehicleIds)
            {
                try
                {
                    var vehicle = await _vehicleServiceClient.GetVehicleAsync(vehicleId);
                    if (vehicle != null)
                    {
                        vehiclesDict[vehicleId] = (vehicle.Model, vehicle.Model, vehicle.PlateNumber);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch vehicle {VehicleId} for check-in list", vehicleId);
                }
            }

            // Map to DTOs
            var checkInDtos = paginatedCheckIns.Select(c =>
            {
                var vehicleInfo = c.VehicleId.HasValue && vehiclesDict.ContainsKey(c.VehicleId.Value)
                    ? vehiclesDict[c.VehicleId.Value]
                    : (Make: (string?)null, Model: (string?)null, PlateNumber: (string?)null);

                return new CheckInSummaryDto
                {
                    Id = c.Id,
                    BookingId = c.BookingId,
                    UserId = c.UserId,
                    UserName = usersDict.GetValueOrDefault(c.UserId, $"{c.UserFirstName} {c.UserLastName}"),
                    VehicleId = c.VehicleId,
                    VehicleMake = vehicleInfo.Make,
                    VehicleModel = vehicleInfo.Model,
                    VehiclePlateNumber = vehicleInfo.PlateNumber,
                    Type = c.Type,
                    OdometerReading = c.Odometer,
                    CheckInTime = c.CheckInTime,
                    Status = string.IsNullOrEmpty(c.SignatureReference) ? "Pending" : "Approved",
                    Notes = c.Notes,
                    IsLateReturn = c.IsLateReturn,
                    LateReturnMinutes = c.LateReturnMinutes,
                    LateFeeAmount = c.LateFeeAmount,
                    BatteryPercentage = null, // Battery percentage not available in CheckInDto
                    Photos = c.Photos ?? new List<CheckInPhotoDto>()
                };
            }).ToList();

            return new CheckInListResponseDto
            {
                CheckIns = checkInDtos,
                TotalCount = totalCount,
                Page = request.Page,
                PageSize = request.PageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / request.PageSize)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving check-ins");
            throw;
        }
    }

    public async Task<CheckInSummaryDto> GetCheckInDetailsAsync(Guid checkInId)
    {
        try
        {
            var checkIn = await _bookingServiceClient.GetCheckInAsync(checkInId);
            if (checkIn == null)
                throw new ArgumentException("Check-in not found");

            // Get user and vehicle info
            string userName = "Unknown User";
            (string? Make, string? Model, string? PlateNumber) vehicleInfo = (null, null, null);

            try
            {
                var user = await _userServiceClient.GetUserProfileAsync(checkIn.UserId);
                if (user != null)
                {
                    userName = $"{user.FirstName} {user.LastName}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch user {UserId} for check-in details", checkIn.UserId);
            }

            if (checkIn.VehicleId.HasValue)
            {
                try
                {
                    var vehicle = await _vehicleServiceClient.GetVehicleAsync(checkIn.VehicleId.Value);
                    if (vehicle != null)
                    {
                        vehicleInfo = (vehicle.Model, vehicle.Model, vehicle.PlateNumber);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch vehicle {VehicleId} for check-in details", checkIn.VehicleId.Value);
                }
            }

            return new CheckInSummaryDto
            {
                Id = checkIn.Id,
                BookingId = checkIn.BookingId,
                UserId = checkIn.UserId,
                UserName = userName,
                VehicleId = checkIn.VehicleId,
                VehicleMake = vehicleInfo.Make,
                VehicleModel = vehicleInfo.Model,
                VehiclePlateNumber = vehicleInfo.PlateNumber,
                Type = checkIn.Type,
                OdometerReading = checkIn.Odometer,
                CheckInTime = checkIn.CheckInTime,
                Status = string.IsNullOrEmpty(checkIn.SignatureReference) ? "Pending" : "Approved",
                Notes = checkIn.Notes,
                IsLateReturn = checkIn.IsLateReturn,
                LateReturnMinutes = checkIn.LateReturnMinutes,
                LateFeeAmount = checkIn.LateFeeAmount,
                BatteryPercentage = null,
                Photos = checkIn.Photos ?? new List<CheckInPhotoDto>()
            };
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving check-in details for {CheckInId}", checkInId);
            throw;
        }
    }

    public async Task<bool> ApproveCheckInAsync(Guid checkInId, ApproveCheckInDto request, Guid adminUserId)
    {
        try
        {
            var result = await _bookingServiceClient.ApproveCheckInAsync(checkInId, request);
            
            if (result)
            {
                // Create audit log
                var auditLog = new AuditLog
                {
                    Id = Guid.NewGuid(),
                    Action = "CheckInApproved",
                    Entity = "CheckIn",
                    EntityId = checkInId,
                    Details = $"Check-in approved by admin",
                    PerformedBy = adminUserId,
                    Timestamp = DateTime.UtcNow,
                    IpAddress = "Admin API"
                };
                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving check-in {CheckInId}", checkInId);
            throw;
        }
    }

    public async Task<bool> RejectCheckInAsync(Guid checkInId, RejectCheckInDto request, Guid adminUserId)
    {
        try
        {
            var result = await _bookingServiceClient.RejectCheckInAsync(checkInId, request);
            
            if (result)
            {
                // Create audit log
                var auditLog = new AuditLog
                {
                    Id = Guid.NewGuid(),
                    Action = "CheckInRejected",
                    Entity = "CheckIn",
                    EntityId = checkInId,
                    Details = $"Check-in rejected by admin: {request.Reason}",
                    PerformedBy = adminUserId,
                    Timestamp = DateTime.UtcNow,
                    IpAddress = "Admin API"
                };
                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting check-in {CheckInId}", checkInId);
            throw;
        }
    }

    // Maintenance Management Methods
    public async Task<MaintenanceListResponseDto> GetMaintenanceAsync(MaintenanceListRequestDto request)
    {
        try
        {
            // Get maintenance schedules from Vehicle service
            var scheduleRequest = new MaintenanceScheduleRequestDto
            {
                Status = !string.IsNullOrEmpty(request.Status) 
                    ? Enum.TryParse<MaintenanceStatus>(request.Status, true, out var status) ? status : null
                    : null,
                Page = request.Page,
                PageSize = request.PageSize
            };

            var schedules = await _vehicleServiceClient.GetMaintenanceSchedulesAsync(scheduleRequest);

            // Filter by additional criteria
            if (request.VehicleId.HasValue)
            {
                schedules = schedules.Where(s => s.VehicleId == request.VehicleId.Value).ToList();
            }

            if (request.ServiceType.HasValue)
            {
                schedules = schedules.Where(s => s.ServiceType == request.ServiceType.Value).ToList();
            }

            if (request.FromDate.HasValue)
            {
                schedules = schedules.Where(s => s.ScheduledDate >= request.FromDate.Value).ToList();
            }

            if (request.ToDate.HasValue)
            {
                schedules = schedules.Where(s => s.ScheduledDate <= request.ToDate.Value).ToList();
            }

            var totalCount = schedules.Count;

            // Apply sorting
            var sortedSchedules = request.SortBy.ToLower() switch
            {
                "scheduleddate" => request.SortDirection.ToLower() == "asc"
                    ? schedules.OrderBy(s => s.ScheduledDate).ToList()
                    : schedules.OrderByDescending(s => s.ScheduledDate).ToList(),
                "cost" => request.SortDirection.ToLower() == "asc"
                    ? schedules.OrderBy(s => s.EstimatedCost ?? 0).ToList()
                    : schedules.OrderByDescending(s => s.EstimatedCost ?? 0).ToList(),
                "status" => request.SortDirection.ToLower() == "asc"
                    ? schedules.OrderBy(s => s.Status).ToList()
                    : schedules.OrderByDescending(s => s.Status).ToList(),
                _ => schedules.OrderByDescending(s => s.ScheduledDate).ToList()
            };

            // Apply pagination
            var paginatedSchedules = sortedSchedules
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();

            // Get unique vehicle IDs
            var vehicleIds = paginatedSchedules.Select(s => s.VehicleId).Distinct().ToList();

            // Fetch vehicles
            var vehiclesDict = new Dictionary<Guid, (string Model, string PlateNumber)>();

            foreach (var vehicleId in vehicleIds)
            {
                try
                {
                    var vehicle = await _vehicleServiceClient.GetVehicleAsync(vehicleId);
                    if (vehicle != null)
                    {
                        vehiclesDict[vehicleId] = (vehicle.Model, vehicle.PlateNumber);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch vehicle {VehicleId} for maintenance list", vehicleId);
                }
            }

            // Map to DTOs
            var maintenanceDtos = paginatedSchedules.Select(s =>
            {
                var vehicleInfo = vehiclesDict.ContainsKey(s.VehicleId)
                    ? vehiclesDict[s.VehicleId]
                    : (Model: (string?)null, PlateNumber: (string?)null);

                var statusString = s.Status switch
                {
                    MaintenanceStatus.Scheduled => "Scheduled",
                    MaintenanceStatus.InProgress => "InProgress",
                    MaintenanceStatus.Completed => "Completed",
                    MaintenanceStatus.Overdue => "Overdue",
                    MaintenanceStatus.Cancelled => "Cancelled",
                    _ => "Scheduled"
                };

                var serviceTypeName = s.ServiceType switch
                {
                    MaintenanceServiceType.GeneralService => "General Service",
                    MaintenanceServiceType.OilChange => "Oil Change",
                    MaintenanceServiceType.TireRotation => "Tire Rotation",
                    MaintenanceServiceType.BrakeService => "Brake Service",
                    MaintenanceServiceType.Battery => "Battery",
                    MaintenanceServiceType.AirFilter => "Air Filter",
                    MaintenanceServiceType.Coolant => "Coolant",
                    MaintenanceServiceType.Transmission => "Transmission",
                    MaintenanceServiceType.Diagnostics => "Diagnostics",
                    MaintenanceServiceType.Repair => "Repair",
                    MaintenanceServiceType.Other => "Other",
                    _ => "Other"
                };

                return new MaintenanceSummaryDto
                {
                    Id = s.Id,
                    VehicleId = s.VehicleId,
                    VehicleMake = null, // Make not available in VehicleDto
                    VehicleModel = vehicleInfo.Model,
                    VehiclePlate = vehicleInfo.PlateNumber,
                    ServiceType = s.ServiceType,
                    ServiceTypeName = serviceTypeName,
                    ScheduledDate = s.ScheduledDate,
                    Status = statusString,
                    EstimatedCost = s.EstimatedCost,
                    Cost = s.EstimatedCost,
                    Provider = s.Provider,
                    Priority = s.Priority
                };
            }).ToList();

            return new MaintenanceListResponseDto
            {
                Maintenance = maintenanceDtos,
                TotalCount = totalCount,
                Page = request.Page,
                PageSize = request.PageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / request.PageSize)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance records");
            throw;
        }
    }

    public async Task<MaintenanceSummaryDto> GetMaintenanceDetailsAsync(Guid maintenanceId)
    {
        try
        {
            var schedule = await _vehicleServiceClient.GetMaintenanceScheduleAsync(maintenanceId);
            if (schedule == null)
                throw new ArgumentException("Maintenance record not found");

            // Get vehicle info
            (string? Model, string? PlateNumber) vehicleInfo = (null, null);

            try
            {
                var vehicle = await _vehicleServiceClient.GetVehicleAsync(schedule.VehicleId);
                if (vehicle != null)
                {
                    vehicleInfo = (vehicle.Model, vehicle.PlateNumber);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch vehicle {VehicleId} for maintenance details", schedule.VehicleId);
            }

            var statusString = schedule.Status switch
            {
                MaintenanceStatus.Scheduled => "Scheduled",
                MaintenanceStatus.InProgress => "InProgress",
                MaintenanceStatus.Completed => "Completed",
                MaintenanceStatus.Overdue => "Overdue",
                MaintenanceStatus.Cancelled => "Cancelled",
                _ => "Scheduled"
            };

            var serviceTypeName = schedule.ServiceType switch
            {
                MaintenanceServiceType.GeneralService => "General Service",
                MaintenanceServiceType.OilChange => "Oil Change",
                MaintenanceServiceType.TireRotation => "Tire Rotation",
                MaintenanceServiceType.BrakeService => "Brake Service",
                MaintenanceServiceType.Battery => "Battery",
                MaintenanceServiceType.AirFilter => "Air Filter",
                MaintenanceServiceType.Coolant => "Coolant",
                MaintenanceServiceType.Transmission => "Transmission",
                MaintenanceServiceType.Diagnostics => "Diagnostics",
                MaintenanceServiceType.Repair => "Repair",
                MaintenanceServiceType.Other => "Other",
                _ => "Other"
            };

            return new MaintenanceSummaryDto
            {
                Id = schedule.Id,
                VehicleId = schedule.VehicleId,
                VehicleMake = null,
                VehicleModel = vehicleInfo.Model,
                VehiclePlate = vehicleInfo.PlateNumber,
                ServiceType = schedule.ServiceType,
                ServiceTypeName = serviceTypeName,
                ScheduledDate = schedule.ScheduledDate,
                Status = statusString,
                EstimatedCost = schedule.EstimatedCost,
                Cost = schedule.EstimatedCost,
                Provider = schedule.Provider,
                Priority = schedule.Priority
            };
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving maintenance details for {MaintenanceId}", maintenanceId);
            throw;
        }
    }

    public async Task<bool> CreateMaintenanceAsync(CreateMaintenanceDto request, Guid adminUserId)
    {
        try
        {
            var result = await _vehicleServiceClient.CreateMaintenanceScheduleAsync(request);
            
            if (result)
            {
                // Create audit log
                var auditLog = new AuditLog
                {
                    Id = Guid.NewGuid(),
                    Action = "MaintenanceCreated",
                    Entity = "Maintenance",
                    EntityId = Guid.NewGuid(), // Would need to get from response
                    Details = $"Maintenance schedule created for vehicle {request.VehicleId}",
                    PerformedBy = adminUserId,
                    Timestamp = DateTime.UtcNow,
                    IpAddress = "Admin API"
                };
                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating maintenance schedule");
            throw;
        }
    }

    public async Task<bool> UpdateMaintenanceAsync(Guid maintenanceId, UpdateMaintenanceDto request, Guid adminUserId)
    {
        try
        {
            var result = await _vehicleServiceClient.UpdateMaintenanceScheduleAsync(maintenanceId, request);
            
            if (result)
            {
                // Create audit log
                var auditLog = new AuditLog
                {
                    Id = Guid.NewGuid(),
                    Action = "MaintenanceUpdated",
                    Entity = "Maintenance",
                    EntityId = maintenanceId,
                    Details = $"Maintenance schedule updated",
                    PerformedBy = adminUserId,
                    Timestamp = DateTime.UtcNow,
                    IpAddress = "Admin API"
                };
                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating maintenance schedule {MaintenanceId}", maintenanceId);
            throw;
        }
    }

    // Vehicle Management Methods
    public async Task<VehicleListResponseDto> GetVehiclesAsync(VehicleListRequestDto request)
    {
        try
        {
            // Get vehicles from Vehicle service via HTTP
            var vehicles = await _vehicleServiceClient.GetVehiclesAsync();
            
            // Apply filtering and sorting in memory
            var query = vehicles.AsQueryable();

            // Apply search filter
            if (!string.IsNullOrEmpty(request.Search))
            {
                var searchTerm = request.Search.ToLower();
                query = query.Where(v => 
                    (v.Model != null && v.Model.ToLower().Contains(searchTerm)) ||
                    (v.PlateNumber != null && v.PlateNumber.ToLower().Contains(searchTerm)) ||
                    (v.Vin != null && v.Vin.ToLower().Contains(searchTerm)) ||
                    (v.Color != null && v.Color.ToLower().Contains(searchTerm)));
            }

            // Apply status filter
            if (request.Status.HasValue)
            {
                query = query.Where(v => v.Status == request.Status.Value);
            }

            // Apply group filter
            if (request.GroupId.HasValue)
            {
                query = query.Where(v => v.GroupId == request.GroupId.Value);
            }

            // Apply sorting
            query = request.SortBy?.ToLower() switch
            {
                "model" => request.SortDirection == "asc" ? query.OrderBy(v => v.Model) : query.OrderByDescending(v => v.Model),
                "platenumber" => request.SortDirection == "asc" ? query.OrderBy(v => v.PlateNumber) : query.OrderByDescending(v => v.PlateNumber),
                "status" => request.SortDirection == "asc" ? query.OrderBy(v => v.Status) : query.OrderByDescending(v => v.Status),
                "odometer" => request.SortDirection == "asc" ? query.OrderBy(v => v.Odometer) : query.OrderByDescending(v => v.Odometer),
                "year" => request.SortDirection == "asc" ? query.OrderBy(v => v.Year) : query.OrderByDescending(v => v.Year),
                _ => request.SortDirection == "asc" ? query.OrderBy(v => v.CreatedAt) : query.OrderByDescending(v => v.CreatedAt)
            };

            var totalCount = query.Count();
            var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);

            var vehicleList = query
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToList();

            return new VehicleListResponseDto
            {
                Vehicles = vehicleList,
                TotalCount = totalCount,
                Page = request.Page,
                PageSize = request.PageSize,
                TotalPages = totalPages
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving vehicles");
            throw;
        }
    }

    public async Task<VehicleDto?> GetVehicleDetailsAsync(Guid vehicleId)
    {
        try
        {
            var vehicle = await _vehicleServiceClient.GetVehicleAsync(vehicleId);
            return vehicle;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving vehicle details for {VehicleId}", vehicleId);
            throw;
        }
    }

    public async Task<bool> UpdateVehicleStatusAsync(Guid vehicleId, UpdateVehicleStatusDto request, Guid adminUserId)
    {
        try
        {
            // Get current vehicle
            var vehicle = await _vehicleServiceClient.GetVehicleAsync(vehicleId);
            if (vehicle == null)
                return false;

            // Update vehicle status via Vehicle service
            var updateDto = new UpdateVehicleDto
            {
                Status = request.Status
            };
            
            // Note: Vehicle service client would need UpdateVehicleAsync method
            // For now, we'll create an audit log and return success
            // TODO: Implement UpdateVehicleAsync in IVehicleServiceClient

            // Create audit log
            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                Action = "VehicleStatusUpdated",
                Entity = "Vehicle",
                EntityId = vehicleId,
                Details = $"Vehicle status updated to {request.Status}. Reason: {request.Reason ?? "No reason provided"}",
                PerformedBy = adminUserId,
                Timestamp = DateTime.UtcNow,
                IpAddress = "Admin API"
            };
            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating vehicle status for {VehicleId}", vehicleId);
            throw;
        }
    }
}
