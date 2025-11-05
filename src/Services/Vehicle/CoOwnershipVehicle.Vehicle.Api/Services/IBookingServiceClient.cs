
using System;
using System.Threading.Tasks;
using DTOs = CoOwnershipVehicle.Vehicle.Api.DTOs;

namespace CoOwnershipVehicle.Vehicle.Api.Services
{
    public interface IBookingServiceClient
    {
        Task<DTOs.BookingConflictDto> CheckAvailabilityAsync(Guid vehicleId, DateTime from, DateTime to, string accessToken);

        /// <summary>
        /// Get booking statistics for a vehicle within a date range
        /// </summary>
        Task<DTOs.VehicleBookingStatistics?> GetVehicleBookingStatisticsAsync(Guid vehicleId, DateTime startDate, DateTime endDate, string accessToken);
    }
}
