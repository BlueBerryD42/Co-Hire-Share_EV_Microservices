using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Booking.Api.Repositories;

public interface IDamageReportRepository
{
    Task AddAsync(DamageReport report, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DamageReport>> GetByCheckInAsync(Guid checkInId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DamageReport>> GetByBookingAsync(Guid bookingId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DamageReport>> GetByVehicleAsync(Guid vehicleId, CancellationToken cancellationToken = default);
    Task<DamageReport?> GetByIdWithDetailsAsync(Guid reportId, CancellationToken cancellationToken = default);
}
