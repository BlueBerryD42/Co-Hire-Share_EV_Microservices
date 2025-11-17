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

    public async Task<IReadOnlyList<CheckIn>> GetByBookingAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        return await _context.CheckIns
            .AsNoTracking()
            .Include(ci => ci.Photos.Where(p => !p.IsDeleted))
            .Where(ci => ci.BookingId == bookingId)
            .OrderBy(ci => ci.CheckInTime)
            .ToListAsync(cancellationToken);
    }

    public Task<CheckIn?> GetLatestAsync(Guid bookingId, CheckInType type, CancellationToken cancellationToken = default)
    {
        return _context.CheckIns
            .AsNoTracking()
            .Where(ci => ci.BookingId == bookingId && ci.Type == type)
            .OrderByDescending(ci => ci.CheckInTime)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
