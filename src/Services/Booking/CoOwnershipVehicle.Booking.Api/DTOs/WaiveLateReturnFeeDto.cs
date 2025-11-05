using System.ComponentModel.DataAnnotations;

namespace CoOwnershipVehicle.Booking.Api.DTOs;

public class WaiveLateReturnFeeDto
{
    [StringLength(500)]
    public string? Reason { get; set; }
}
