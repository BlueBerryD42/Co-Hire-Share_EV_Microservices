
using System;
using System.Collections.Generic;

namespace CoOwnershipVehicle.Vehicle.Api.DTOs
{
    public class BookingConflictDto
    {
        public Guid VehicleId { get; set; }
        public DateTime RequestedStartAt { get; set; }
        public DateTime RequestedEndAt { get; set; }
        public bool HasConflicts { get; set; }
        public List<BookingDto> ConflictingBookings { get; set; } = new();
    }

    public class BookingDto
    {
        public Guid Id { get; set; }
        public Guid VehicleId { get; set; }
        public Guid UserId { get; set; }
        public string UserFirstName { get; set; }
        public string UserLastName { get; set; }
        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }
        public BookingStatus Status { get; set; }
        public int Priority { get; set; }
        public bool IsEmergency { get; set; }
    }

    public enum BookingStatus
    {
        Pending = 0,
        PendingApproval = 1,
        Confirmed = 2,
        InProgress = 3,
        Completed = 4,
        Cancelled = 5,
        NoShow = 6
    }

    /// <summary>
    /// Booking statistics for a vehicle
    /// </summary>
    public class VehicleBookingStatistics
    {
        public Guid VehicleId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        // Completed bookings
        public List<CompletedBookingDto> CompletedBookings { get; set; } = new();

        // Summary metrics
        public int TotalBookings { get; set; }
        public int CompletedBookingsCount { get; set; }
        public int CancelledBookings { get; set; }
        public decimal TotalDistance { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalUsageHours { get; set; }
    }

    /// <summary>
    /// Completed booking with check-in/check-out data
    /// </summary>
    public class CompletedBookingDto
    {
        public Guid Id { get; set; }
        public Guid VehicleId { get; set; }
        public Guid UserId { get; set; }
        public string UserFirstName { get; set; } = string.Empty;
        public string UserLastName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }
        public DateTime? ActualStartAt { get; set; }
        public DateTime? ActualEndAt { get; set; }
        public int? CheckInOdometer { get; set; }
        public int? CheckOutOdometer { get; set; }
        public decimal? Distance { get; set; }
        public decimal? Cost { get; set; }
        public decimal UsageHours { get; set; }
    }
}
