namespace CoOwnershipVehicle.Booking.Api.Storage;

public class StorageOptions
{
    public string RootPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "uploads");
    public string PublicBaseUrl { get; set; } = string.Empty;
    public string CheckInFolder { get; set; } = "checkins";
    public int ThumbnailSize { get; set; } = 300;
}
