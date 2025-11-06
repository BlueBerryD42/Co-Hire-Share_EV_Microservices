using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Shared.Contracts.DTOs;

public class FinancialOverviewDto
{
    public decimal TotalRevenueAllTime { get; set; }
    public decimal TotalRevenueYear { get; set; }
    public decimal TotalRevenueMonth { get; set; }
    public decimal TotalRevenueWeek { get; set; }
    public decimal TotalRevenueDay { get; set; }
    public List<KeyValuePair<string, decimal>> RevenueBySource { get; set; } = new();
    public decimal TotalExpensesAllGroups { get; set; }
    public decimal TotalFundBalances { get; set; }
    public double PaymentSuccessRate { get; set; }
    public int FailedPaymentsCount { get; set; }
    public decimal FailedPaymentsAmount { get; set; }
    public int PendingPaymentsCount { get; set; }
    public List<TimeSeriesPointDto<decimal>> RevenueTrend { get; set; } = new();
    public List<GroupSpendSummaryDto> TopSpendingGroups { get; set; } = new();
    public int FinancialHealthScore { get; set; }
}

public class GroupSpendSummaryDto
{
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public decimal TotalExpenses { get; set; }
}

public class TimeSeriesPointDto<T>
{
    public DateTime Date { get; set; }
    public T Value { get; set; } = default!;
}

public class FinancialGroupBreakdownDto
{
    public List<GroupFinancialItemDto> Groups { get; set; } = new();
}

public class GroupFinancialItemDto
{
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public decimal TotalExpenses { get; set; }
    public Dictionary<string, decimal> ExpensesByType { get; set; } = new();
    public decimal FundBalance { get; set; }
    public bool HasFinancialIssues { get; set; }
    public double PaymentComplianceRate { get; set; }
}

public class PaymentStatisticsDto
{
    public double SuccessRate { get; set; }
    public double FailureRate { get; set; }
    public Dictionary<PaymentMethod, int> MethodCounts { get; set; } = new();
    public decimal AverageAmount { get; set; }
    public List<TimeSeriesPointDto<int>> VolumeTrend { get; set; } = new();
    public Dictionary<string, int> FailedReasons { get; set; } = new();
    public VnPaySummaryDto VnPay { get; set; } = new();
}

public class VnPaySummaryDto
{
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public decimal TotalAmount { get; set; }
}

public class ExpenseAnalysisDto
{
    public Dictionary<ExpenseType, decimal> TotalByType { get; set; } = new();
    public List<TimeSeriesPointDto<decimal>> ExpenseTrend { get; set; } = new();
    public decimal AverageCostPerVehicle { get; set; }
    public Dictionary<Guid, decimal> CostPerGroup { get; set; } = new();
    public List<string> OptimizationOpportunities { get; set; } = new();
}

public class FinancialAnomaliesDto
{
    public List<PaymentAnomalyDto> UnusualTransactions { get; set; } = new();
    public List<SuspiciousPatternDto> SuspiciousPaymentPatterns { get; set; } = new();
    public List<GroupNegativeBalanceDto> NegativeBalanceGroups { get; set; } = new();
}

public class PaymentAnomalyDto
{
    public Guid PaymentId { get; set; }
    public decimal Amount { get; set; }
    public double ZScore { get; set; }
    public DateTime? PaidAt { get; set; }
    public PaymentMethod Method { get; set; }
}

public class SuspiciousPatternDto
{
    public Guid PayerId { get; set; }
    public int FailedCount7Days { get; set; }
}

public class GroupNegativeBalanceDto
{
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public decimal Balance { get; set; }
}

public class FinancialReportRequestDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Type { get; set; } = "Monthly"; // Monthly | Quarterly | Tax
}


