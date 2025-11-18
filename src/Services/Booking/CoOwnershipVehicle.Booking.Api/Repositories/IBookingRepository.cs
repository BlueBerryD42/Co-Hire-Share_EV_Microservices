using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Booking.Api.Repositories;

public interface IBookingRepository
{
    Task<bool> UserHasVehicleAccessAsync(Guid vehicleId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> UserHasGroupAccessAsync(Guid userId, Guid groupId, CancellationToken cancellationToken = default);
    Task<int> GetEmergencyBookingCountForUserInMonthAsync(Guid userId, DateTime month, CancellationToken cancellationToken = default); // Added for emergency bookings
    Task<IReadOnlyList<CoOwnershipVehicle.Domain.Entities.Booking>> GetBookingsInPeriodAsync(Guid vehicleId, DateTime startAt, DateTime endAt, CancellationToken cancellationToken = default);
    Task AddAsync(CoOwnershipVehicle.Domain.Entities.Booking booking, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<List<CoOwnershipVehicle.Domain.Entities.Booking>> GetUserBookingsAsync(Guid userId, DateTime? from = null, DateTime? to = null);
    Task<List<CoOwnershipVehicle.Domain.Entities.Booking>> GetVehicleBookingsAsync(Guid vehicleId, DateTime? from = null, DateTime? to = null);
    Task<List<CoOwnershipVehicle.Domain.Entities.Booking>> GetConflictingBookingsAsync(Guid vehicleId, DateTime startAt, DateTime endAt, Guid? excludeBookingId, Guid? excludeRecurringBookingId = null, CancellationToken cancellationToken = default);
    Task<List<CoOwnershipVehicle.Domain.Entities.Booking>> GetBookingsForPriorityQueueAsync(Guid vehicleId, DateTime startAt, DateTime endAt, CancellationToken cancellationToken = default);
    Task<CoOwnershipVehicle.Domain.Entities.Booking?> GetBookingWithDetailsAsync(Guid bookingId, CancellationToken cancellationToken = default);
    Task<CoOwnershipVehicle.Domain.Entities.Booking?> GetBookingWithVehicleAndUserAsync(Guid bookingId, CancellationToken cancellationToken = default);
    Task<List<CoOwnershipVehicle.Domain.Entities.Booking>> GetUserBookingHistoryAsync(Guid userId, DateTime before, int limit, CancellationToken cancellationToken = default);
}
