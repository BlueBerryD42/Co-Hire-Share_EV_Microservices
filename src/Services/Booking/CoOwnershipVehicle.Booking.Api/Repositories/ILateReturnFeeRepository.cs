using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Booking.Api.Repositories;

public interface ILateReturnFeeRepository
{
    Task AddAsync(LateReturnFee fee, CancellationToken cancellationToken = default);

    Task<LateReturnFee?> GetByIdAsync(Guid feeId, CancellationToken cancellationToken = default);

    Task<LateReturnFee?> GetByCheckInAsync(Guid checkInId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LateReturnFee>> GetByUserAsync(Guid userId, int? take = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LateReturnFee>> GetByBookingAsync(Guid bookingId, CancellationToken cancellationToken = default);

    Task<LateReturnFee?> GetForUpdateAsync(Guid feeId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
