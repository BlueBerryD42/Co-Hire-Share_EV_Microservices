using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CoOwnershipVehicle.Booking.Api.Data;
using CoOwnershipVehicle.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoOwnershipVehicle.Booking.Api.Repositories;

public class LateReturnFeeRepository : ILateReturnFeeRepository
{
    private readonly BookingDbContext _context;

    public LateReturnFeeRepository(BookingDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddAsync(LateReturnFee fee, CancellationToken cancellationToken = default)
    {
        if (fee == null)
        {
            throw new ArgumentNullException(nameof(fee));
        }

        await _context.LateReturnFees.AddAsync(fee, cancellationToken);
    }

    public Task<LateReturnFee?> GetByIdAsync(Guid feeId, CancellationToken cancellationToken = default)
    {
        return _context.LateReturnFees
            .AsNoTracking()
            .Include(f => f.Booking)
                .ThenInclude(b => b.Vehicle)
            .Include(f => f.CheckIn)
            .Include(f => f.User)
            .FirstOrDefaultAsync(f => f.Id == feeId, cancellationToken);
    }

    public Task<LateReturnFee?> GetByCheckInAsync(Guid checkInId, CancellationToken cancellationToken = default)
    {
        return _context.LateReturnFees
            .AsNoTracking()
            .Include(f => f.Booking)
            .Include(f => f.CheckIn)
            .FirstOrDefaultAsync(f => f.CheckInId == checkInId, cancellationToken);
    }

    public Task<LateReturnFee?> GetForUpdateAsync(Guid feeId, CancellationToken cancellationToken = default)
    {
        return _context.LateReturnFees
            .Include(f => f.CheckIn)
            .FirstOrDefaultAsync(f => f.Id == feeId, cancellationToken);
    }

    public async Task<IReadOnlyList<LateReturnFee>> GetByUserAsync(Guid userId, int? take = null, CancellationToken cancellationToken = default)
    {
        var query = _context.LateReturnFees
            .AsNoTracking()
            .Include(f => f.Booking)
            .Include(f => f.CheckIn)
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.CreatedAt)
            .AsQueryable();

        if (take.HasValue && take.Value > 0)
        {
            query = query.Take(take.Value);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LateReturnFee>> GetByBookingAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        var fees = await _context.LateReturnFees
            .AsNoTracking()
            .Include(f => f.CheckIn)
            .Where(f => f.BookingId == bookingId)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(cancellationToken);

        return fees;
    }
}
