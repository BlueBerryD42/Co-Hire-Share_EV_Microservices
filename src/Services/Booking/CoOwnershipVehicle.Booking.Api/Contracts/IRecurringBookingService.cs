using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CoOwnershipVehicle.Shared.Contracts.DTOs;

namespace CoOwnershipVehicle.Booking.Api.Contracts;

public interface IRecurringBookingService
{
    Task<RecurringBookingDto> CreateAsync(CreateRecurringBookingDto request, Guid userId, CancellationToken cancellationToken = default);

    Task<RecurringBookingDto> UpdateAsync(Guid recurringBookingId, UpdateRecurringBookingDto request, Guid userId, CancellationToken cancellationToken = default);

    Task CancelAsync(Guid recurringBookingId, Guid userId, string? reason = null, CancellationToken cancellationToken = default);

    Task<RecurringBookingDto> GetByIdAsync(Guid recurringBookingId, Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecurringBookingDto>> GetUserRecurringBookingsAsync(Guid userId, CancellationToken cancellationToken = default);
}
