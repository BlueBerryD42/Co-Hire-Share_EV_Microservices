using System.ComponentModel.DataAnnotations;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Shared.Contracts.DTOs;

public class FundBalanceDto
{
    public Guid GroupId { get; set; }
    public decimal TotalBalance { get; set; }
    public decimal ReserveBalance { get; set; }
    public decimal AvailableBalance { get; set; }
    public DateTime LastUpdated { get; set; }
    public List<FundTransactionDto> RecentTransactions { get; set; } = new();
    public FundStatisticsDto Statistics { get; set; } = new();
}

public class FundStatisticsDto
{
    public decimal TotalDeposits { get; set; }
    public decimal TotalWithdrawals { get; set; }
    public decimal NetChange { get; set; }
    public Dictionary<string, decimal> MemberContributions { get; set; } = new();
}

public class DepositFundDto
{
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
    public decimal Amount { get; set; }

    [Required]
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Reference { get; set; }

    [Range(0, 100)]
    public decimal? AutoAllocateToReservePercent { get; set; }
}

public class WithdrawFundDto
{
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
    public decimal Amount { get; set; }

    [Required]
    [StringLength(500)]
    public string Reason { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Recipient { get; set; }
}

public class AllocateReserveDto
{
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
    public decimal Amount { get; set; }

    [Required]
    [StringLength(500)]
    public string Reason { get; set; } = string.Empty;
}

public class ReleaseReserveDto
{
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
    public decimal Amount { get; set; }

    [Required]
    [StringLength(500)]
    public string Reason { get; set; } = string.Empty;
}

public class FundTransactionDto
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public Guid InitiatedBy { get; set; }
    public string InitiatorName { get; set; } = string.Empty;
    public FundTransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }
    public string Description { get; set; } = string.Empty;
    public FundTransactionStatus Status { get; set; }
    public Guid? ApprovedBy { get; set; }
    public string? ApproverName { get; set; }
    public DateTime TransactionDate { get; set; }
    public string? Reference { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class FundTransactionHistoryDto
{
    public List<FundTransactionDto> Transactions { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class FundSummaryDto
{
    public string Period { get; set; } = string.Empty; // monthly, quarterly, yearly
    public decimal TotalDeposits { get; set; }
    public decimal TotalWithdrawals { get; set; }
    public decimal NetChange { get; set; }
    public decimal AverageBalance { get; set; }
    public Dictionary<string, decimal> MemberContributions { get; set; } = new();
    public decimal ReserveAllocationChanges { get; set; }
}

