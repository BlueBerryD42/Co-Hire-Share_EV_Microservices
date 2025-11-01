
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using System;
using System.Threading.Tasks;

namespace CoOwnershipVehicle.Vehicle.Api.Services
{
    public interface IBookingServiceClient
    {
        Task<BookingConflictDto> CheckAvailabilityAsync(Guid vehicleId, DateTime from, DateTime to, string accessToken);

        /// <summary>
        /// Get booking statistics for a vehicle within a date range
        /// </summary>
        Task<VehicleBookingStatistics?> GetVehicleBookingStatisticsAsync(Guid vehicleId, DateTime startDate, DateTime endDate, string accessToken);
    }
}
