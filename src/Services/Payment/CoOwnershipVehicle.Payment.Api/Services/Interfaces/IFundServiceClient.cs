using CoOwnershipVehicle.Shared.Contracts.DTOs;

namespace CoOwnershipVehicle.Payment.Api.Services.Interfaces;

/// <summary>
/// HTTP client interface for communicating with Group Service's Fund endpoints
/// Used to pay expenses from group fund and track fund transactions
/// </summary>
public interface IFundServiceClient
{
    /// <summary>
    /// Get fund balance for a group
    /// </summary>
    Task<FundBalanceDto?> GetFundBalanceAsync(Guid groupId, string accessToken);

    /// <summary>
    /// Pay an expense from group fund
    /// Creates a fund transaction of type ExpensePayment
    /// </summary>
    Task<FundTransactionDto?> PayExpenseFromFundAsync(
        Guid groupId,
        Guid expenseId,
        decimal amount,
        string description,
        Guid initiatedBy,
        string accessToken);

    /// <summary>
    /// Check if group fund has sufficient balance
    /// </summary>
    Task<bool> HasSufficientBalanceAsync(Guid groupId, decimal amount, string accessToken);

    /// <summary>
    /// Complete fund deposit after VNPay payment callback
    /// Called by Payment service after successful VNPay payment
    /// </summary>
    Task<FundTransactionDto?> CompleteFundDepositAsync(
        Guid groupId,
        decimal amount,
        string description,
        string paymentReference,
        Guid initiatedBy,
        string? reference,
        string accessToken);
}

