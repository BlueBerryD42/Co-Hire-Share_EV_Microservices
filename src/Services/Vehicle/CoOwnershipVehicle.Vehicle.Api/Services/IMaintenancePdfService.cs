using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Vehicle.Api.Services;

public interface IMaintenancePdfService
{
    /// <summary>
    /// Generate PDF report for a maintenance record
    /// </summary>
    Task<byte[]> GenerateMaintenanceReportPdfAsync(Guid maintenanceRecordId);
}
