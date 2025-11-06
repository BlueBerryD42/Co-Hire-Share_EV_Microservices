using System;
using System.ComponentModel.DataAnnotations;

namespace CoOwnershipVehicle.Domain.Entities;

public class BookingTemplate : BaseEntity
{
    public Guid UserId { get; set; }

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

    public BookingPriority Priority { get; set; }

    public int UsageCount { get; set; }

    // Navigation properties
    public virtual User User { get; set; } = null!;
    public virtual Vehicle? Vehicle { get; set; }
}