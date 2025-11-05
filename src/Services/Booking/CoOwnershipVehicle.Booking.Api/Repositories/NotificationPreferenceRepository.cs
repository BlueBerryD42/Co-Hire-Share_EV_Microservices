using System;
using System.Threading;
using System.Threading.Tasks;
using CoOwnershipVehicle.Booking.Api.Data;
using CoOwnershipVehicle.Booking.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoOwnershipVehicle.Booking.Api.Repositories;

public class NotificationPreferenceRepository : INotificationPreferenceRepository
{
    private readonly BookingDbContext _context;

    public NotificationPreferenceRepository(BookingDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public Task<BookingNotificationPreference?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _context.NotificationPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
    }

    public async Task UpsertAsync(BookingNotificationPreference preference, CancellationToken cancellationToken = default)
    {
        if (preference == null)
        {
            throw new ArgumentNullException(nameof(preference));
        }

        var existing = await _context.NotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == preference.UserId, cancellationToken);

        if (existing == null)
        {
            preference.UpdatedAt = DateTime.UtcNow;
            _context.NotificationPreferences.Add(preference);
        }
        else
        {
            existing.EnableReminders = preference.EnableReminders;
            existing.EnableEmail = preference.EnableEmail;
            existing.EnableSms = preference.EnableSms;
            existing.PreferredTimeZoneId = preference.PreferredTimeZoneId;
            existing.Notes = preference.Notes;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
