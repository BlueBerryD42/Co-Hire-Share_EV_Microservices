using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Booking.Api.Repositories;

public interface ICheckInRepository
{
    Task AddAsync(CheckIn checkIn, CancellationToken cancellationToken = default);
    Task AddPhotosAsync(IEnumerable<CheckInPhoto> photos, CancellationToken cancellationToken = default);
    void Remove(CheckIn checkIn);
    void RemovePhotos(IEnumerable<CheckInPhoto> photos);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<CheckIn?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CheckIn>> GetByBookingAsync(Guid bookingId, CancellationToken cancellationToken = default);
    Task<CheckIn?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CheckIn?> GetForPhotoUploadAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CheckInPhoto?> GetPhotoAsync(Guid checkInId, Guid photoId, CancellationToken cancellationToken = default);
    Task<CheckIn?> GetForSignatureAsync(Guid checkInId, CancellationToken cancellationToken = default);
    Task<CheckIn?> GetForDeletionAsync(Guid checkInId, CancellationToken cancellationToken = default);
    Task<CheckIn?> GetForDamageReportAsync(Guid checkInId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CheckIn>> GetFilteredAsync(Guid? vehicleId, Guid? userId, DateTime? from, DateTime? to, CheckInType? type, CancellationToken cancellationToken = default);
}
