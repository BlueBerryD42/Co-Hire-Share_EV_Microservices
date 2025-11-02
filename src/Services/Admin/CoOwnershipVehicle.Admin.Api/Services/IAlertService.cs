using CoOwnershipVehicle.Shared.Contracts.DTOs;

namespace CoOwnershipVehicle.Admin.Api.Services;

public interface IAlertService
{
    Task<List<AlertDto>> GetActiveAlertsAsync();
    Task CheckAndTriggerAlertsAsync();
    Task<bool> CreateAlertAsync(string type, string title, string message, string severity);
}

