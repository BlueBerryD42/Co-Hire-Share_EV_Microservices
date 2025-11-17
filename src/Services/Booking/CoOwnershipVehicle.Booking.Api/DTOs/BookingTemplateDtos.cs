using System;
using System.ComponentModel.DataAnnotations;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Booking.Api.DTOs;

public class CreateBookingTemplateRequest
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public Guid? VehicleId { get; set; }

    [Required]
    public TimeSpan Duration { get; set; }

    [Required]
    public TimeSpan PreferredStartTime { get; set; }

    [StringLength(500)]
    public string? Purpose { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public BookingPriority Priority { get; set; } = BookingPriority.Normal;
}

public class UpdateBookingTemplateRequest
{
    [StringLength(100)]
    public string? Name { get; set; }

    public Guid? VehicleId { get; set; }

    public TimeSpan? Duration { get; set; }

    public TimeSpan? PreferredStartTime { get; set; }

    [StringLength(500)]
    public string? Purpose { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public BookingPriority? Priority { get; set; }
}

public class BookingTemplateResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? VehicleId { get; set; }
    public TimeSpan Duration { get; set; }
    public TimeSpan PreferredStartTime { get; set; }
    public string? Purpose { get; set; }
    public string? Notes { get; set; }
    public BookingPriority Priority { get; set; }
    public int UsageCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateBookingFromTemplateRequest
{
    [Required]
    public DateTime StartDateTime { get; set; }

    public Guid? VehicleId { get; set; }

    [Required]
    public Guid GroupId { get; set; }
}
