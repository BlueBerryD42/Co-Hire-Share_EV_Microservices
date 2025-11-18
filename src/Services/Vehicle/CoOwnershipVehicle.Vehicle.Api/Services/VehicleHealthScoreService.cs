using Microsoft.EntityFrameworkCore;
using CoOwnershipVehicle.Vehicle.Api.Data;
using CoOwnershipVehicle.Vehicle.Api.DTOs;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Domain.Enums;

namespace CoOwnershipVehicle.Vehicle.Api.Services;

public class VehicleHealthScoreService
{
    private readonly VehicleDbContext _context;
    private readonly ILogger<VehicleHealthScoreService> _logger;
    private readonly IBookingServiceClient _bookingServiceClient;

    // Weight constants (must sum to 100)
    private const decimal MAINTENANCE_WEIGHT = 30m;
    private const decimal ODOMETER_AGE_WEIGHT = 20m;
    private const decimal DAMAGE_WEIGHT = 20m;
    private const decimal SERVICE_FREQUENCY_WEIGHT = 15m;
    private const decimal VEHICLE_AGE_WEIGHT = 10m;
    private const decimal INSPECTION_WEIGHT = 5m;

    public VehicleHealthScoreService(
        VehicleDbContext context,
        ILogger<VehicleHealthScoreService> logger,
        IBookingServiceClient bookingServiceClient)
    {
        _context = context;
        _logger = logger;
        _bookingServiceClient = bookingServiceClient;
    }

    /// <summary>
    /// Calculate comprehensive health score for a vehicle
    /// </summary>
    public async Task<HealthScoreResponse> CalculateHealthScoreAsync(
        Guid vehicleId,
        HealthScoreRequest request,
        string accessToken)
    {
        var vehicle = await _context.Vehicles
            .FirstOrDefaultAsync(v => v.Id == vehicleId);

        if (vehicle == null)
        {
            throw new InvalidOperationException($"Vehicle {vehicleId} not found");
        }

        var response = new HealthScoreResponse
        {
            VehicleId = vehicle.Id,
            VehicleName = $"{vehicle.Year} {vehicle.Model}",
            PlateNumber = vehicle.PlateNumber,
            CalculatedAt = DateTime.UtcNow
        };

        // Calculate each component score with error handling
        var breakdown = new ScoreBreakdown();

        try
        {
            // 1. Maintenance Adherence (30%)
            breakdown.MaintenanceAdherence = await CalculateMaintenanceAdherenceScore(vehicle);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error calculating maintenance adherence score for vehicle {VehicleId}", vehicleId);
            breakdown.MaintenanceAdherence = GetDefaultComponentScore("Maintenance Adherence", MAINTENANCE_WEIGHT, "Unable to calculate");
        }

        try
        {
            // 2. Odometer vs Age (20%)
            breakdown.OdometerVsAge = CalculateOdometerVsAgeScore(vehicle);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error calculating odometer vs age score for vehicle {VehicleId}", vehicleId);
            breakdown.OdometerVsAge = GetDefaultComponentScore("Odometer vs Age", ODOMETER_AGE_WEIGHT, "Unable to calculate");
        }

        try
        {
            // 3. Damage Reports (20%)
            breakdown.DamageReports = await CalculateDamageReportsScore(vehicle, accessToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error calculating damage reports score for vehicle {VehicleId}", vehicleId);
            breakdown.DamageReports = GetDefaultComponentScore("Damage Reports", DAMAGE_WEIGHT, "Unable to calculate");
        }

        try
        {
            // 4. Service Frequency (15%)
            breakdown.ServiceFrequency = await CalculateServiceFrequencyScore(vehicle);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error calculating service frequency score for vehicle {VehicleId}", vehicleId);
            breakdown.ServiceFrequency = GetDefaultComponentScore("Service Frequency", SERVICE_FREQUENCY_WEIGHT, "Unable to calculate");
        }

        try
        {
            // 5. Vehicle Age (10%)
            breakdown.VehicleAge = CalculateVehicleAgeScore(vehicle);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error calculating vehicle age score for vehicle {VehicleId}", vehicleId);
            breakdown.VehicleAge = GetDefaultComponentScore("Vehicle Age", VEHICLE_AGE_WEIGHT, "Unable to calculate");
        }

        try
        {
            // 6. Inspection Results (5%)
            breakdown.InspectionResults = await CalculateInspectionScore(vehicle);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error calculating inspection score for vehicle {VehicleId}", vehicleId);
            breakdown.InspectionResults = GetDefaultComponentScore("Inspection Results", INSPECTION_WEIGHT, "Unable to calculate");
        }

        response.Breakdown = breakdown;

        // Calculate overall score
        response.OverallScore =
            breakdown.MaintenanceAdherence.Points +
            breakdown.OdometerVsAge.Points +
            breakdown.DamageReports.Points +
            breakdown.ServiceFrequency.Points +
            breakdown.VehicleAge.Points +
            breakdown.InspectionResults.Points;

        // Determine category and color
        response.Category = DetermineHealthCategory(response.OverallScore);
        response.ColorIndicator = GetColorIndicator(response.Category);

        // Generate factors
        response.PositiveFactors = GeneratePositiveFactors(breakdown);
        response.NegativeFactors = GenerateNegativeFactors(breakdown);

        // Generate recommendations
        response.Recommendations = GenerateRecommendations(vehicle, breakdown);

        // Generate alerts
        response.Alerts = await GenerateHealthAlerts(vehicle, response.OverallScore, breakdown);

        // Historical trend (if requested)
        if (request.IncludeHistory)
        {
            response.HistoricalTrend = await GetHistoricalTrend(vehicleId, request.HistoryMonths);
        }

        // Benchmark comparison (if requested)
        if (request.IncludeBenchmark)
        {
            response.Benchmark = await GetBenchmarkComparison(vehicle, response.OverallScore);
        }

        // Future prediction
        response.Prediction = await PredictFutureHealth(vehicle, response.HistoricalTrend);

        // Save this score to history
        await SaveHealthScoreHistory(vehicle, response, breakdown);

        return response;
    }

    /// <summary>
    /// Calculate maintenance adherence score (0-30 points)
    /// </summary>
    private async Task<ComponentScore> CalculateMaintenanceAdherenceScore(Domain.Entities.Vehicle vehicle)
    {
        var now = DateTime.UtcNow;

        // Get scheduled maintenance
        var scheduledMaintenance = await _context.MaintenanceSchedules
            .Where(m => m.VehicleId == vehicle.Id)
            .ToListAsync();

        var totalScheduled = scheduledMaintenance.Count;
        var overdue = scheduledMaintenance.Count(m =>
            m.Status != MaintenanceStatus.Completed &&
            m.ScheduledDate < now);

        // For completed maintenance, check completion via MaintenanceRecords
        var completedCount = scheduledMaintenance.Count(m => m.Status == MaintenanceStatus.Completed);
        var onTime = completedCount; // Approximate - would need to join with MaintenanceRecords for exact timing

        decimal score;
        string description;
        string status;

        if (totalScheduled == 0)
        {
            // No scheduled maintenance - neutral score
            score = MAINTENANCE_WEIGHT * 0.7m; // 70% = 21 points
            description = "No maintenance schedule established. Consider setting up regular maintenance.";
            status = "Fair";
        }
        else
        {
            // Calculate on-time percentage
            var onTimePercentage = completedCount > 0 ? (decimal)onTime / completedCount : 0;
            var overduePercentage = (decimal)overdue / totalScheduled;

            // Score formula: reduce points for each overdue item
            decimal adherenceRate = 1.0m - (overduePercentage * 0.5m) + (onTimePercentage * 0.5m);
            adherenceRate = Math.Max(0, Math.Min(1, adherenceRate));

            score = MAINTENANCE_WEIGHT * adherenceRate;

            description = $"{onTime} on-time completions, {overdue} overdue items out of {totalScheduled} scheduled.";

            status = score >= MAINTENANCE_WEIGHT * 0.8m ? "Excellent" :
                     score >= MAINTENANCE_WEIGHT * 0.6m ? "Good" :
                     score >= MAINTENANCE_WEIGHT * 0.4m ? "Fair" : "Poor";
        }

        return new ComponentScore
        {
            ComponentName = "Maintenance Adherence",
            Points = Math.Round(score, 2),
            MaxPoints = MAINTENANCE_WEIGHT,
            Weight = MAINTENANCE_WEIGHT,
            Description = description,
            Status = status
        };
    }

    /// <summary>
    /// Calculate odometer vs age score (0-20 points)
    /// </summary>
    private ComponentScore CalculateOdometerVsAgeScore(Domain.Entities.Vehicle vehicle)
    {
        var currentYear = DateTime.UtcNow.Year;
        var vehicleAge = currentYear - vehicle.Year;

        if (vehicleAge <= 0)
        {
            vehicleAge = 1; // Brand new vehicle
        }

        // Expected average: 15,000 km per year for EVs
        var expectedOdometer = vehicleAge * 15000;
        var actualOdometer = vehicle.Odometer;

        decimal ratio = expectedOdometer > 0 ? (decimal)actualOdometer / expectedOdometer : 1;

        // Optimal range: 0.7 - 1.0 (70%-100% of expected usage)
        // Below 0.7 = underused (slightly negative)
        // Above 1.5 = overused (negative)
        decimal score;
        string description;
        string status;

        if (ratio <= 1.0m)
        {
            // At or below expected - good
            score = ODOMETER_AGE_WEIGHT; // Full points
            description = $"Odometer ({actualOdometer:N0} km) is within optimal range for {vehicleAge}-year-old vehicle.";
            status = "Excellent";
        }
        else if (ratio <= 1.3m)
        {
            // Slightly above expected - acceptable
            score = ODOMETER_AGE_WEIGHT * 0.85m;
            description = $"Odometer ({actualOdometer:N0} km) is slightly high for {vehicleAge}-year-old vehicle.";
            status = "Good";
        }
        else if (ratio <= 1.7m)
        {
            // Significantly above expected - concerning
            score = ODOMETER_AGE_WEIGHT * 0.6m;
            description = $"Odometer ({actualOdometer:N0} km) is significantly high for {vehicleAge}-year-old vehicle.";
            status = "Fair";
        }
        else
        {
            // Very high usage - poor
            score = ODOMETER_AGE_WEIGHT * 0.3m;
            description = $"Odometer ({actualOdometer:N0} km) is very high for {vehicleAge}-year-old vehicle.";
            status = "Poor";
        }

        return new ComponentScore
        {
            ComponentName = "Odometer vs Age",
            Points = Math.Round(score, 2),
            MaxPoints = ODOMETER_AGE_WEIGHT,
            Weight = ODOMETER_AGE_WEIGHT,
            Description = description,
            Status = status
        };
    }

    /// <summary>
    /// Calculate damage reports score (0-20 points)
    /// Note: In a real system, this would query a Damage/Incident service
    /// For now, we'll check CheckIn photos with "Damage" type
    /// </summary>
    private Task<ComponentScore> CalculateDamageReportsScore(Domain.Entities.Vehicle vehicle, string accessToken)
    {
        // TODO: Integrate with a proper Damage/Incident service or Analytics Service (CheckIn photos with PhotoType.Damage)
        // For now, we'll use a placeholder and return a neutral score if no data.

        int damageReportCount = 0; // Placeholder - would come from service call
        int recentMonths = 12; // <-- define this

        
        decimal score;
        string description;
        string status;

        if (damageReportCount == 0)
        {
            // If no damage reports, return a neutral score, not full points, as this is a placeholder.
            score = DAMAGE_WEIGHT * 0.5m; // Neutral score (e.g., 10 points out of 20)
            description = $"No damage reports found. (Placeholder: Needs integration with damage reporting system)";
            status = "Fair";
        }
        else if (damageReportCount <= 2)
        {
            score = DAMAGE_WEIGHT * 0.7m;
            description = $"{damageReportCount} minor damage report(s) in the last {recentMonths} months.";
            status = "Good";
        }
        else if (damageReportCount <= 4)
        {
            score = DAMAGE_WEIGHT * 0.4m;
            description = $"{damageReportCount} damage reports in the last {recentMonths} months.";
            status = "Fair";
        }
        else
        {
            score = DAMAGE_WEIGHT * 0.2m;
            description = $"{damageReportCount} damage reports in the last {recentMonths} months - concerning trend.";
            status = "Poor";
        }

        return Task.FromResult(new ComponentScore
        {
            ComponentName = "Damage Reports",
            Points = Math.Round(score, 2),
            MaxPoints = DAMAGE_WEIGHT,
            Weight = DAMAGE_WEIGHT,
            Description = description,
            Status = status
        });
    }

    /// <summary>
    /// Calculate service frequency score (0-15 points)
    /// </summary>
    private async Task<ComponentScore> CalculateServiceFrequencyScore(Domain.Entities.Vehicle vehicle)
    {
        var recentMonths = 12;
        var cutoffDate = DateTime.UtcNow.AddMonths(-recentMonths);

        var serviceRecords = await _context.MaintenanceRecords
            .Where(m => m.VehicleId == vehicle.Id && m.ScheduledDate >= cutoffDate)
            .CountAsync();

        // Expected: at least 2 services per year (oil change, inspection)
        // Good: 3-4 services per year
        // Excellent: 4+ services per year

        decimal score;
        string description;
        string status;

        if (serviceRecords >= 4)
        {
            score = SERVICE_FREQUENCY_WEIGHT; // Full points
            description = $"{serviceRecords} service records in the last {recentMonths} months - excellent maintenance.";
            status = "Excellent";
        }
        else if (serviceRecords >= 2)
        {
            score = SERVICE_FREQUENCY_WEIGHT * 0.8m;
            description = $"{serviceRecords} service records in the last {recentMonths} months - adequate maintenance.";
            status = "Good";
        }
        else if (serviceRecords == 1)
        {
            score = SERVICE_FREQUENCY_WEIGHT * 0.5m;
            description = $"Only {serviceRecords} service record in the last {recentMonths} months - consider more frequent maintenance.";
            status = "Fair";
        }
        else
        {
            score = SERVICE_FREQUENCY_WEIGHT * 0.2m;
            description = $"No service records in the last {recentMonths} months - maintenance overdue.";
            status = "Poor";
        }

        return new ComponentScore
        {
            ComponentName = "Service Frequency",
            Points = Math.Round(score, 2),
            MaxPoints = SERVICE_FREQUENCY_WEIGHT,
            Weight = SERVICE_FREQUENCY_WEIGHT,
            Description = description,
            Status = status
        };
    }

    /// <summary>
    /// Calculate vehicle age score (0-10 points)
    /// </summary>
    private ComponentScore CalculateVehicleAgeScore(Domain.Entities.Vehicle vehicle)
    {
        var currentYear = DateTime.UtcNow.Year;
        var vehicleAge = currentYear - vehicle.Year;

        decimal score;
        string description;
        string status;

        if (vehicleAge <= 1)
        {
            score = VEHICLE_AGE_WEIGHT; // Brand new
            description = $"Vehicle is {vehicleAge} year(s) old - like new.";
            status = "Excellent";
        }
        else if (vehicleAge <= 3)
        {
            score = VEHICLE_AGE_WEIGHT * 0.9m;
            description = $"Vehicle is {vehicleAge} years old - minimal age-related wear.";
            status = "Excellent";
        }
        else if (vehicleAge <= 5)
        {
            score = VEHICLE_AGE_WEIGHT * 0.75m;
            description = $"Vehicle is {vehicleAge} years old - moderate age.";
            status = "Good";
        }
        else if (vehicleAge <= 8)
        {
            score = VEHICLE_AGE_WEIGHT * 0.55m;
            description = $"Vehicle is {vehicleAge} years old - some age-related concerns.";
            status = "Fair";
        }
        else
        {
            score = VEHICLE_AGE_WEIGHT * 0.35m;
            description = $"Vehicle is {vehicleAge} years old - significant age-related wear expected.";
            status = "Fair";
        }

        return new ComponentScore
        {
            ComponentName = "Vehicle Age",
            Points = Math.Round(score, 2),
            MaxPoints = VEHICLE_AGE_WEIGHT,
            Weight = VEHICLE_AGE_WEIGHT,
            Description = description,
            Status = status
        };
    }

    /// <summary>
    /// Calculate inspection results score (0-5 points)
    /// Based on most recent maintenance records with ratings
    /// </summary>
    private async Task<ComponentScore> CalculateInspectionScore(Domain.Entities.Vehicle vehicle)
    {
        var recentInspections = await _context.MaintenanceRecords
            .Where(m => m.VehicleId == vehicle.Id &&
                        m.ServiceProviderRating.HasValue)
            .OrderByDescending(m => m.ScheduledDate)
            .Take(3)
            .ToListAsync();

        decimal score;
        string description;
        string status;

        if (!recentInspections.Any())
        {
            score = INSPECTION_WEIGHT * 0.6m; // Neutral
            description = "No recent inspection ratings available.";
            status = "Fair";
        }
        else
        {
            var averageRating = (decimal)recentInspections.Average(m => m.ServiceProviderRating!.Value);

            // Convert 1-5 rating to score (5 stars = 100%, 1 star = 20%)
            var ratingPercentage = averageRating / 5m;

            score = INSPECTION_WEIGHT * ratingPercentage;
            description = $"Average inspection rating: {averageRating:F1}/5.0 based on {recentInspections.Count} recent service(s).";

            status = averageRating >= 4.5m ? "Excellent" :
                     averageRating >= 3.5m ? "Good" :
                     averageRating >= 2.5m ? "Fair" : "Poor";
        }

        return new ComponentScore
        {
            ComponentName = "Inspection Results",
            Points = Math.Round(score, 2),
            MaxPoints = INSPECTION_WEIGHT,
            Weight = INSPECTION_WEIGHT,
            Description = description,
            Status = status
        };
    }

    /// <summary>
    /// Determine health category from overall score
    /// </summary>
    private HealthCategory DetermineHealthCategory(decimal score)
    {
        if (score >= 80) return HealthCategory.Excellent;
        if (score >= 60) return HealthCategory.Good;
        if (score >= 40) return HealthCategory.Fair;
        if (score >= 20) return HealthCategory.Poor;
        return HealthCategory.Critical;
    }

    /// <summary>
    /// Get color indicator for health category
    /// </summary>
    private string GetColorIndicator(HealthCategory category)
    {
        return category switch
        {
            HealthCategory.Excellent => "Green",
            HealthCategory.Good => "Yellow",
            HealthCategory.Fair => "Orange",
            HealthCategory.Poor => "Red",
            HealthCategory.Critical => "Red",
            _ => "Gray"
        };
    }

    /// <summary>
    /// Generate positive factors list
    /// </summary>
    private List<HealthFactor> GeneratePositiveFactors(ScoreBreakdown breakdown)
    {
        var factors = new List<HealthFactor>();

        // Check each component for positive contribution
        if (breakdown.MaintenanceAdherence.Points >= breakdown.MaintenanceAdherence.MaxPoints * 0.8m)
        {
            factors.Add(new HealthFactor
            {
                Description = "Excellent maintenance adherence",
                Impact = breakdown.MaintenanceAdherence.Points,
                Category = "Maintenance"
            });
        }

        if (breakdown.ServiceFrequency.Points >= breakdown.ServiceFrequency.MaxPoints * 0.8m)
        {
            factors.Add(new HealthFactor
            {
                Description = "Regular service schedule maintained",
                Impact = breakdown.ServiceFrequency.Points,
                Category = "Maintenance"
            });
        }

        if (breakdown.DamageReports.Points >= breakdown.DamageReports.MaxPoints * 0.9m)
        {
            factors.Add(new HealthFactor
            {
                Description = "No recent damage reports",
                Impact = breakdown.DamageReports.Points,
                Category = "Condition"
            });
        }

        if (breakdown.VehicleAge.Points >= breakdown.VehicleAge.MaxPoints * 0.8m)
        {
            factors.Add(new HealthFactor
            {
                Description = "Relatively new vehicle",
                Impact = breakdown.VehicleAge.Points,
                Category = "Age"
            });
        }

        return factors;
    }

    /// <summary>
    /// Generate negative factors list
    /// </summary>
    private List<HealthFactor> GenerateNegativeFactors(ScoreBreakdown breakdown)
    {
        var factors = new List<HealthFactor>();

        // Check each component for negative contribution
        if (breakdown.MaintenanceAdherence.Points < breakdown.MaintenanceAdherence.MaxPoints * 0.6m)
        {
            var deficit = breakdown.MaintenanceAdherence.MaxPoints - breakdown.MaintenanceAdherence.Points;
            factors.Add(new HealthFactor
            {
                Description = "Poor maintenance adherence with overdue items",
                Impact = -deficit,
                Category = "Maintenance"
            });
        }

        if (breakdown.ServiceFrequency.Points < breakdown.ServiceFrequency.MaxPoints * 0.5m)
        {
            var deficit = breakdown.ServiceFrequency.MaxPoints - breakdown.ServiceFrequency.Points;
            factors.Add(new HealthFactor
            {
                Description = "Insufficient service frequency",
                Impact = -deficit,
                Category = "Maintenance"
            });
        }

        if (breakdown.DamageReports.Points < breakdown.DamageReports.MaxPoints * 0.7m)
        {
            var deficit = breakdown.DamageReports.MaxPoints - breakdown.DamageReports.Points;
            factors.Add(new HealthFactor
            {
                Description = "Multiple damage reports recorded",
                Impact = -deficit,
                Category = "Condition"
            });
        }

        if (breakdown.OdometerVsAge.Points < breakdown.OdometerVsAge.MaxPoints * 0.6m)
        {
            var deficit = breakdown.OdometerVsAge.MaxPoints - breakdown.OdometerVsAge.Points;
            factors.Add(new HealthFactor
            {
                Description = "High mileage for vehicle age",
                Impact = -deficit,
                Category = "Usage"
            });
        }

        return factors;
    }

    /// <summary>
    /// Generate actionable recommendations
    /// </summary>
    private List<Recommendation> GenerateRecommendations(Domain.Entities.Vehicle vehicle, ScoreBreakdown breakdown)
    {
        var recommendations = new List<Recommendation>();

        // Check maintenance adherence
        if (breakdown.MaintenanceAdherence.Points < breakdown.MaintenanceAdherence.MaxPoints * 0.7m)
        {
            recommendations.Add(new Recommendation
            {
                Title = "Complete Overdue Maintenance",
                Description = "Schedule and complete all overdue maintenance items to improve health score.",
                Priority = Priority.High,
                PotentialScoreIncrease = breakdown.MaintenanceAdherence.MaxPoints - breakdown.MaintenanceAdherence.Points,
                ActionType = "maintenance"
            });
        }

        // Check service frequency
        if (breakdown.ServiceFrequency.Points < breakdown.ServiceFrequency.MaxPoints * 0.6m)
        {
            recommendations.Add(new Recommendation
            {
                Title = "Establish Regular Maintenance Schedule",
                Description = "Set up a regular maintenance schedule with at least 2 services per year.",
                Priority = Priority.Medium,
                PotentialScoreIncrease = breakdown.ServiceFrequency.MaxPoints - breakdown.ServiceFrequency.Points,
                ActionType = "maintenance"
            });
        }

        // Check damage reports
        if (breakdown.DamageReports.Points < breakdown.DamageReports.MaxPoints * 0.7m)
        {
            recommendations.Add(new Recommendation
            {
                Title = "Address Existing Damage",
                Description = "Review and repair any existing damage to improve vehicle condition.",
                Priority = Priority.High,
                PotentialScoreIncrease = breakdown.DamageReports.MaxPoints - breakdown.DamageReports.Points,
                ActionType = "repair"
            });
        }

        // Check if no recent inspections
        if (breakdown.InspectionResults.Points < breakdown.InspectionResults.MaxPoints * 0.5m)
        {
            recommendations.Add(new Recommendation
            {
                Title = "Schedule Vehicle Inspection",
                Description = "Get a professional inspection to identify potential issues early.",
                Priority = Priority.Medium,
                PotentialScoreIncrease = breakdown.InspectionResults.MaxPoints - breakdown.InspectionResults.Points,
                ActionType = "inspection"
            });
        }

        return recommendations.OrderByDescending(r => r.Priority).ToList();
    }

    /// <summary>
    /// Generate health alerts
    /// </summary>
    private async Task<List<HealthAlert>> GenerateHealthAlerts(
        Domain.Entities.Vehicle vehicle,
        decimal overallScore,
        ScoreBreakdown breakdown)
    {
        var alerts = new List<HealthAlert>();

        // Alert if score drops below threshold
        if (overallScore < 40)
        {
            alerts.Add(new HealthAlert
            {
                Id = Guid.NewGuid(),
                Title = "Critical Health Score",
                Message = $"Vehicle health score is {overallScore:F1}/100 - immediate attention required.",
                Severity = AlertSeverity.Critical,
                CreatedAt = DateTime.UtcNow,
                AlertType = "score_drop"
            });
        }
        else if (overallScore < 60)
        {
            alerts.Add(new HealthAlert
            {
                Id = Guid.NewGuid(),
                Title = "Low Health Score",
                Message = $"Vehicle health score is {overallScore:F1}/100 - maintenance recommended.",
                Severity = AlertSeverity.Warning,
                CreatedAt = DateTime.UtcNow,
                AlertType = "score_drop"
            });
        }

        // Alert for overdue maintenance
        var overdueMaintenance = await _context.MaintenanceSchedules
            .Where(m => m.VehicleId == vehicle.Id &&
                        m.Status != MaintenanceStatus.Completed &&
                        m.ScheduledDate < DateTime.UtcNow)
            .CountAsync();

        if (overdueMaintenance > 0)
        {
            alerts.Add(new HealthAlert
            {
                Id = Guid.NewGuid(),
                Title = "Overdue Maintenance",
                Message = $"{overdueMaintenance} maintenance item(s) are overdue.",
                Severity = overdueMaintenance >= 3 ? AlertSeverity.Critical : AlertSeverity.Warning,
                CreatedAt = DateTime.UtcNow,
                AlertType = "overdue_maintenance"
            });
        }

        // Alert for damage (placeholder - would come from service call)
        if (breakdown.DamageReports.Points < breakdown.DamageReports.MaxPoints * 0.5m)
        {
            alerts.Add(new HealthAlert
            {
                Id = Guid.NewGuid(),
                Title = "Damage Reports Detected",
                Message = "Multiple damage reports recorded - inspection recommended.",
                Severity = AlertSeverity.Warning,
                CreatedAt = DateTime.UtcNow,
                AlertType = "damage"
            });
        }

        return alerts;
    }

    /// <summary>
    /// Get historical health score trend
    /// </summary>
    private async Task<List<HistoricalScore>> GetHistoricalTrend(Guid vehicleId, int months)
    {
        var cutoffDate = DateTime.UtcNow.AddMonths(-months);

        var historicalScores = await _context.VehicleHealthScores
            .Where(h => h.VehicleId == vehicleId && h.CalculatedAt >= cutoffDate)
            .OrderBy(h => h.CalculatedAt)
            .Select(h => new HistoricalScore
            {
                Date = h.CalculatedAt,
                Score = h.OverallScore,
                Category = (HealthCategory)h.Category,
                Note = h.Note ?? string.Empty
            })
            .ToListAsync();

        return historicalScores;
    }

    /// <summary>
    /// Get benchmark comparison to similar vehicles
    /// </summary>
    private async Task<BenchmarkComparison> GetBenchmarkComparison(Domain.Entities.Vehicle vehicle, decimal currentScore)
    {
        var currentYear = DateTime.UtcNow.Year;
        var vehicleAge = currentYear - vehicle.Year;

        // Find similar vehicles (same model, similar age)
        var minYear = vehicle.Year - 2;
        var maxYear = vehicle.Year + 2;

        var similarVehicles = await _context.Vehicles
            .Where(v => v.Model == vehicle.Model &&
                        v.Year >= minYear &&
                        v.Year <= maxYear &&
                        v.Id != vehicle.Id)
            .ToListAsync();

        if (!similarVehicles.Any())
        {
            return new BenchmarkComparison
            {
                AverageScore = currentScore,
                Percentile = 50,
                ComparisonGroupSize = 0,
                Summary = "No similar vehicles found for comparison.",
                TopPerformerScore = currentScore,
                Criteria = new BenchmarkCriteria
                {
                    Model = vehicle.Model,
                    MinYear = minYear,
                    MaxYear = maxYear
                }
            };
        }

        // Get latest scores for similar vehicles
        var similarVehicleIds = similarVehicles.Select(v => v.Id).ToList();

        var latestScores = await _context.VehicleHealthScores
            .Where(h => similarVehicleIds.Contains(h.VehicleId))
            .GroupBy(h => h.VehicleId)
            .Select(g => g.OrderByDescending(h => h.CalculatedAt).FirstOrDefault())
            .Where(h => h != null)
            .Select(h => h!.OverallScore)
            .ToListAsync();

        if (!latestScores.Any())
        {
            latestScores = new List<decimal> { currentScore };
        }

        var averageScore = latestScores.Average();
        var topScore = latestScores.Max();

        // Calculate percentile
        var betterOrEqual = latestScores.Count(s => s <= currentScore);
        var percentile = (decimal)betterOrEqual / latestScores.Count * 100;

        var summary = percentile >= 75 ? $"This vehicle ranks in the top 25% of similar vehicles." :
                      percentile >= 50 ? $"This vehicle performs above average compared to similar vehicles." :
                      percentile >= 25 ? $"This vehicle performs below average compared to similar vehicles." :
                                         $"This vehicle ranks in the bottom 25% of similar vehicles.";

        return new BenchmarkComparison
        {
            AverageScore = Math.Round(averageScore, 2),
            Percentile = Math.Round(percentile, 0),
            ComparisonGroupSize = latestScores.Count,
            Summary = summary,
            TopPerformerScore = Math.Round(topScore, 2),
            Criteria = new BenchmarkCriteria
            {
                Model = vehicle.Model,
                MinYear = minYear,
                MaxYear = maxYear
            }
        };
    }

    /// <summary>
    /// Predict future health trend
    /// </summary>
    private async Task<FuturePrediction> PredictFutureHealth(
        Domain.Entities.Vehicle vehicle,
        List<HistoricalScore>? historicalTrend)
    {
        var prediction = new FuturePrediction();

        if (historicalTrend == null || historicalTrend.Count < 2)
        {
            // Not enough data for prediction - use current state
            var currentScore = await GetLatestHealthScore(vehicle.Id);

            prediction.OneMonthPrediction = currentScore;
            prediction.ThreeMonthPrediction = currentScore * 0.98m; // Slight decline assumed
            prediction.SixMonthPrediction = currentScore * 0.95m;
            prediction.Trend = "stable";
            prediction.Confidence = 50; // Low confidence
            prediction.KeyFactors = new List<string> { "Insufficient historical data for accurate prediction" };

            return prediction;
        }

        // Calculate trend from historical data
        var scores = historicalTrend.Select(h => (double)h.Score).ToList();
        var trend = CalculateLinearTrend(scores);

        var latestScore = (decimal)scores.Last();

        // Project future scores
        prediction.OneMonthPrediction = Math.Max(0, Math.Min(100, latestScore + ((decimal)trend * 1)));
        prediction.ThreeMonthPrediction = Math.Max(0, Math.Min(100, latestScore + ((decimal)trend * 3)));
        prediction.SixMonthPrediction = Math.Max(0, Math.Min(100, latestScore + ((decimal)trend * 6)));

        prediction.Trend = trend > 0.5 ? "improving" :
                           trend < -0.5 ? "declining" : "stable";

        prediction.Confidence = Math.Min(95, 60 + (historicalTrend.Count * 5)); // More data = higher confidence

        prediction.KeyFactors = await GetPredictionKeyFactors(vehicle, trend);

        return prediction;
    }

    /// <summary>
    /// Calculate linear trend from score history
    /// </summary>
    private double CalculateLinearTrend(List<double> scores)
    {
        if (scores.Count < 2) return 0;

        var n = scores.Count;
        var sumX = 0.0;
        var sumY = 0.0;
        var sumXY = 0.0;
        var sumX2 = 0.0;

        for (int i = 0; i < n; i++)
        {
            sumX += i;
            sumY += scores[i];
            sumXY += i * scores[i];
            sumX2 += i * i;
        }

        var slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
        return slope;
    }

    /// <summary>
    /// Get key factors affecting prediction
    /// </summary>
    private async Task<List<string>> GetPredictionKeyFactors(Domain.Entities.Vehicle vehicle, double trend)
    {
        var factors = new List<string>();

        // Check upcoming maintenance
        var upcomingMaintenance = await _context.MaintenanceSchedules
            .Where(m => m.VehicleId == vehicle.Id &&
                        m.Status != MaintenanceStatus.Completed &&
                        m.ScheduledDate > DateTime.UtcNow &&
                        m.ScheduledDate < DateTime.UtcNow.AddMonths(3))
            .CountAsync();

        if (upcomingMaintenance > 0)
        {
            factors.Add($"{upcomingMaintenance} scheduled maintenance item(s) in next 3 months");
        }

        // Vehicle age factor
        var currentYear = DateTime.UtcNow.Year;
        var vehicleAge = currentYear - vehicle.Year;

        if (vehicleAge >= 5)
        {
            factors.Add("Vehicle age may lead to increased maintenance needs");
        }

        // Trend factor
        if (trend > 0)
        {
            factors.Add("Positive historical trend from regular maintenance");
        }
        else if (trend < 0)
        {
            factors.Add("Declining trend - increased attention recommended");
        }

        if (factors.Count == 0)
        {
            factors.Add("Normal aging and usage patterns");
        }

        return factors;
    }

    /// <summary>
    /// Get latest health score for a vehicle
    /// </summary>
    private async Task<decimal> GetLatestHealthScore(Guid vehicleId)
    {
        var latestScore = await _context.VehicleHealthScores
            .Where(h => h.VehicleId == vehicleId)
            .OrderByDescending(h => h.CalculatedAt)
            .Select(h => h.OverallScore)
            .FirstOrDefaultAsync();

        return latestScore > 0 ? latestScore : 75; // Default to 75 if no history
    }

    /// <summary>
    /// Save health score to history
    /// </summary>
    private async Task SaveHealthScoreHistory(
        Domain.Entities.Vehicle vehicle,
        HealthScoreResponse response,
        ScoreBreakdown breakdown)
    {
        var overdueMaintenance = await _context.MaintenanceSchedules
            .Where(m => m.VehicleId == vehicle.Id &&
                        m.Status != MaintenanceStatus.Completed &&
                        m.ScheduledDate < DateTime.UtcNow)
            .CountAsync();

        var healthScore = new VehicleHealthScore
        {
            Id = Guid.NewGuid(),
            VehicleId = vehicle.Id,
            OverallScore = response.OverallScore,
            Category = (Domain.Entities.HealthCategory)response.Category,
            CalculatedAt = response.CalculatedAt,
            MaintenanceScore = breakdown.MaintenanceAdherence.Points,
            OdometerAgeScore = breakdown.OdometerVsAge.Points,
            DamageScore = breakdown.DamageReports.Points,
            ServiceFrequencyScore = breakdown.ServiceFrequency.Points,
            VehicleAgeScore = breakdown.VehicleAge.Points,
            InspectionScore = breakdown.InspectionResults.Points,
            OdometerAtCalculation = vehicle.Odometer,
            OverdueMaintenanceCount = overdueMaintenance,
            DamageReportCount = 0, // Placeholder
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.VehicleHealthScores.Add(healthScore);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Saved health score {Score} for vehicle {VehicleId} (Category: {Category})",
            response.OverallScore, vehicle.Id, response.Category);
    }

    /// <summary>
    /// Get simplified health summary for vehicle list view
    /// </summary>
    public async Task<VehicleHealthSummary?> GetHealthSummaryAsync(Guid vehicleId)
    {
        var latestScore = await _context.VehicleHealthScores
            .Where(h => h.VehicleId == vehicleId)
            .OrderByDescending(h => h.CalculatedAt)
            .FirstOrDefaultAsync();

        if (latestScore == null)
        {
            return null;
        }

        var alertCount = await _context.MaintenanceSchedules
            .Where(m => m.VehicleId == vehicleId &&
                        m.Status != MaintenanceStatus.Completed &&
                        m.ScheduledDate < DateTime.UtcNow)
            .CountAsync();

        return new VehicleHealthSummary
        {
            VehicleId = vehicleId,
            OverallScore = latestScore.OverallScore,
            Category = (HealthCategory)latestScore.Category,
            ColorIndicator = GetColorIndicator((HealthCategory)latestScore.Category),
            AlertCount = alertCount,
            LastCalculated = latestScore.CalculatedAt
        };
    }

    /// <summary>
    /// Get default component score when calculation fails
    /// </summary>
    private ComponentScore GetDefaultComponentScore(string componentName, decimal maxPoints, string description)
    {
        return new ComponentScore
        {
            ComponentName = componentName,
            Points = 0,
            MaxPoints = maxPoints,
            Weight = maxPoints,
            Description = description,
            Status = "Unknown"
        };
    }
}
