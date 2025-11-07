namespace CoOwnershipVehicle.Vehicle.Api.DTOs;

/// <summary>
/// Vehicle information with health score summary for list view
/// </summary>
public class VehicleListItemDto
{
    public Guid Id { get; set; }
    public string Vin { get; set; } = string.Empty;
    public string PlateNumber { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Year { get; set; }
    public string? Color { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? LastServiceDate { get; set; }
    public int Odometer { get; set; }
    public Guid? GroupId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Health score summary (if available)
    /// </summary>
    public VehicleHealthSummary? HealthScore { get; set; }
}
