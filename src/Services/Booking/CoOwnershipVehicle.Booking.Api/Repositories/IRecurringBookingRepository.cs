using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Booking.Api.Repositories;

public interface IRecurringBookingRepository
{
    Task AddAsync(RecurringBooking recurringBooking, CancellationToken cancellationToken = default);

    Task<RecurringBooking?> GetByIdAsync(Guid recurringBookingId, bool includeGeneratedBookings = false, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecurringBooking>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecurringBooking>> GetActiveAsync(DateTime generateThroughUtc, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecurringBooking>> GetRecurringBookingsToGenerateAsync(
        DateTime nowUtc,
        DateTime generationCutoff,
        DateTime lookBackCutoff,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CoOwnershipVehicle.Domain.Entities.Booking>> GetFutureGeneratedBookingsAsync(Guid recurringBookingId, DateTime fromUtc, CancellationToken cancellationToken = default);

    Task AddGeneratedBookingsAsync(IEnumerable<CoOwnershipVehicle.Domain.Entities.Booking> bookings, CancellationToken cancellationToken = default);

    void RemoveGeneratedBookings(IEnumerable<CoOwnershipVehicle.Domain.Entities.Booking> bookings);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
