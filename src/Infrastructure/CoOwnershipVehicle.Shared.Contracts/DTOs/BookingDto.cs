using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Shared.Contracts.DTOs;

public class BookingDto
{
    public Guid Id { get; set; }
    public Guid VehicleId { get; set; }
    public string VehicleModel { get; set; } = string.Empty;
    public string VehiclePlateNumber { get; set; } = string.Empty;
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string UserFirstName { get; set; } = string.Empty;
    public string UserLastName { get; set; } = string.Empty;
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public BookingStatus Status { get; set; }
    public decimal PriorityScore { get; set; }
    public string? Notes { get; set; }
    public string? Purpose { get; set; }
    public bool IsEmergency { get; set; }
    public BookingPriority Priority { get; set; }
    public bool RequiresDamageReview { get; set; }
    public Guid? RecurringBookingId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public VehicleStatus VehicleStatus { get; set; }
    public decimal? DistanceKm { get; set; }
    public decimal TripFeeAmount { get; set; }
}

public class CreateBookingDto
{
    [Required]
    public Guid VehicleId { get; set; }

    [Required]
    public DateTime StartAt { get; set; }

    [Required]
    public DateTime EndAt { get; set; }

    public string? Notes { get; set; }

    public string? Purpose { get; set; }

    public bool IsEmergency { get; set; } = false;

    [StringLength(1000)] // Added validation for EmergencyReason
    public string? EmergencyReason { get; set; } // Added EmergencyReason

    /// <summary>
    /// For emergency bookings, determines whether conflicting bookings should be auto-cancelled.
    /// </summary>
    public bool EmergencyAutoCancelConflicts { get; set; }

    public BookingPriority Priority { get; set; } = BookingPriority.Normal;

    // These will be set by the system
    public Guid UserId { get; set; }
    public Guid GroupId { get; set; }
}

public class UpdateBookingDto
{
    public DateTime? StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    public BookingStatus? Status { get; set; }
    public string? Notes { get; set; }
}

public class BookingConflictDto
{
    public Guid RequestedBookingId { get; set; }
    public List<BookingDto> ConflictingBookings { get; set; } = new();
    public BookingDto RecommendedBooking { get; set; } = null!;
    public string Resolution { get; set; } = string.Empty;
}

public class PriorityCalculationDto
{
    public Guid UserId { get; set; }
    public decimal OwnershipShare { get; set; }
    public decimal HistoricalUsage { get; set; }
    public int DaysSinceLastBooking { get; set; }
    public decimal CalculatedScore { get; set; }
}

public class BookingHistoryEntryDto
{
    public BookingDto Booking { get; set; } = new();
    public IReadOnlyList<CheckInDto> CheckIns { get; set; } = Array.Empty<CheckInDto>();
}


