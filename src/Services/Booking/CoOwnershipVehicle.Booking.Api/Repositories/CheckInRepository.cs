using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CoOwnershipVehicle.Booking.Api.Data;
using CoOwnershipVehicle.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoOwnershipVehicle.Booking.Api.Repositories;

public class CheckInRepository : ICheckInRepository
{
    private readonly BookingDbContext _context;

    public CheckInRepository(BookingDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task AddAsync(CheckIn checkIn, CancellationToken cancellationToken = default)
    {
        if (checkIn == null)
        {
            throw new ArgumentNullException(nameof(checkIn));
        }

        await _context.CheckIns.AddAsync(checkIn, cancellationToken);
    }

    public Task AddPhotosAsync(IEnumerable<CheckInPhoto> photos, CancellationToken cancellationToken = default)
    {
        if (photos == null)
        {
            throw new ArgumentNullException(nameof(photos));
        }

        return _context.CheckInPhotos.AddRangeAsync(photos, cancellationToken);
    }

    public void Remove(CheckIn checkIn)
    {
        _context.CheckIns.Remove(checkIn);
    }

    public void RemovePhotos(IEnumerable<CheckInPhoto> photos)
    {
        if (photos == null)
        {
            throw new ArgumentNullException(nameof(photos));
        }

        _context.CheckInPhotos.RemoveRange(photos);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }

    public Task<CheckIn?> GetWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _context.CheckIns
            .AsNoTracking()
            .Include(ci => ci.User)
            .Include(ci => ci.LateReturnFee)
            .Include(ci => ci.Photos.Where(p => !p.IsDeleted))
            .FirstOrDefaultAsync(ci => ci.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<CheckIn>> GetByBookingAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        var result = await _context.CheckIns
            .AsNoTracking()
            .Include(ci => ci.User)
            .Include(ci => ci.LateReturnFee)
            .Include(ci => ci.Photos.Where(p => !p.IsDeleted))
            .Where(ci => ci.BookingId == bookingId)
            .OrderBy(ci => ci.CheckInTime)
            .ToListAsync(cancellationToken);

        return result;
    }

    public Task<CheckIn?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _context.CheckIns
            .Include(ci => ci.Photos.Where(p => !p.IsDeleted))
            .Include(ci => ci.Booking)
            .Include(ci => ci.LateReturnFee)
            .FirstOrDefaultAsync(ci => ci.Id == id, cancellationToken);
    }

    public Task<CheckIn?> GetForPhotoUploadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _context.CheckIns
            .Include(ci => ci.Photos)
            .Include(ci => ci.LateReturnFee)
            .FirstOrDefaultAsync(ci => ci.Id == id, cancellationToken);
    }

    public Task<CheckInPhoto?> GetPhotoAsync(Guid checkInId, Guid photoId, CancellationToken cancellationToken = default)
    {
        return _context.CheckInPhotos
            .Include(p => p.CheckIn)
            .FirstOrDefaultAsync(p => p.Id == photoId && p.CheckInId == checkInId, cancellationToken);
    }

    public Task<CheckIn?> GetForSignatureAsync(Guid checkInId, CancellationToken cancellationToken = default)
    {
        return _context.CheckIns
            .Include(ci => ci.Booking)
                .ThenInclude(b => b.Group)
            .Include(ci => ci.LateReturnFee)
            .FirstOrDefaultAsync(ci => ci.Id == checkInId, cancellationToken);
    }

    public Task<CheckIn?> GetForDeletionAsync(Guid checkInId, CancellationToken cancellationToken = default)
    {
        return _context.CheckIns
            .Include(ci => ci.Booking)
            .Include(ci => ci.LateReturnFee)
            .Include(ci => ci.Photos)
            .FirstOrDefaultAsync(ci => ci.Id == checkInId, cancellationToken);
    }

    public Task<CheckIn?> GetForDamageReportAsync(Guid checkInId, CancellationToken cancellationToken = default)
    {
        return _context.CheckIns
            .Include(ci => ci.Booking)
                .ThenInclude(b => b.Group)
                    .ThenInclude(g => g.Members)
            .Include(ci => ci.Booking)
                .ThenInclude(b => b.Vehicle)
            .Include(ci => ci.LateReturnFee)
            .Include(ci => ci.Photos)
            .FirstOrDefaultAsync(ci => ci.Id == checkInId, cancellationToken);
    }

    public async Task<IReadOnlyList<CheckIn>> GetFilteredAsync(Guid? vehicleId, Guid? userId, DateTime? from, DateTime? to, CheckInType? type, CancellationToken cancellationToken = default)
    {
        var query = _context.CheckIns
            .AsNoTracking()
            .Include(ci => ci.User)
            .Include(ci => ci.Photos.Where(p => !p.IsDeleted))
            .Include(ci => ci.Booking)
                .ThenInclude(b => b.Group)
                    .ThenInclude(g => g.Members)
            .Include(ci => ci.Booking)
                .ThenInclude(b => b.Vehicle)
            .Include(ci => ci.LateReturnFee)
            .AsQueryable();

        if (vehicleId.HasValue && vehicleId.Value != Guid.Empty)
        {
            query = query.Where(ci => ci.VehicleId == vehicleId.Value || ci.Booking.VehicleId == vehicleId.Value);
        }

        if (userId.HasValue && userId.Value != Guid.Empty)
        {
            query = query.Where(ci => ci.UserId == userId.Value);
        }

        if (from.HasValue)
        {
            query = query.Where(ci => ci.CheckInTime >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(ci => ci.CheckInTime <= to.Value);
        }

        if (type.HasValue)
        {
            query = query.Where(ci => ci.Type == type.Value);
        }

        var results = await query
            .OrderByDescending(ci => ci.CheckInTime)
            .ToListAsync(cancellationToken);

        return results;
    }
}
