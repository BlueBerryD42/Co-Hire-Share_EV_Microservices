using CoOwnershipVehicle.Shared.Contracts.DTOs;

namespace CoOwnershipVehicle.Booking.Api.Contracts;

public interface ICheckInService
{
    Task<CheckInDto> StartTripAsync(StartTripDto request, Guid userId, CancellationToken cancellationToken = default);
    Task<CheckInDto> EndTripAsync(EndTripDto request, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CheckInDto>> GetBookingHistoryAsync(Guid bookingId, Guid userId, CancellationToken cancellationToken = default);
}
