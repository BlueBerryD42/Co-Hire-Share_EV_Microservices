using CoOwnershipVehicle.Domain.Enums;
using CoOwnershipVehicle.Vehicle.Api.Data;
using CoOwnershipVehicle.Vehicle.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace CoOwnershipVehicle.Vehicle.Api.Services;

/// <summary>
/// Service for calculating vehicle usage statistics and analytics
/// </summary>
public class VehicleStatisticsService
{
    private readonly VehicleDbContext _context;
    private readonly IBookingServiceClient _bookingClient;
    private readonly ILogger<VehicleStatisticsService> _logger;

    public VehicleStatisticsService(
        VehicleDbContext context,
        IBookingServiceClient bookingClient,
        ILogger<VehicleStatisticsService> logger)
    {
        _context = context;
        _bookingClient = bookingClient;
        _logger = logger;
    }

    /// <summary>
    /// Get comprehensive vehicle usage statistics
    /// </summary>
    public async Task<VehicleStatisticsResponse> GetVehicleStatisticsAsync(
        Guid vehicleId,
        VehicleStatisticsRequest request,
        string accessToken)
    {
        // 1. Validate vehicle exists and get vehicle info
        var vehicle = await _context.Vehicles
            .FirstOrDefaultAsync(v => v.Id == vehicleId);

        if (vehicle == null)
        {
            throw new InvalidOperationException($"Vehicle {vehicleId} not found");
        }

        // 2. Set date range defaults
        var endDate = request.EndDate ?? DateTime.UtcNow;
        var startDate = request.StartDate ?? endDate.AddDays(-30);
        var totalDays = (int)(endDate - startDate).TotalDays + 1;

        // 3. Get booking statistics from Booking service
        var bookingStats = await _bookingClient.GetVehicleBookingStatisticsAsync(
            vehicleId, startDate, endDate, accessToken);

        // If booking service is unavailable, use empty data
        bookingStats ??= new VehicleBookingStatistics
        {
            VehicleId = vehicleId,
            StartDate = startDate,
            EndDate = endDate,
            CompletedBookings = new List<CompletedBookingDto>(),
            TotalBookings = 0,
            CompletedBookingsCount = 0,
            CancelledBookings = 0,
            TotalDistance = 0,
            TotalRevenue = 0,
            TotalUsageHours = 0
        };

        // 4. Get maintenance data
        var maintenanceSchedules = await _context.MaintenanceSchedules
            .Where(s => s.VehicleId == vehicleId &&
                       s.ScheduledDate >= startDate &&
                       s.ScheduledDate <= endDate)
            .ToListAsync();

        // 5. Calculate basic usage metrics
        var usageMetrics = CalculateUsageMetrics(bookingStats);

        // 6. Calculate utilization metrics
        var utilizationMetrics = CalculateUtilizationMetrics(
            bookingStats, maintenanceSchedules, totalDays);

        // 7. Calculate efficiency metrics
        var efficiencyMetrics = CalculateEfficiencyMetrics(bookingStats, vehicle);

        // 8. Analyze usage patterns
        var patterns = AnalyzeUsagePatterns(bookingStats.CompletedBookings);

        // 9. Generate trend data
        var trends = GenerateTrendData(
            bookingStats.CompletedBookings, startDate, endDate, request.GroupBy);

        // 10. Calculate comparison to previous period
        var comparison = await CalculatePeriodComparisonAsync(
            vehicleId, startDate, endDate, accessToken);

        // 11. Calculate benchmarks (if requested)
        BenchmarkData? benchmarks = null;
        if (request.IncludeBenchmarks && vehicle.GroupId.HasValue)
        {
            benchmarks = await CalculateBenchmarksAsync(
                vehicleId, vehicle.GroupId.Value, vehicle.Model, vehicle.Year,
                startDate, endDate, accessToken);
        }

        // 12. Build response
        return new VehicleStatisticsResponse
        {
            VehicleId = vehicleId,
            VehicleName = vehicle.Model,
            PlateNumber = vehicle.PlateNumber,
            StartDate = startDate,
            EndDate = endDate,
            TotalDays = totalDays,
            Usage = usageMetrics,
            Utilization = utilizationMetrics,
            Efficiency = efficiencyMetrics,
            Patterns = patterns,
            TrendsOverTime = trends,
            Comparison = comparison,
            Benchmarks = benchmarks
        };
    }

    #region Private Calculation Methods

    private UsageMetrics CalculateUsageMetrics(VehicleBookingStatistics bookingStats)
    {
        var completedBookings = bookingStats.CompletedBookings;

        // Find most frequent user
        MostFrequentUser? mostFrequentUser = null;
        if (completedBookings.Any())
        {
            var userGroups = completedBookings
                .GroupBy(b => b.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    FirstName = g.First().UserFirstName,
                    LastName = g.First().UserLastName,
                    Email = g.First().UserEmail,
                    TripCount = g.Count(),
                    TotalDistance = g.Sum(b => b.Distance ?? 0),
                    TotalUsageHours = g.Sum(b => b.UsageHours)
                })
                .OrderByDescending(u => u.TripCount)
                .FirstOrDefault();

            if (userGroups != null)
            {
                mostFrequentUser = new MostFrequentUser
                {
                    UserId = userGroups.UserId,
                    FirstName = userGroups.FirstName,
                    LastName = userGroups.LastName,
                    Email = userGroups.Email,
                    TripCount = userGroups.TripCount,
                    TotalDistance = userGroups.TotalDistance,
                    TotalUsageHours = userGroups.TotalUsageHours
                };
            }
        }

        return new UsageMetrics
        {
            TotalTrips = bookingStats.CompletedBookingsCount,
            TotalDistance = bookingStats.TotalDistance,
            TotalUsageHours = bookingStats.TotalUsageHours,
            AverageTripDistance = bookingStats.CompletedBookingsCount > 0
                ? bookingStats.TotalDistance / bookingStats.CompletedBookingsCount
                : 0,
            AverageTripDuration = bookingStats.CompletedBookingsCount > 0
                ? bookingStats.TotalUsageHours / bookingStats.CompletedBookingsCount
                : 0,
            MostFrequentUser = mostFrequentUser
        };
    }

    private UtilizationMetrics CalculateUtilizationMetrics(
        VehicleBookingStatistics bookingStats,
        List<Domain.Entities.MaintenanceSchedule> maintenanceSchedules,
        int totalDays)
    {
        var totalAvailableHours = totalDays * 24m;
        var totalUsageHours = bookingStats.TotalUsageHours;

        // Calculate maintenance hours
        var maintenanceHours = maintenanceSchedules
            .Where(s => s.Status == MaintenanceStatus.Completed || s.Status == MaintenanceStatus.InProgress)
            .Sum(s => s.EstimatedDuration / 60m); // Convert minutes to hours

        // Idle hours = available hours - usage hours - maintenance hours
        var idleHours = totalAvailableHours - totalUsageHours - maintenanceHours;
        if (idleHours < 0) idleHours = 0;

        // Utilization rate = usage hours / available hours
        var utilizationRate = totalAvailableHours > 0
            ? (totalUsageHours / totalAvailableHours) * 100
            : 0;

        return new UtilizationMetrics
        {
            UtilizationRate = Math.Round(utilizationRate, 2),
            TotalAvailableHours = totalAvailableHours,
            IdleHours = Math.Round(idleHours, 2),
            MaintenanceHours = Math.Round(maintenanceHours, 2),
            UnavailableHours = 0 // TODO: Calculate from vehicle blocks if implemented
        };
    }

    private EfficiencyMetrics CalculateEfficiencyMetrics(
        VehicleBookingStatistics bookingStats,
        Domain.Entities.Vehicle vehicle)
    {
        var totalRevenue = bookingStats.TotalRevenue;
        var totalDistance = bookingStats.TotalDistance;
        var totalUsageHours = bookingStats.TotalUsageHours;

        // Get maintenance costs from this period
        // TODO: Get actual costs from Payment service
        decimal totalCosts = 0;

        return new EfficiencyMetrics
        {
            DistancePerCharge = null, // TODO: Calculate from check-ins if battery level tracked
            CostPerKilometer = totalDistance > 0 ? totalCosts / totalDistance : null,
            CostPerHour = totalUsageHours > 0 ? totalCosts / totalUsageHours : null,
            TotalRevenue = totalRevenue,
            TotalCosts = totalCosts,
            NetProfit = totalRevenue - totalCosts
        };
    }

    private UsagePatterns AnalyzeUsagePatterns(List<CompletedBookingDto> bookings)
    {
        if (!bookings.Any())
        {
            return new UsagePatterns
            {
                PeakHours = new List<HourlyUsage>(),
                PeakDays = new List<DailyUsage>(),
                BusiestHour = "N/A",
                BusiestDay = "N/A"
            };
        }

        // Analyze hourly patterns
        var hourlyStats = bookings
            .GroupBy(b => b.ActualStartAt?.Hour ?? b.StartAt.Hour)
            .Select(g => new HourlyUsage
            {
                Hour = g.Key,
                TripCount = g.Count(),
                AverageDuration = g.Average(b => b.UsageHours)
            })
            .OrderByDescending(h => h.TripCount)
            .ToList();

        var busiestHour = hourlyStats.FirstOrDefault();

        // Analyze daily patterns
        var dailyStats = bookings
            .GroupBy(b => (b.ActualStartAt ?? b.StartAt).DayOfWeek)
            .Select(g => new DailyUsage
            {
                DayOfWeek = g.Key,
                DayName = g.Key.ToString(),
                TripCount = g.Count(),
                TotalDistance = g.Sum(b => b.Distance ?? 0)
            })
            .OrderByDescending(d => d.TripCount)
            .ToList();

        var busiestDay = dailyStats.FirstOrDefault();

        return new UsagePatterns
        {
            PeakHours = hourlyStats.Take(5).ToList(),
            PeakDays = dailyStats,
            BusiestHour = busiestHour != null ? $"{busiestHour.Hour:D2}:00" : "N/A",
            BusiestDay = busiestDay?.DayName ?? "N/A"
        };
    }

    private List<TrendDataPoint> GenerateTrendData(
        List<CompletedBookingDto> bookings,
        DateTime startDate,
        DateTime endDate,
        string groupBy)
    {
        var trends = new List<TrendDataPoint>();

        if (!bookings.Any())
        {
            return trends;
        }

        switch (groupBy.ToLower())
        {
            case "daily":
                return GenerateDailyTrends(bookings, startDate, endDate);
            case "weekly":
                return GenerateWeeklyTrends(bookings, startDate, endDate);
            case "monthly":
                return GenerateMonthlyTrends(bookings, startDate, endDate);
            default:
                return GenerateDailyTrends(bookings, startDate, endDate);
        }
    }

    private List<TrendDataPoint> GenerateDailyTrends(
        List<CompletedBookingDto> bookings,
        DateTime startDate,
        DateTime endDate)
    {
        var trends = new List<TrendDataPoint>();
        var current = startDate.Date;

        while (current <= endDate.Date)
        {
            var dayBookings = bookings
                .Where(b => (b.ActualStartAt ?? b.StartAt).Date == current)
                .ToList();

            var totalHours = dayBookings.Sum(b => b.UsageHours);
            var utilizationRate = totalHours > 0 ? (totalHours / 24m) * 100 : 0;

            trends.Add(new TrendDataPoint
            {
                Date = current,
                Period = current.ToString("yyyy-MM-dd"),
                TripCount = dayBookings.Count,
                Distance = dayBookings.Sum(b => b.Distance ?? 0),
                UsageHours = totalHours,
                UtilizationRate = Math.Round(utilizationRate, 2)
            });

            current = current.AddDays(1);
        }

        return trends;
    }

    private List<TrendDataPoint> GenerateWeeklyTrends(
        List<CompletedBookingDto> bookings,
        DateTime startDate,
        DateTime endDate)
    {
        var trends = new List<TrendDataPoint>();
        var current = startDate.Date;

        while (current <= endDate.Date)
        {
            var weekEnd = current.AddDays(6);
            if (weekEnd > endDate) weekEnd = endDate;

            var weekBookings = bookings
                .Where(b =>
                {
                    var bookingDate = (b.ActualStartAt ?? b.StartAt).Date;
                    return bookingDate >= current && bookingDate <= weekEnd;
                })
                .ToList();

            var totalHours = weekBookings.Sum(b => b.UsageHours);
            var weekHours = ((weekEnd - current).Days + 1) * 24m;
            var utilizationRate = weekHours > 0 ? (totalHours / weekHours) * 100 : 0;

            var weekNumber = System.Globalization.ISOWeek.GetWeekOfYear(current);

            trends.Add(new TrendDataPoint
            {
                Date = current,
                Period = $"Week {weekNumber} {current.Year}",
                TripCount = weekBookings.Count,
                Distance = weekBookings.Sum(b => b.Distance ?? 0),
                UsageHours = totalHours,
                UtilizationRate = Math.Round(utilizationRate, 2)
            });

            current = current.AddDays(7);
        }

        return trends;
    }

    private List<TrendDataPoint> GenerateMonthlyTrends(
        List<CompletedBookingDto> bookings,
        DateTime startDate,
        DateTime endDate)
    {
        var trends = new List<TrendDataPoint>();
        var current = new DateTime(startDate.Year, startDate.Month, 1);

        while (current <= endDate)
        {
            var monthEnd = current.AddMonths(1).AddDays(-1);
            if (monthEnd > endDate) monthEnd = endDate;

            var monthBookings = bookings
                .Where(b =>
                {
                    var bookingDate = (b.ActualStartAt ?? b.StartAt).Date;
                    return bookingDate >= current && bookingDate <= monthEnd;
                })
                .ToList();

            var totalHours = monthBookings.Sum(b => b.UsageHours);
            var monthHours = ((monthEnd - current).Days + 1) * 24m;
            var utilizationRate = monthHours > 0 ? (totalHours / monthHours) * 100 : 0;

            trends.Add(new TrendDataPoint
            {
                Date = current,
                Period = current.ToString("MMM yyyy"),
                TripCount = monthBookings.Count,
                Distance = monthBookings.Sum(b => b.Distance ?? 0),
                UsageHours = totalHours,
                UtilizationRate = Math.Round(utilizationRate, 2)
            });

            current = current.AddMonths(1);
        }

        return trends;
    }

    private async Task<PeriodComparison> CalculatePeriodComparisonAsync(
        Guid vehicleId,
        DateTime startDate,
        DateTime endDate,
        string accessToken)
    {
        // Calculate previous period (same duration)
        var periodDuration = endDate - startDate;
        var previousEndDate = startDate.AddDays(-1);
        var previousStartDate = previousEndDate - periodDuration;

        // Get previous period booking stats
        var previousStats = await _bookingClient.GetVehicleBookingStatisticsAsync(
            vehicleId, previousStartDate, previousEndDate, accessToken);

        if (previousStats == null)
        {
            return new PeriodComparison
            {
                PreviousTripCount = 0,
                PreviousDistance = 0,
                PreviousUsageHours = 0,
                PreviousUtilizationRate = 0,
                TripCountGrowth = 0,
                DistanceGrowth = 0,
                UsageHoursGrowth = 0,
                UtilizationRateGrowth = 0
            };
        }

        // Calculate growth percentages
        var tripCountGrowth = previousStats.CompletedBookingsCount > 0
            ? ((previousStats.CompletedBookingsCount - previousStats.CompletedBookingsCount) / (decimal)previousStats.CompletedBookingsCount) * 100
            : 0;

        var distanceGrowth = previousStats.TotalDistance > 0
            ? ((previousStats.TotalDistance - previousStats.TotalDistance) / previousStats.TotalDistance) * 100
            : 0;

        var usageHoursGrowth = previousStats.TotalUsageHours > 0
            ? ((previousStats.TotalUsageHours - previousStats.TotalUsageHours) / previousStats.TotalUsageHours) * 100
            : 0;

        var previousUtilizationRate = (periodDuration.Days * 24m) > 0
            ? (previousStats.TotalUsageHours / (periodDuration.Days * 24m)) * 100
            : 0;

        return new PeriodComparison
        {
            PreviousTripCount = previousStats.CompletedBookingsCount,
            PreviousDistance = previousStats.TotalDistance,
            PreviousUsageHours = previousStats.TotalUsageHours,
            PreviousUtilizationRate = Math.Round(previousUtilizationRate, 2),
            TripCountGrowth = Math.Round(tripCountGrowth, 2),
            DistanceGrowth = Math.Round(distanceGrowth, 2),
            UsageHoursGrowth = Math.Round(usageHoursGrowth, 2),
            UtilizationRateGrowth = 0 // TODO: Calculate properly
        };
    }

    private async Task<BenchmarkData> CalculateBenchmarksAsync(
        Guid vehicleId,
        Guid groupId,
        string model,
        int year,
        DateTime startDate,
        DateTime endDate,
        string accessToken)
    {
        // Get all vehicles in the same group
        var groupVehicles = await _context.Vehicles
            .Where(v => v.GroupId == groupId && v.Id != vehicleId)
            .ToListAsync();

        // Get similar vehicles (same model and year)
        var similarVehicles = groupVehicles
            .Where(v => v.Model == model && v.Year == year)
            .ToList();

        // For now, return placeholder data
        // TODO: Fetch actual statistics for all group vehicles and calculate averages
        return new BenchmarkData
        {
            GroupAverage = new GroupComparison
            {
                AverageTrips = 0,
                AverageDistance = 0,
                AverageUtilizationRate = 0,
                AverageCostPerKm = 0,
                TripsDifference = 0,
                DistanceDifference = 0,
                UtilizationDifference = 0,
                CostPerKmDifference = 0,
                TripsPercentDifference = 0,
                DistancePercentDifference = 0,
                UtilizationPercentDifference = 0
            },
            SimilarVehicles = new GroupComparison
            {
                AverageTrips = 0,
                AverageDistance = 0,
                AverageUtilizationRate = 0,
                AverageCostPerKm = 0,
                TripsDifference = 0,
                DistanceDifference = 0,
                UtilizationDifference = 0,
                CostPerKmDifference = 0,
                TripsPercentDifference = 0,
                DistancePercentDifference = 0,
                UtilizationPercentDifference = 0
            },
            GroupRank = 1,
            TotalVehiclesInGroup = groupVehicles.Count + 1
        };
    }

    #endregion
}
