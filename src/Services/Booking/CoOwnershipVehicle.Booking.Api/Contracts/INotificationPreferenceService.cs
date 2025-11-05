using System;
using System.Threading;
using System.Threading.Tasks;
using CoOwnershipVehicle.Booking.Api.DTOs;

namespace CoOwnershipVehicle.Booking.Api.Contracts;

public interface INotificationPreferenceService
{
    Task<BookingNotificationPreferenceDto> GetAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<BookingNotificationPreferenceDto> UpdateAsync(Guid userId, UpdateBookingNotificationPreferenceDto request, CancellationToken cancellationToken = default);
}
