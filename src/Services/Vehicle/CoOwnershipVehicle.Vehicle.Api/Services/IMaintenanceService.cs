using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Domain.Enums;

namespace CoOwnershipVehicle.Vehicle.Api.Services;

/// <summary>
/// Service interface for managing vehicle maintenance schedules and records
/// </summary>
public interface IMaintenanceService
{
    // MaintenanceSchedule operations
    Task<MaintenanceSchedule?> GetScheduleByIdAsync(Guid id);
    Task<IEnumerable<MaintenanceSchedule>> GetSchedulesByVehicleIdAsync(Guid vehicleId);
    Task<IEnumerable<MaintenanceSchedule>> GetSchedulesByStatusAsync(MaintenanceStatus status);
    Task<IEnumerable<MaintenanceSchedule>> GetOverdueSchedulesAsync();
    Task<MaintenanceSchedule> CreateScheduleAsync(MaintenanceSchedule schedule);
    Task<MaintenanceSchedule?> UpdateScheduleAsync(Guid id, MaintenanceSchedule schedule);
    Task<bool> DeleteScheduleAsync(Guid id);
    Task<bool> UpdateScheduleStatusAsync(Guid id, MaintenanceStatus status);

    // MaintenanceRecord operations
    Task<MaintenanceRecord?> GetRecordByIdAsync(Guid id);
    Task<IEnumerable<MaintenanceRecord>> GetRecordsByVehicleIdAsync(Guid vehicleId);
    Task<IEnumerable<MaintenanceRecord>> GetRecordsByServiceTypeAsync(Guid vehicleId, ServiceType serviceType);
    Task<MaintenanceRecord?> GetLatestRecordByTypeAsync(Guid vehicleId, ServiceType serviceType);
    Task<MaintenanceRecord> CreateRecordAsync(MaintenanceRecord record);
    Task<MaintenanceRecord?> UpdateRecordAsync(Guid id, MaintenanceRecord record);
    Task<bool> DeleteRecordAsync(Guid id);

    // Helper methods
    Task<decimal> GetTotalMaintenanceCostAsync(Guid vehicleId, DateTime? startDate = null, DateTime? endDate = null);
    Task<IEnumerable<MaintenanceRecord>> GetMaintenanceHistoryAsync(Guid vehicleId, int? limit = null);
}
