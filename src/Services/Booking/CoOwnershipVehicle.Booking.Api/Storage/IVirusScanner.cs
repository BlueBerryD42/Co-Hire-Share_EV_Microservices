namespace CoOwnershipVehicle.Booking.Api.Storage;

public interface IVirusScanner
{
    Task ScanAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default);
}
