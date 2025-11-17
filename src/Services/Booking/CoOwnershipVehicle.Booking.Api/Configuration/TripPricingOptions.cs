namespace CoOwnershipVehicle.Booking.Api.Configuration;

public class TripPricingOptions
{
    public decimal CostPerKm { get; set; } = 1.5m;
    public decimal? MinimumFee { get; set; } = 0m;
}
