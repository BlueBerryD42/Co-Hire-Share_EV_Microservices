using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CoOwnershipVehicle.Booking.Api.Data;
using CoOwnershipVehicle.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoOwnershipVehicle.Booking.Api.Repositories;

public class DamageReportRepository : IDamageReportRepository
{
    private readonly BookingDbContext _context;

    public DamageReportRepository(BookingDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task AddAsync(DamageReport report, CancellationToken cancellationToken = default)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        await _context.DamageReports.AddAsync(report, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DamageReport>> GetByCheckInAsync(Guid checkInId, CancellationToken cancellationToken = default)
    {
        var results = await _context.DamageReports
            .AsNoTracking()
            .Where(r => r.CheckInId == checkInId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        return results;
    }

    public async Task<IReadOnlyList<DamageReport>> GetByBookingAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        var results = await _context.DamageReports
            .AsNoTracking()
            .Where(r => r.BookingId == bookingId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        return results;
    }

    public async Task<IReadOnlyList<DamageReport>> GetByVehicleAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        var results = await _context.DamageReports
            .AsNoTracking()
            .Where(r => r.VehicleId == vehicleId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        return results;
    }

    public Task<DamageReport?> GetByIdWithDetailsAsync(Guid reportId, CancellationToken cancellationToken = default)
    {
        return _context.DamageReports
            .Include(r => r.CheckIn)
                .ThenInclude(ci => ci.Booking)
                    .ThenInclude(b => b.Group)
                        .ThenInclude(g => g.Members)
            .FirstOrDefaultAsync(r => r.Id == reportId, cancellationToken);
    }
}
