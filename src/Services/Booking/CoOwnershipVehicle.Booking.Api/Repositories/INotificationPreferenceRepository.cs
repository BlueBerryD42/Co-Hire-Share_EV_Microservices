using System;
using System.Threading;
using System.Threading.Tasks;
using CoOwnershipVehicle.Booking.Api.Entities;

namespace CoOwnershipVehicle.Booking.Api.Repositories;

public interface INotificationPreferenceRepository
{
    Task<BookingNotificationPreference?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task UpsertAsync(BookingNotificationPreference preference, CancellationToken cancellationToken = default);
}
