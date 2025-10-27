
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using System;
using System.Threading.Tasks;

namespace CoOwnershipVehicle.Vehicle.Api.Services
{
    public interface IBookingServiceClient
    {
        Task<BookingConflictDto> CheckAvailabilityAsync(Guid vehicleId, DateTime from, DateTime to, string accessToken);
    }
}
