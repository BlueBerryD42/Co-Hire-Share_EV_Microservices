using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Booking.Api.DTOs;

public class UpdateVehicleStatusDto
{
    public VehicleStatus Status { get; set; }
}

public class UpdateTripSummaryDto
{
    public decimal DistanceKm { get; set; }
}
