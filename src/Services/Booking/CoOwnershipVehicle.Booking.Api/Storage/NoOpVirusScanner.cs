using Microsoft.Extensions.Logging;

namespace CoOwnershipVehicle.Booking.Api.Storage;

public class NoOpVirusScanner : IVirusScanner
{
    private readonly ILogger<NoOpVirusScanner> _logger;

    public NoOpVirusScanner(ILogger<NoOpVirusScanner> logger)
    {
        _logger = logger;
    }

    public Task ScanAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        if (!fileStream.CanSeek)
        {
            throw new InvalidOperationException("Virus scanner requires seekable stream.");
        }

        _logger.LogDebug("Virus scan passed for file {File}", fileName);
        fileStream.Seek(0, SeekOrigin.Begin);
        return Task.CompletedTask;
    }
}
