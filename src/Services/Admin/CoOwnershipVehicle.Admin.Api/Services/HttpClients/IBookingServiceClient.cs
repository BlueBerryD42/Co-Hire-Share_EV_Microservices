using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Admin.Api.Services.HttpClients;

public interface IBookingServiceClient
{
    Task<List<BookingDto>> GetBookingsAsync(DateTime? from = null, DateTime? to = null, Guid? userId = null, Guid? groupId = null);
    Task<int> GetBookingCountAsync(BookingStatus? status = null);
    Task<BookingDto?> GetBookingAsync(Guid bookingId);
}

