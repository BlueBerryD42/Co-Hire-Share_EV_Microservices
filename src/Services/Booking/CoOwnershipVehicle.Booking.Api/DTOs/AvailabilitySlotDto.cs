using System;

namespace CoOwnershipVehicle.Booking.Api.DTOs;

public sealed class AvailabilitySlotDto
{
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
}
