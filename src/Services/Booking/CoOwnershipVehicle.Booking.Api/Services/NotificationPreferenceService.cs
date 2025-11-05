using System;
using System.Threading;
using System.Threading.Tasks;
using CoOwnershipVehicle.Booking.Api.Contracts;
using CoOwnershipVehicle.Booking.Api.DTOs;
using CoOwnershipVehicle.Booking.Api.Entities;
using CoOwnershipVehicle.Booking.Api.Repositories;

namespace CoOwnershipVehicle.Booking.Api.Services;

public class NotificationPreferenceService : INotificationPreferenceService
{
    private readonly INotificationPreferenceRepository _repository;

    public NotificationPreferenceService(INotificationPreferenceRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<BookingNotificationPreferenceDto> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var preference = await _repository.GetByUserIdAsync(userId, cancellationToken);
        preference ??= CreateDefaultPreference(userId);

        return MapToDto(preference);
    }

    public async Task<BookingNotificationPreferenceDto> UpdateAsync(Guid userId, UpdateBookingNotificationPreferenceDto request, CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var preference = await _repository.GetByUserIdAsync(userId, cancellationToken) ?? CreateDefaultPreference(userId);

        if (request.EnableReminders.HasValue)
        {
            preference.EnableReminders = request.EnableReminders.Value;
        }

        if (request.EnableEmail.HasValue)
        {
            preference.EnableEmail = request.EnableEmail.Value;
        }

        if (request.EnableSms.HasValue)
        {
            preference.EnableSms = request.EnableSms.Value;
        }

        if (!string.IsNullOrWhiteSpace(request.PreferredTimeZoneId))
        {
            preference.PreferredTimeZoneId = NormalizeTimeZone(request.PreferredTimeZoneId);
        }
        else if (request.PreferredTimeZoneId == string.Empty)
        {
            preference.PreferredTimeZoneId = null;
        }

        preference.UpdatedAt = DateTime.UtcNow;

        await _repository.UpsertAsync(preference, cancellationToken);

        return MapToDto(preference);
    }

    private static BookingNotificationPreference CreateDefaultPreference(Guid userId) => new()
    {
        UserId = userId,
        EnableReminders = true,
        EnableEmail = true,
        EnableSms = true,
        PreferredTimeZoneId = null,
        UpdatedAt = DateTime.UtcNow
    };

    private static BookingNotificationPreferenceDto MapToDto(BookingNotificationPreference preference) => new()
    {
        EnableReminders = preference.EnableReminders,
        EnableEmail = preference.EnableEmail,
        EnableSms = preference.EnableSms,
        PreferredTimeZoneId = preference.PreferredTimeZoneId,
        UpdatedAt = preference.UpdatedAt
    };

    private static string? NormalizeTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return null;
        }

        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim());
            return tz.Id;
        }
        catch (TimeZoneNotFoundException)
        {
            return null;
        }
        catch (InvalidTimeZoneException)
        {
            return null;
        }
    }
}
