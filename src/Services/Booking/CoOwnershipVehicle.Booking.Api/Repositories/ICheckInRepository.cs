using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Booking.Api.Repositories;

public interface ICheckInRepository
{
    Task AddAsync(CheckIn checkIn, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CheckIn>> GetByBookingAsync(Guid bookingId, CancellationToken cancellationToken = default);
    Task<CheckIn?> GetLatestAsync(Guid bookingId, CheckInType type, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
