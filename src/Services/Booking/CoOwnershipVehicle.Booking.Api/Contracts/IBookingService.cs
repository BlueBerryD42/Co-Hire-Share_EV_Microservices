using CoOwnershipVehicle.Booking.Api.DTOs;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Shared.Contracts.DTOs;

namespace CoOwnershipVehicle.Booking.Api.Contracts;

public interface IBookingService
{
    Task<BookingDto> CreateBookingAsync(CreateBookingDto createDto, Guid userId, bool isEmergency = false, string? emergencyReason = null);
    Task<List<BookingDto>> GetUserBookingsAsync(Guid userId, DateTime? from = null, DateTime? to = null);
    Task<List<BookingDto>> GetVehicleBookingsAsync(Guid vehicleId, DateTime? from = null, DateTime? to = null);
    Task<List<AvailabilitySlotDto>> GetAvailabilityAsync(Guid vehicleId, DateTime from, DateTime to, int durationMinutes = 60, int bufferMinutes = 0);
    Task<List<BookingDto>> GetAllBookingsAsync(DateTime? from = null, DateTime? to = null, Guid? userId = null, Guid? groupId = null);
    Task<IReadOnlyList<BookingHistoryEntryDto>> GetUserBookingHistoryAsync(Guid userId, int limit = 20);
    Task<BookingConflictSummaryDto> CheckBookingConflictsAsync(Guid vehicleId, DateTime startAt, DateTime endAt, Guid? excludeBookingId = null);
    Task<List<BookingPriorityDto>> GetBookingPriorityQueueAsync(Guid vehicleId, DateTime startAt, DateTime endAt);
    Task<BookingDto> ApproveBookingAsync(Guid bookingId, Guid approverId);
    Task<BookingDto> CancelBookingAsync(Guid bookingId, Guid userId, string? reason = null);
    Task<List<BookingDto>> GetPendingApprovalsAsync(Guid userId);
    Task<BookingDto> UpdateVehicleStatusAsync(Guid bookingId, DTOs.UpdateVehicleStatusDto request);
    Task<BookingDto> UpdateTripSummaryAsync(Guid bookingId, UpdateTripSummaryDto request);
    Task<BookingDto> CompleteBookingAsync(Guid bookingId, Guid callerUserId, bool callerIsAdmin = false);
}