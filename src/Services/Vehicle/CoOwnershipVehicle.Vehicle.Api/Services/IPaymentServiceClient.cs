using System;
using System.Threading.Tasks;
using CoOwnershipVehicle.Vehicle.Api.DTOs;

namespace CoOwnershipVehicle.Vehicle.Api.Services
{
    /// <summary>
    /// Client interface for communicating with Payment Service
    /// </summary>
    public interface IPaymentServiceClient
    {
        /// <summary>
        /// Get all expenses for a vehicle within a date range
        /// </summary>
        Task<VehicleExpensesResponse?> GetVehicleExpensesAsync(
            Guid vehicleId,
            DateTime startDate,
            DateTime endDate,
            string accessToken);

        /// <summary>
        /// Get budget information for a vehicle
        /// </summary>
        Task<VehicleBudgetResponse?> GetVehicleBudgetAsync(
            Guid vehicleId,
            string accessToken);
    }
}
