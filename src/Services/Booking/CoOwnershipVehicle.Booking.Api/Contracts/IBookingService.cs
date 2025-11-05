using System;
using System.Collections.Generic;
using CoOwnershipVehicle.Booking.Api.DTOs;
using CoOwnershipVehicle.Shared.Contracts.DTOs;

namespace CoOwnershipVehicle.Booking.Api.Contracts;

public interface IBookingService
{
    Task<BookingDto> CreateBookingAsync(CreateBookingDto createDto, Guid userId, bool isEmergency = false, string? emergencyReason = null);
    Task<List<BookingDto>> GetUserBookingsAsync(Guid userId, DateTime? from = null, DateTime? to = null);
    Task<List<BookingDto>> GetVehicleBookingsAsync(Guid vehicleId, DateTime? from = null, DateTime? to = null);
    Task<BookingConflictSummaryDto> CheckBookingConflictsAsync(Guid vehicleId, DateTime startAt, DateTime endAt, Guid? excludeBookingId = null);
    Task<List<BookingPriorityDto>> GetBookingPriorityQueueAsync(Guid vehicleId, DateTime startAt, DateTime endAt);
    Task<BookingDto> ApproveBookingAsync(Guid bookingId, Guid approverId);
    Task<BookingDto> CancelBookingAsync(Guid bookingId, Guid userId, string? reason = null);
    Task<List<BookingDto>> GetPendingApprovalsAsync(Guid userId);
}
