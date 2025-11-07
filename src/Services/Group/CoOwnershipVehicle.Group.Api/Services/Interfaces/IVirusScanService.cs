namespace CoOwnershipVehicle.Group.Api.Services.Interfaces;

public interface IVirusScanService
{
    Task<VirusScanResult> ScanFileAsync(Stream fileStream, string fileName);
}

public class VirusScanResult
{
    public bool IsClean { get; set; }
    public string? ThreatName { get; set; }
    public string ScanEngine { get; set; } = string.Empty;
    public DateTime ScannedAt { get; set; }
    public string? AdditionalInfo { get; set; }
}