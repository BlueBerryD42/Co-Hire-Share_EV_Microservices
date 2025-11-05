using System.ComponentModel.DataAnnotations;

namespace CoOwnershipVehicle.Booking.Api.Configuration;

public class LateReturnFeeOptions
{
    public const string SectionName = "LateReturnFees";

    /// <summary>
    /// Minutes allowed before any late fee is applied.
    /// </summary>
    [Range(0, 240)]
    public int GracePeriodMinutes { get; set; } = 15;

    /// <summary>
    /// Maximum amount that can be charged for a single late return.
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal MaxFeeAmount { get; set; } = 200m;

    /// <summary>
    /// Optional default fee amount when no band matches.
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal DefaultHourlyRate { get; set; } = 0m;

    public bool EnableNotifications { get; set; } = true;

    public bool NotifyNextBookingHolder { get; set; } = true;

    public List<LateReturnFeeBand> Bands { get; set; } = new();
}

public class LateReturnFeeBand
{
    /// <summary>
    /// Inclusive lower bound for the lateness duration in minutes.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int FromMinutes { get; set; }

    /// <summary>
    /// Exclusive upper bound for the lateness duration in minutes. Null means no upper bound.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int? ToMinutes { get; set; }

    /// <summary>
    /// Hourly rate applied while in this band. Supports fractional hours.
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal RatePerHour { get; set; }

    /// <summary>
    /// Optional flat fee added when the band applies.
    /// </summary>
    [Range(0, double.MaxValue)]
    public decimal? FlatFee { get; set; }

    [StringLength(100)]
    public string? Label { get; set; }
}
