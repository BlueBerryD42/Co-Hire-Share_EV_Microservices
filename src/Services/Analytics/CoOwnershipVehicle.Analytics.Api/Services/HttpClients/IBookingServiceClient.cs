using CoOwnershipVehicle.Shared.Contracts.DTOs;

namespace CoOwnershipVehicle.Analytics.Api.Services.HttpClients;

public interface IBookingServiceClient
{
    Task<List<BookingDto>> GetBookingsAsync(DateTime? from = null, DateTime? to = null, Guid? groupId = null);
    Task<List<CheckInDto>> GetBookingCheckInsAsync(Guid bookingId);
}

