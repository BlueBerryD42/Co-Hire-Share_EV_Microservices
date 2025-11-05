using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CoOwnershipVehicle.Booking.Api.Data;
using CoOwnershipVehicle.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoOwnershipVehicle.Booking.Api.Repositories;

public class RecurringBookingRepository : IRecurringBookingRepository
{
    private readonly BookingDbContext _context;

    public RecurringBookingRepository(BookingDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public Task AddAsync(RecurringBooking recurringBooking, CancellationToken cancellationToken = default)
    {
        if (recurringBooking == null)
        {
            throw new ArgumentNullException(nameof(recurringBooking));
        }

        return _context.RecurringBookings.AddAsync(recurringBooking, cancellationToken).AsTask();
    }

    public Task<RecurringBooking?> GetByIdAsync(Guid recurringBookingId, bool includeGeneratedBookings = false, CancellationToken cancellationToken = default)
    {
        var query = _context.RecurringBookings
            .Include(rb => rb.Vehicle)
            .Include(rb => rb.Group)
            .Include(rb => rb.User)
            .AsQueryable();

        if (includeGeneratedBookings)
        {
            query = query
                .Include(rb => rb.GeneratedBookings.Where(b => b.StartAt >= DateTime.UtcNow))
                    .ThenInclude(b => b.User)
                .Include(rb => rb.GeneratedBookings)
                    .ThenInclude(b => b.Vehicle);
        }

        return query.FirstOrDefaultAsync(rb => rb.Id == recurringBookingId, cancellationToken);
    }

    public async Task<IReadOnlyList<RecurringBooking>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.RecurringBookings
            .AsNoTracking()
            .Include(rb => rb.Vehicle)
            .Where(rb => rb.UserId == userId)
            .OrderBy(rb => rb.RecurrenceStartDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RecurringBooking>> GetActiveAsync(DateTime generateThroughUtc, CancellationToken cancellationToken = default)
    {
        return await _context.RecurringBookings
            .Include(rb => rb.Vehicle)
            .Include(rb => rb.Group)
            .Where(rb =>
                rb.Status == RecurringBookingStatus.Active ||
                (rb.Status == RecurringBookingStatus.Paused && (rb.PausedUntilUtc == null || rb.PausedUntilUtc <= generateThroughUtc)))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RecurringBooking>> GetRecurringBookingsToGenerateAsync(
        DateTime nowUtc,
        DateTime generationCutoff,
        DateTime lookBackCutoff,
        CancellationToken cancellationToken = default)
    {
        return await _context.RecurringBookings
            .Include(rb => rb.Vehicle)
            .Include(rb => rb.Group) // Include Group to ensure GroupId is available
            .Where(rb =>
                (rb.Status == RecurringBookingStatus.Active ||
                 (rb.Status == RecurringBookingStatus.Paused && (rb.PausedUntilUtc == null || rb.PausedUntilUtc <= nowUtc))) &&
                rb.RecurrenceStartDate.ToDateTime(TimeOnly.MinValue).Date <= generationCutoff.Date && // Ensure recurrence started or will start within generation window
                (rb.RecurrenceEndDate == null || rb.RecurrenceEndDate.Value.ToDateTime(TimeOnly.MinValue).Date >= nowUtc.Date) && // Ensure recurrence hasn't ended
                (rb.LastGeneratedUntilUtc == null || rb.LastGeneratedUntilUtc < generationCutoff || (rb.LastGenerationRunAtUtc != null && rb.LastGenerationRunAtUtc < lookBackCutoff)))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CoOwnershipVehicle.Domain.Entities.Booking>> GetFutureGeneratedBookingsAsync(Guid recurringBookingId, DateTime fromUtc, CancellationToken cancellationToken = default)
    {
        return await _context.Bookings
            .Include(b => b.User)
            .Where(b => b.RecurringBookingId == recurringBookingId && b.StartAt >= fromUtc)
            .OrderBy(b => b.StartAt)
            .ToListAsync(cancellationToken);
    }

    public Task AddGeneratedBookingsAsync(IEnumerable<CoOwnershipVehicle.Domain.Entities.Booking> bookings, CancellationToken cancellationToken = default)
    {
        if (bookings == null)
        {
            throw new ArgumentNullException(nameof(bookings));
        }

        return _context.Bookings.AddRangeAsync(bookings, cancellationToken);
    }

    public void RemoveGeneratedBookings(IEnumerable<CoOwnershipVehicle.Domain.Entities.Booking> bookings)
    {
        if (bookings == null)
        {
            throw new ArgumentNullException(nameof(bookings));
        }

        _context.Bookings.RemoveRange(bookings);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
