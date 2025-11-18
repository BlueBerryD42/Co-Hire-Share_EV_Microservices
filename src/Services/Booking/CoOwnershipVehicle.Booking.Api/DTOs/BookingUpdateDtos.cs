using System.Text.Json.Serialization;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Booking.Api.DTOs;

public class UpdateVehicleStatusDto
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public VehicleStatus Status { get; set; }
}

public class UpdateTripSummaryDto
{
    public decimal DistanceKm { get; set; }
}
