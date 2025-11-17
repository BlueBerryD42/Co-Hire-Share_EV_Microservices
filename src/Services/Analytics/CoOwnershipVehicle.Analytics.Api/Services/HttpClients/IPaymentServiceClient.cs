using CoOwnershipVehicle.Shared.Contracts.DTOs;

namespace CoOwnershipVehicle.Analytics.Api.Services.HttpClients;

public interface IPaymentServiceClient
{
    Task<List<ExpenseDto>> GetExpensesAsync(Guid? groupId = null, DateTime? from = null, DateTime? to = null);
}

