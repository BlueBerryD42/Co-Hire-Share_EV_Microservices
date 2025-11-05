using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Booking.Api.DTOs;

public class BookingConflictSummaryDto
{
    public Guid VehicleId { get; set; }
    public DateTime RequestedStartAt { get; set; }
    public DateTime RequestedEndAt { get; set; }
    public bool HasConflicts { get; set; }
    public List<BookingDto> ConflictingBookings { get; set; } = new();
}

public class BookingPriorityDto
{
    public Guid BookingId { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public Guid VehicleId { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public BookingStatus Status { get; set; }
    public int Priority { get; set; }
    public bool IsEmergency { get; set; }
    public int PriorityScore { get; set; }
    public decimal OwnershipPercentage { get; set; }
}
public class BookingNotificationPreferenceDto
{
    public bool EnableReminders { get; set; }
    public bool EnableEmail { get; set; }
    public bool EnableSms { get; set; }
    public string? PreferredTimeZoneId { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class UpdateBookingNotificationPreferenceDto
{
    public bool? EnableReminders { get; set; }
    public bool? EnableEmail { get; set; }
    public bool? EnableSms { get; set; }
    public string? PreferredTimeZoneId { get; set; }
}
