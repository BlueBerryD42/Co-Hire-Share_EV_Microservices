using CoOwnershipVehicle.Shared.Contracts.DTOs;

namespace CoOwnershipVehicle.Admin.Api.Services.HttpClients;

public interface IPaymentServiceClient
{
    Task<List<PaymentDto>> GetPaymentsAsync(DateTime? from = null, DateTime? to = null, PaymentStatus? status = null);
    Task<List<ExpenseDto>> GetExpensesAsync(Guid? groupId = null, DateTime? from = null, DateTime? to = null);
    Task<decimal> GetTotalRevenueAsync(DateTime? from = null, DateTime? to = null);
    Task<decimal> GetTotalExpensesAsync(Guid? groupId = null, DateTime? from = null, DateTime? to = null);
}

