using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Analytics.Api.Services.HttpClients;

public interface IPaymentServiceClient
{
    Task<List<PaymentDto>> GetPaymentsAsync(DateTime? from = null, DateTime? to = null);
    Task<List<ExpenseDto>> GetExpensesAsync(Guid? groupId = null, DateTime? from = null, DateTime? to = null);
}

