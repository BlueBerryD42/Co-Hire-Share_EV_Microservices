using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CoOwnershipVehicle.Booking.Api.Data;
using CoOwnershipVehicle.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoOwnershipVehicle.Booking.Api.Repositories;

public class BookingRepository : IBookingRepository
{
    private readonly BookingDbContext _context;

    public BookingRepository(BookingDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public Task<bool> UserHasVehicleAccessAsync(Guid vehicleId, Guid userId, CancellationToken cancellationToken = default)
    {
        return _context.Vehicles
            .AsNoTracking()
            .Where(v => v.Id == vehicleId && v.Group != null)
            .AnyAsync(v => v.Group!.Members.Any(m => m.UserId == userId), cancellationToken);
    }

    public Task<bool> UserHasGroupAccessAsync(Guid userId, Guid groupId, CancellationToken cancellationToken = default)
    {
        return _context.GroupMembers
            .AsNoTracking()
            .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId, cancellationToken);
    }

    public Task<bool> IsGroupAdminAsync(Guid userId, Guid groupId, CancellationToken cancellationToken = default)
    {
        return _context.GroupMembers
            .AsNoTracking()
            .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId && gm.RoleInGroup == GroupRole.Admin, cancellationToken);
    }

    public async Task<IReadOnlyList<GroupMember>> GetGroupMembersAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        return await _context.GroupMembers
            .AsNoTracking()
            .Include(gm => gm.User)
            .Where(gm => gm.GroupId == groupId)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetEmergencyBookingCountForUserInMonthAsync(Guid userId, DateTime month, CancellationToken cancellationToken = default)
    {
        var startOfMonth = new DateTime(month.Year, month.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endOfMonth = startOfMonth.AddMonths(1).AddTicks(-1);

        return await _context.Bookings
            .AsNoTracking()
            .CountAsync(b => b.UserId == userId && b.IsEmergency && b.CreatedAt >= startOfMonth && b.CreatedAt <= endOfMonth, cancellationToken);
    }

    public async Task<IReadOnlyList<CoOwnershipVehicle.Domain.Entities.Booking>> GetBookingsInPeriodAsync(Guid vehicleId, DateTime startAt, DateTime endAt, CancellationToken cancellationToken = default)
    {
        return await _context.Bookings
            .Include(b => b.User)
            .Where(b => b.VehicleId == vehicleId &&
                        b.Status != Domain.Entities.BookingStatus.Cancelled &&
                        b.Status != Domain.Entities.BookingStatus.Completed &&
                        ((b.StartAt <= startAt && b.EndAt > startAt) ||
                         (b.StartAt < endAt && b.EndAt >= endAt) ||
                         (b.StartAt >= startAt && b.EndAt <= endAt)))
            .OrderBy(b => b.StartAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(CoOwnershipVehicle.Domain.Entities.Booking booking, CancellationToken cancellationToken = default)
    {
        if (booking == null)
        {
            throw new ArgumentNullException(nameof(booking));
        }

        await _context.Bookings.AddAsync(booking, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }

    public Task<List<CoOwnershipVehicle.Domain.Entities.Booking>> GetUserBookingsAsync(Guid userId, DateTime? from = null, DateTime? to = null)
    {
        var query = _context.Bookings
            .Include(b => b.Vehicle)
                .ThenInclude(v => v!.Group)
            .Include(b => b.User)
            .Where(b => b.UserId == userId);

        if (from.HasValue)
        {
            query = query.Where(b => b.EndAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(b => b.StartAt <= to.Value);
        }

        return query
            .OrderBy(b => b.StartAt)
            .ToListAsync();
    }

    public Task<List<CoOwnershipVehicle.Domain.Entities.Booking>> GetVehicleBookingsAsync(Guid vehicleId, DateTime? from = null, DateTime? to = null)
    {
        var query = _context.Bookings
            .Include(b => b.User)
            .Where(b => b.VehicleId == vehicleId && b.Status != Domain.Entities.BookingStatus.Cancelled);

        if (from.HasValue)
        {
            query = query.Where(b => b.EndAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(b => b.StartAt <= to.Value);
        }

        return query
            .OrderBy(b => b.StartAt)
            .ToListAsync();
    }

    public Task<List<CoOwnershipVehicle.Domain.Entities.Booking>> GetConflictingBookingsAsync(Guid vehicleId, DateTime startAt, DateTime endAt, Guid? excludeBookingId, Guid? excludeRecurringBookingId = null, CancellationToken cancellationToken = default)
    {
        return _context.Bookings
            .Include(b => b.User)
            .Where(b => b.VehicleId == vehicleId &&
                        b.Status != Domain.Entities.BookingStatus.Cancelled &&
                        b.Status != Domain.Entities.BookingStatus.Completed &&
                        (excludeBookingId == null || b.Id != excludeBookingId) &&
                        (excludeRecurringBookingId == null || b.RecurringBookingId != excludeRecurringBookingId) &&
                        ((b.StartAt <= startAt && b.EndAt > startAt) ||
                         (b.StartAt < endAt && b.EndAt >= endAt) ||
                         (b.StartAt >= startAt && b.EndAt <= endAt)))
            .OrderBy(b => b.StartAt)
            .ToListAsync(cancellationToken);
    }

    public Task<List<CoOwnershipVehicle.Domain.Entities.Booking>> GetBookingsForPriorityQueueAsync(Guid vehicleId, DateTime startAt, DateTime endAt, CancellationToken cancellationToken = default)
    {
        return _context.Bookings
            .Include(b => b.User)
            .Include(b => b.Vehicle)
                .ThenInclude(v => v!.Group)
                    .ThenInclude(g => g!.Members)
            .Where(b => b.VehicleId == vehicleId &&
                        b.Status != Domain.Entities.BookingStatus.Cancelled &&
                        b.Status != Domain.Entities.BookingStatus.Completed &&
                        ((b.StartAt <= startAt && b.EndAt > startAt) ||
                         (b.StartAt < endAt && b.EndAt >= endAt) ||
                         (b.StartAt >= startAt && b.EndAt <= endAt)))
            .ToListAsync(cancellationToken);
    }

    public Task<CoOwnershipVehicle.Domain.Entities.Booking?> GetBookingWithDetailsAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        return _context.Bookings
            .Include(b => b.Vehicle)
                .ThenInclude(v => v!.Group)
                    .ThenInclude(g => g!.Members)
            .Include(b => b.User)
            .Include(b => b.LateReturnFees)
                .ThenInclude(f => f.CheckIn)
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);
    }

    public Task<CoOwnershipVehicle.Domain.Entities.Booking?> GetBookingWithVehicleAndUserAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        return _context.Bookings
            .Include(b => b.Vehicle)
            .Include(b => b.User)
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);
    }

    public Task<CoOwnershipVehicle.Domain.Entities.Booking?> GetBookingAggregateAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        return _context.Bookings
            .Include(b => b.Vehicle)
            .Include(b => b.Group)
                .ThenInclude(g => g.Members)
            .Include(b => b.User)
            .Include(b => b.CheckIns)
                .ThenInclude(ci => ci.Photos)
            .Include(b => b.CheckIns)
                .ThenInclude(ci => ci.User)
            .Include(b => b.LateReturnFees)
                .ThenInclude(f => f.CheckIn)
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);
    }

    public async Task<IReadOnlyList<CoOwnershipVehicle.Domain.Entities.Booking>> GetBookingsForReminderProcessingAsync(
        DateTime preWindowStartUtc,
        DateTime preWindowEndUtc,
        DateTime finalWindowStartUtc,
        DateTime finalWindowEndUtc,
        DateTime missedWindowStartUtc,
        DateTime missedWindowEndUtc,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Bookings
            .Include(b => b.User)
            .Include(b => b.Vehicle)
            .Where(b => b.Status == BookingStatus.Confirmed &&
                        (
                            (b.PreCheckoutReminderSentAt == null && b.StartAt >= preWindowStartUtc && b.StartAt <= preWindowEndUtc) ||
                            (b.FinalCheckoutReminderSentAt == null && b.StartAt >= finalWindowStartUtc && b.StartAt <= finalWindowEndUtc) ||
                            (b.MissedCheckoutReminderSentAt == null && b.StartAt >= missedWindowStartUtc && b.StartAt <= missedWindowEndUtc)
                        ));

        return await query
            .OrderBy(b => b.StartAt)
            .ToListAsync(cancellationToken);
    }

    public Task<List<Guid>> GetAdminVehicleIdsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _context.Vehicles
            .Include(v => v.Group)
                .ThenInclude(g => g!.Members)
            .Where(v => v.Group != null &&
                        v.Group.Members.Any(m => m.UserId == userId && m.RoleInGroup == GroupRole.Admin))
            .Select(v => v.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<List<CoOwnershipVehicle.Domain.Entities.Booking>> GetPendingApprovalsAsync(IEnumerable<Guid> vehicleIds, CancellationToken cancellationToken = default)
    {
        var idSet = vehicleIds.ToHashSet();

        return _context.Bookings
            .Include(b => b.User)
            .Include(b => b.Vehicle)
            .Where(b => idSet.Contains(b.VehicleId) &&
                        b.Status == Domain.Entities.BookingStatus.PendingApproval)
            .OrderBy(b => b.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<GroupMember?> GetMemberForVehicleAsync(Guid userId, Guid vehicleId, CancellationToken cancellationToken = default)
    {
        return _context.GroupMembers
            .Include(m => m.Group)
                .ThenInclude(g => g.Vehicles)
            .FirstOrDefaultAsync(m => m.UserId == userId &&
                                      m.Group.Vehicles.Any(v => v.Id == vehicleId),
                                 cancellationToken);
    }

    public Task<Vehicle?> GetVehicleByIdAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        return _context.Vehicles
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == vehicleId, cancellationToken);
    }

    public Task<CoOwnershipVehicle.Domain.Entities.Booking?> GetBookingForCheckoutWindowAsync(Guid vehicleId, Guid userId, DateTime windowStart, DateTime windowEnd, CancellationToken cancellationToken = default)
    {
        return _context.Bookings
            .Include(b => b.Vehicle)
            .Include(b => b.Group)
            .Include(b => b.User)
            .Where(b => b.VehicleId == vehicleId &&
                        b.UserId == userId &&
                        (b.Status == Domain.Entities.BookingStatus.Confirmed || b.Status == Domain.Entities.BookingStatus.InProgress) &&
                        windowStart <= b.StartAt &&
                        b.StartAt <= windowEnd)
            .OrderBy(b => b.StartAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<CoOwnershipVehicle.Domain.Entities.Booking?> GetBookingForCheckinWindowAsync(Guid vehicleId, Guid userId, DateTime now, DateTime windowEnd, CancellationToken cancellationToken = default)
    {
        return _context.Bookings
            .Include(b => b.Vehicle)
            .Include(b => b.Group)
            .Include(b => b.User)
            .Where(b => b.VehicleId == vehicleId &&
                        b.UserId == userId &&
                        (b.Status == Domain.Entities.BookingStatus.InProgress || b.Status == Domain.Entities.BookingStatus.Confirmed) &&
                        b.StartAt <= windowEnd)
            .OrderByDescending(b => b.StartAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<CoOwnershipVehicle.Domain.Entities.Booking?> GetNextBookingAsync(Guid vehicleId, DateTime afterUtc, CancellationToken cancellationToken = default)
    {
        return _context.Bookings
            .Include(b => b.User)
            .Include(b => b.Group)
            .Include(b => b.Vehicle)
            .Where(b => b.VehicleId == vehicleId &&
                        b.Status != Domain.Entities.BookingStatus.Cancelled &&
                        b.Status != Domain.Entities.BookingStatus.Completed &&
                        b.StartAt >= afterUtc)
            .OrderBy(b => b.StartAt)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
