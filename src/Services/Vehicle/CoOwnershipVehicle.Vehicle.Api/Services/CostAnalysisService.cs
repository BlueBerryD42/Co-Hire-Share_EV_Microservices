using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CoOwnershipVehicle.Vehicle.Api.DTOs;
using CoOwnershipVehicle.Vehicle.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CoOwnershipVehicle.Vehicle.Api.Services
{
    public class CostAnalysisService
    {
        private readonly VehicleDbContext _context;
        private readonly IPaymentServiceClient _paymentClient;
        private readonly IBookingServiceClient _bookingClient;
        private readonly ILogger<CostAnalysisService> _logger;

        public CostAnalysisService(
            VehicleDbContext context,
            IPaymentServiceClient paymentClient,
            IBookingServiceClient bookingClient,
            ILogger<CostAnalysisService> logger)
        {
            _context = context;
            _paymentClient = paymentClient;
            _bookingClient = bookingClient;
            _logger = logger;
        }

        /// <summary>
        /// Get comprehensive cost analysis for a vehicle
        /// </summary>
        public async Task<CostAnalysisResponse> GetCostAnalysisAsync(
            Guid vehicleId,
            CostAnalysisRequest request,
            string accessToken)
        {
            // 1. Validate vehicle exists
            var vehicle = await _context.Vehicles
                .FirstOrDefaultAsync(v => v.Id == vehicleId);

            if (vehicle == null)
            {
                throw new InvalidOperationException($"Vehicle {vehicleId} not found");
            }

            // 2. Set date range defaults
            var endDate = request.EndDate ?? DateTime.UtcNow;
            var startDate = request.StartDate ?? endDate.AddYears(-1); // Default: last 1 year
            var totalDays = (endDate - startDate).Days + 1;

            // 3. Get expenses from Payment Service
            // The Payment Service's GetVehicleExpensesAsync actually expects a groupId, not a vehicleId.
            // We need to pass the groupId associated with this vehicle.
            VehicleExpensesResponse expensesData;
            if (!vehicle.GroupId.HasValue)
            {
                _logger.LogWarning("Vehicle {VehicleId} does not have an associated GroupId. Cannot fetch expenses.", vehicleId);
                expensesData = new VehicleExpensesResponse
                {
                    VehicleId = vehicleId,
                    StartDate = startDate,
                    EndDate = endDate,
                    Expenses = new(),
                    TotalAmount = 0
                };
            }
            else
            {
                expensesData = await _paymentClient.GetVehicleExpensesAsync(
                    vehicle.GroupId.Value, startDate, endDate, accessToken);
            }

            // Ensure expensesData is not null before proceeding
            if (expensesData == null)
            {
                expensesData = new VehicleExpensesResponse
                {
                    VehicleId = vehicleId,
                    StartDate = startDate,
                    EndDate = endDate,
                    Expenses = new(),
                    TotalAmount = 0
                };
            }

            // 4. Get all-time expenses for total cost
            // Use vehicle year to estimate all-time start date if no purchase date available
            var allTimeStartDate = vehicle.Year > 0 ? new DateTime(vehicle.Year, 1, 1) : DateTime.MinValue;
            var allTimeExpenses = await _paymentClient.GetVehicleExpensesAsync(
                vehicleId,
                allTimeStartDate,
                DateTime.UtcNow,
                accessToken);

            // 5. Get booking statistics for per-unit calculations
            var bookingStats = await _bookingClient.GetVehicleBookingStatisticsAsync(
                vehicleId, startDate, endDate, accessToken);

            // 6. Get budget information
            var budgetData = await _paymentClient.GetVehicleBudgetAsync(vehicleId, accessToken);

            // 7. Calculate expense breakdown
            var expenseBreakdown = CalculateExpenseBreakdown(expensesData.Expenses);

            // 8. Calculate total costs
            var totalCosts = CalculateTotalCosts(
                allTimeExpenses?.TotalAmount ?? 0,
                expensesData.TotalAmount,
                totalDays);

            // 9. Calculate cost per unit
            var costPerUnit = CalculateCostPerUnit(
                expensesData.TotalAmount,
                bookingStats?.TotalDistance ?? 0,
                bookingStats?.TotalUsageHours ?? 0,
                bookingStats?.TotalBookings ?? 0);

            // 10. Generate cost trends
            var costTrends = GenerateCostTrends(
                expensesData.Expenses,
                startDate,
                endDate,
                request.GroupBy);

            // 11. Calculate budget comparison
            BudgetComparison? budgetComparison = null;
            if (budgetData != null && budgetData.HasBudget)
            {
                budgetComparison = CalculateBudgetComparison(
                    budgetData.MonthlyBudget,
                    totalCosts.AverageMonthlyCost);
            }

            // 12. Calculate depreciation
            var depreciation = CalculateDepreciation(vehicle);

            // 13. Calculate ROI for co-ownership
            var roi = await CalculateCoOwnershipROI(
                vehicleId,
                vehicle.GroupId,
                allTimeExpenses?.TotalAmount ?? 0,
                depreciation,
                accessToken);

            // 14. Generate cost optimization suggestions
            var suggestions = GenerateOptimizationSuggestions(
                expenseBreakdown,
                totalCosts,
                budgetComparison,
                costTrends);

            // 15. Build response
            return new CostAnalysisResponse
            {
                VehicleId = vehicleId,
                VehicleName = vehicle.Model,
                PlateNumber = vehicle.PlateNumber,
                StartDate = startDate,
                EndDate = endDate,
                TotalDays = totalDays,
                ExpenseBreakdown = expenseBreakdown,
                TotalCosts = totalCosts,
                CostPerUnit = costPerUnit,
                CostTrends = costTrends,
                BudgetComparison = budgetComparison,
                Depreciation = depreciation,
                ROI = roi,
                Suggestions = suggestions
            };
        }

        private ExpenseBreakdown CalculateExpenseBreakdown(List<ExpenseDto> expenses)
        {
            return new ExpenseBreakdown
            {
                MaintenanceCosts = expenses
                    .Where(e => e.ExpenseType.Equals("Maintenance", StringComparison.OrdinalIgnoreCase))
                    .Sum(e => e.Amount),
                InsuranceCosts = expenses
                    .Where(e => e.ExpenseType.Equals("Insurance", StringComparison.OrdinalIgnoreCase))
                    .Sum(e => e.Amount),
                RegistrationLicensingCosts = expenses
                    .Where(e => e.ExpenseType.Equals("Registration", StringComparison.OrdinalIgnoreCase) ||
                                e.ExpenseType.Equals("Licensing", StringComparison.OrdinalIgnoreCase))
                    .Sum(e => e.Amount),
                ChargingFuelCosts = expenses
                    .Where(e => e.ExpenseType.Equals("Charging", StringComparison.OrdinalIgnoreCase) ||
                                e.ExpenseType.Equals("Fuel", StringComparison.OrdinalIgnoreCase))
                    .Sum(e => e.Amount),
                CleaningCosts = expenses
                    .Where(e => e.ExpenseType.Equals("Cleaning", StringComparison.OrdinalIgnoreCase))
                    .Sum(e => e.Amount),
                RepairCosts = expenses
                    .Where(e => e.ExpenseType.Equals("Repair", StringComparison.OrdinalIgnoreCase))
                    .Sum(e => e.Amount),
                ParkingTollsCosts = expenses
                    .Where(e => e.ExpenseType.Equals("Parking", StringComparison.OrdinalIgnoreCase) ||
                                e.ExpenseType.Equals("Tolls", StringComparison.OrdinalIgnoreCase))
                    .Sum(e => e.Amount),
                OtherCosts = expenses
                    .Where(e => !new[] { "Maintenance", "Insurance", "Registration", "Licensing",
                                        "Charging", "Fuel", "Cleaning", "Repair", "Parking", "Tolls" }
                                 .Contains(e.ExpenseType, StringComparer.OrdinalIgnoreCase))
                    .Sum(e => e.Amount)
            };
        }

        private TotalCosts CalculateTotalCosts(decimal allTimeCost, decimal periodCost, int totalDays)
        {
            var monthsInPeriod = totalDays / 30.0;
            var averageMonthlyCost = monthsInPeriod > 0 ? periodCost / (decimal)monthsInPeriod : 0;

            return new TotalCosts
            {
                AllTimeCost = allTimeCost,
                PeriodCost = periodCost,
                AverageMonthlyCost = Math.Round(averageMonthlyCost, 2)
            };
        }

        private CostPerUnit CalculateCostPerUnit(
            decimal totalCost,
            decimal totalDistance,
            decimal totalUsageHours,
            int totalTrips)
        {
            return new CostPerUnit
            {
                CostPerKilometer = totalDistance > 0 ? Math.Round(totalCost / totalDistance, 2) : null,
                CostPerUsageHour = totalUsageHours > 0 ? Math.Round(totalCost / totalUsageHours, 2) : null,
                CostPerTrip = totalTrips > 0 ? Math.Round(totalCost / totalTrips, 2) : null
            };
        }

        private List<CostTrendDataPoint> GenerateCostTrends(
            List<ExpenseDto> expenses,
            DateTime startDate,
            DateTime endDate,
            string groupBy)
        {
            var trends = new List<CostTrendDataPoint>();

            if (groupBy.Equals("month", StringComparison.OrdinalIgnoreCase))
            {
                var currentDate = new DateTime(startDate.Year, startDate.Month, 1);
                var endMonth = new DateTime(endDate.Year, endDate.Month, 1);

                while (currentDate <= endMonth)
                {
                    var monthExpenses = expenses
                        .Where(e => e.ExpenseDate.Year == currentDate.Year &&
                                   e.ExpenseDate.Month == currentDate.Month)
                        .ToList();

                    trends.Add(new CostTrendDataPoint
                    {
                        Date = currentDate,
                        Period = currentDate.ToString("yyyy-MM"),
                        TotalCost = monthExpenses.Sum(e => e.Amount),
                        MaintenanceCost = monthExpenses
                            .Where(e => e.ExpenseType.Equals("Maintenance", StringComparison.OrdinalIgnoreCase) ||
                                       e.ExpenseType.Equals("Repair", StringComparison.OrdinalIgnoreCase))
                            .Sum(e => e.Amount),
                        OperationalCost = monthExpenses
                            .Where(e => e.ExpenseType.Equals("Charging", StringComparison.OrdinalIgnoreCase) ||
                                       e.ExpenseType.Equals("Fuel", StringComparison.OrdinalIgnoreCase) ||
                                       e.ExpenseType.Equals("Parking", StringComparison.OrdinalIgnoreCase) ||
                                       e.ExpenseType.Equals("Tolls", StringComparison.OrdinalIgnoreCase))
                            .Sum(e => e.Amount),
                        OtherCost = monthExpenses
                            .Where(e => !new[] { "Maintenance", "Repair", "Charging", "Fuel", "Parking", "Tolls" }
                                         .Contains(e.ExpenseType, StringComparer.OrdinalIgnoreCase))
                            .Sum(e => e.Amount)
                    });

                    currentDate = currentDate.AddMonths(1);
                }
            }
            else if (groupBy.Equals("quarter", StringComparison.OrdinalIgnoreCase))
            {
                var quarters = expenses
                    .GroupBy(e => new
                    {
                        Year = e.ExpenseDate.Year,
                        Quarter = (e.ExpenseDate.Month - 1) / 3 + 1
                    })
                    .OrderBy(g => g.Key.Year)
                    .ThenBy(g => g.Key.Quarter);

                foreach (var quarter in quarters)
                {
                    var quarterExpenses = quarter.ToList();
                    var quarterStart = new DateTime(quarter.Key.Year, (quarter.Key.Quarter - 1) * 3 + 1, 1);

                    trends.Add(new CostTrendDataPoint
                    {
                        Date = quarterStart,
                        Period = $"Q{quarter.Key.Quarter} {quarter.Key.Year}",
                        TotalCost = quarterExpenses.Sum(e => e.Amount),
                        MaintenanceCost = quarterExpenses
                            .Where(e => e.ExpenseType.Equals("Maintenance", StringComparison.OrdinalIgnoreCase) ||
                                       e.ExpenseType.Equals("Repair", StringComparison.OrdinalIgnoreCase))
                            .Sum(e => e.Amount),
                        OperationalCost = quarterExpenses
                            .Where(e => e.ExpenseType.Equals("Charging", StringComparison.OrdinalIgnoreCase) ||
                                       e.ExpenseType.Equals("Fuel", StringComparison.OrdinalIgnoreCase) ||
                                       e.ExpenseType.Equals("Parking", StringComparison.OrdinalIgnoreCase) ||
                                       e.ExpenseType.Equals("Tolls", StringComparison.OrdinalIgnoreCase))
                            .Sum(e => e.Amount),
                        OtherCost = quarterExpenses
                            .Where(e => !new[] { "Maintenance", "Repair", "Charging", "Fuel", "Parking", "Tolls" }
                                         .Contains(e.ExpenseType, StringComparer.OrdinalIgnoreCase))
                            .Sum(e => e.Amount)
                    });
                }
            }
            else // year
            {
                var years = expenses
                    .GroupBy(e => e.ExpenseDate.Year)
                    .OrderBy(g => g.Key);

                foreach (var year in years)
                {
                    var yearExpenses = year.ToList();

                    trends.Add(new CostTrendDataPoint
                    {
                        Date = new DateTime(year.Key, 1, 1),
                        Period = year.Key.ToString(),
                        TotalCost = yearExpenses.Sum(e => e.Amount),
                        MaintenanceCost = yearExpenses
                            .Where(e => e.ExpenseType.Equals("Maintenance", StringComparison.OrdinalIgnoreCase) ||
                                       e.ExpenseType.Equals("Repair", StringComparison.OrdinalIgnoreCase))
                            .Sum(e => e.Amount),
                        OperationalCost = yearExpenses
                            .Where(e => e.ExpenseType.Equals("Charging", StringComparison.OrdinalIgnoreCase) ||
                                       e.ExpenseType.Equals("Fuel", StringComparison.OrdinalIgnoreCase) ||
                                       e.ExpenseType.Equals("Parking", StringComparison.OrdinalIgnoreCase) ||
                                       e.ExpenseType.Equals("Tolls", StringComparison.OrdinalIgnoreCase))
                            .Sum(e => e.Amount),
                        OtherCost = yearExpenses
                            .Where(e => !new[] { "Maintenance", "Repair", "Charging", "Fuel", "Parking", "Tolls" }
                                         .Contains(e.ExpenseType, StringComparer.OrdinalIgnoreCase))
                            .Sum(e => e.Amount)
                    });
                }
            }

            return trends;
        }

        private BudgetComparison? CalculateBudgetComparison(decimal monthlyBudget, decimal actualMonthlyAverage)
        {
            if (monthlyBudget <= 0) return null;

            var variance = actualMonthlyAverage - monthlyBudget;
            var variancePercent = (variance / monthlyBudget) * 100;

            return new BudgetComparison
            {
                MonthlyBudget = monthlyBudget,
                ActualMonthlyAverage = actualMonthlyAverage,
                BudgetVariance = Math.Round(variance, 2),
                BudgetVariancePercent = Math.Round(variancePercent, 2),
                IsOverBudget = variance > 0
            };
        }

        private DepreciationAnalysis CalculateDepreciation(Domain.Entities.Vehicle vehicle)
        {
            // Vehicle entity doesn't have PurchasePrice or PurchaseDate
            // Calculate age from Year only
            var ageMonths = 0;
            if (vehicle.Year > 0)
            {
                // Estimate age from year (assume mid-year purchase)
                ageMonths = (DateTime.UtcNow.Year - vehicle.Year) * 12 + (DateTime.UtcNow.Month - 6);
                ageMonths = Math.Max(0, ageMonths);
            }

            // Return depreciation analysis with null values since we don't have purchase price
            // In a real system, this information would come from Payment Service or be stored separately
            return new DepreciationAnalysis
            {
                PurchasePrice = null,
                CurrentEstimatedValue = null,
                DepreciationRate = null,
                TotalDepreciation = null,
                VehicleAgeMonths = ageMonths
            };
        }

        private Task<CoOwnershipROI> CalculateCoOwnershipROI(
            Guid vehicleId,
            Guid? groupId,
            decimal totalCost,
            DepreciationAnalysis depreciation,
            string accessToken)
        {
            int numberOfOwners = 1;

            if (groupId.HasValue)
            {
                // Count members in group (simplified - in real app, would call Group Service)
                // For now, assume from database or default
                numberOfOwners = 4; // Default assumption for co-ownership
            }

            var totalCostPerOwner = numberOfOwners > 0 ? totalCost / numberOfOwners : totalCost;

            // Estimate individual ownership cost (including depreciation, insurance, maintenance)
            var estimatedIndividualCost = totalCost;
            if (depreciation.TotalDepreciation.HasValue)
            {
                estimatedIndividualCost = totalCost + depreciation.TotalDepreciation.Value;
            }

            // In co-ownership, depreciation is shared
            var savings = estimatedIndividualCost - totalCostPerOwner;
            var savingsPercent = estimatedIndividualCost > 0
                ? (savings / estimatedIndividualCost) * 100
                : 0;

            return Task.FromResult(new CoOwnershipROI
            {
                NumberOfOwners = numberOfOwners,
                TotalCostPerOwner = Math.Round(totalCostPerOwner, 2),
                EstimatedIndividualOwnershipCost = Math.Round(estimatedIndividualCost, 2),
                SavingsPerOwner = Math.Round(savings, 2),
                SavingsPercent = Math.Round(savingsPercent, 2)
            });
        }

        private List<CostOptimizationSuggestion> GenerateOptimizationSuggestions(
            ExpenseBreakdown breakdown,
            TotalCosts totalCosts,
            BudgetComparison? budgetComparison,
            List<CostTrendDataPoint> trends)
        {
            var suggestions = new List<CostOptimizationSuggestion>();

            // High maintenance costs
            if (breakdown.MaintenanceCosts > totalCosts.AverageMonthlyCost * 0.3m)
            {
                suggestions.Add(new CostOptimizationSuggestion
                {
                    Category = "Maintenance",
                    Issue = "Maintenance costs are high (>30% of monthly average)",
                    Suggestion = "Review maintenance schedule and consider preventive maintenance to reduce reactive repairs",
                    PotentialSavings = breakdown.MaintenanceCosts * 0.20m, // Estimate 20% savings
                    Priority = "High"
                });
            }

            // High charging costs
            if (breakdown.ChargingFuelCosts > totalCosts.AverageMonthlyCost * 0.25m)
            {
                suggestions.Add(new CostOptimizationSuggestion
                {
                    Category = "Charging",
                    Issue = "Charging costs are high (>25% of monthly average)",
                    Suggestion = "Consider optimizing charging times (off-peak hours) or exploring alternative charging locations with lower rates",
                    PotentialSavings = breakdown.ChargingFuelCosts * 0.15m,
                    Priority = "Medium"
                });
            }

            // Over budget
            if (budgetComparison != null && budgetComparison.IsOverBudget)
            {
                suggestions.Add(new CostOptimizationSuggestion
                {
                    Category = "Budget",
                    Issue = $"Spending exceeds budget by {budgetComparison.BudgetVariancePercent}%",
                    Suggestion = "Review discretionary expenses and implement stricter cost controls",
                    PotentialSavings = budgetComparison.BudgetVariance,
                    Priority = "High"
                });
            }

            // Increasing costs trend
            if (trends.Count >= 3)
            {
                var recentTrends = trends.TakeLast(3).ToList();
                var isIncreasing = recentTrends[0].TotalCost < recentTrends[1].TotalCost &&
                                  recentTrends[1].TotalCost < recentTrends[2].TotalCost;

                if (isIncreasing)
                {
                    suggestions.Add(new CostOptimizationSuggestion
                    {
                        Category = "Trend",
                        Issue = "Costs are consistently increasing over recent periods",
                        Suggestion = "Investigate root cause of increasing costs and implement cost control measures",
                        PotentialSavings = null,
                        Priority = "Medium"
                    });
                }
            }

            // High repair costs (relative to maintenance)
            if (breakdown.RepairCosts > breakdown.MaintenanceCosts * 1.5m)
            {
                suggestions.Add(new CostOptimizationSuggestion
                {
                    Category = "Repair",
                    Issue = "Repair costs exceed maintenance costs significantly",
                    Suggestion = "Increase preventive maintenance frequency to reduce unexpected repairs",
                    PotentialSavings = breakdown.RepairCosts * 0.30m,
                    Priority = "High"
                });
            }

            // If no suggestions, add a positive note
            if (suggestions.Count == 0)
            {
                suggestions.Add(new CostOptimizationSuggestion
                {
                    Category = "General",
                    Issue = "No major cost issues detected",
                    Suggestion = "Continue current practices. Costs are well-managed.",
                    PotentialSavings = null,
                    Priority = "Low"
                });
            }

            return suggestions;
        }
    }
}
