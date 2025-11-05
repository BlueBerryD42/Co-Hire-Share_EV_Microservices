using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Shared.Contracts.DTOs;

public class RecurringBookingDto
{
    public Guid Id { get; set; }
    public Guid VehicleId { get; set; }
    public Guid GroupId { get; set; }
    public Guid UserId { get; set; }
    public RecurrencePattern Pattern { get; set; }
    public int Interval { get; set; }
    public IReadOnlyList<DayOfWeek> DaysOfWeek { get; set; } = Array.Empty<DayOfWeek>();
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public DateTime RecurrenceStartDate { get; set; }
    public DateTime? RecurrenceEndDate { get; set; }
    public RecurringBookingStatus Status { get; set; }
    public DateTime? PausedUntilUtc { get; set; }
    public DateTime? LastGeneratedUntilUtc { get; set; }
    public DateTime? LastGenerationRunAtUtc { get; set; }
    public string? Notes { get; set; }
    public string? Purpose { get; set; }
    public string? TimeZoneId { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CreateRecurringBookingDto
{
    [Required]
    public Guid VehicleId { get; set; }

    [Required]
    public Guid GroupId { get; set; } // Added GroupId

    [Required]
    public RecurrencePattern Pattern { get; set; }

    /// <summary>
    /// Optional interval multiplier (defaults to 1).
    /// </summary>
    [Range(1, 12)]
    public int Interval { get; set; } = 1;

    /// <summary>
    /// Required for weekly patterns.
    /// </summary>
    public List<DayOfWeek> DaysOfWeek { get; set; } = new();

    [Required]
    public TimeSpan StartTime { get; set; }

    [Required]
    public TimeSpan EndTime { get; set; }

    [Required]
    public DateTime RecurrenceStartDate { get; set; }

    public DateTime? RecurrenceEndDate { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    [StringLength(200)]
    public string? Purpose { get; set; }

    [StringLength(100)]
    public string? TimeZoneId { get; set; }
}

public class UpdateRecurringBookingDto
{
    public RecurrencePattern? Pattern { get; set; }

    [Range(1, 12)]
    public int? Interval { get; set; }

    public List<DayOfWeek>? DaysOfWeek { get; set; }

    public TimeSpan? StartTime { get; set; }

    public TimeSpan? EndTime { get; set; }

    public DateTime? RecurrenceStartDate { get; set; }

    public DateTime? RecurrenceEndDate { get; set; }

    public RecurringBookingStatus? Status { get; set; }

    public DateTime? PausedUntilUtc { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    [StringLength(200)]
    public string? Purpose { get; set; }

    [StringLength(100)]
    public string? TimeZoneId { get; set; }
}
