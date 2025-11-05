using System;
using System.ComponentModel.DataAnnotations;

namespace CoOwnershipVehicle.Booking.Api.Entities;

public class BookingNotificationPreference
{
    [Key]
    public Guid UserId { get; set; }

    public bool EnableReminders { get; set; } = true;

    public bool EnableEmail { get; set; } = true;

    public bool EnableSms { get; set; } = true;

    [StringLength(100)]
    public string? PreferredTimeZoneId { get; set; }

    [StringLength(250)]
    public string? Notes { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
