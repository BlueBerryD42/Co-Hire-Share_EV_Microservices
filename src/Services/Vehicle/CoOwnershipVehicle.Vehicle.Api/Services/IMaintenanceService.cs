using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Domain.Enums;
using CoOwnershipVehicle.Vehicle.Api.DTOs;

namespace CoOwnershipVehicle.Vehicle.Api.Services;

/// <summary>
/// Service interface for managing vehicle maintenance schedules and records
/// </summary>
public interface IMaintenanceService
{
    // MaintenanceSchedule operations
    Task<MaintenanceSchedule?> GetScheduleByIdAsync(Guid id);
    Task<IEnumerable<MaintenanceSchedule>> GetSchedulesByVehicleIdAsync(Guid vehicleId);
    Task<IEnumerable<MaintenanceSchedule>> GetSchedulesByStatusAsync(CoOwnershipVehicle.Domain.Enums.MaintenanceStatus status);
    Task<IEnumerable<MaintenanceSchedule>> GetOverdueSchedulesAsync();
    Task<MaintenanceSchedule> CreateScheduleAsync(MaintenanceSchedule schedule);
    Task<MaintenanceSchedule?> UpdateScheduleAsync(Guid id, MaintenanceSchedule schedule);
    Task<bool> DeleteScheduleAsync(Guid id);
    Task<bool> UpdateScheduleStatusAsync(Guid id, CoOwnershipVehicle.Domain.Enums.MaintenanceStatus status);

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

    // Advanced scheduling with conflict detection
    Task<ScheduleMaintenanceResponse> ScheduleMaintenanceAsync(ScheduleMaintenanceRequest request, Guid userId, string accessToken, bool isAdmin = false);
    Task<List<MaintenanceConflict>> CheckMaintenanceConflictsAsync(Guid vehicleId, DateTime startTime, DateTime endTime, Guid? excludeScheduleId = null);

    // Maintenance views and history
    Task<MaintenanceScheduleResponse> GetVehicleMaintenanceScheduleAsync(Guid vehicleId, MaintenanceScheduleQuery query, Guid userId, string accessToken);
    Task<MaintenanceHistoryResponse> GetVehicleMaintenanceHistoryAsync(Guid vehicleId, MaintenanceHistoryQuery query, Guid userId, string accessToken);
    Task<MaintenanceCostStatistics> CalculateMaintenanceStatisticsAsync(Guid vehicleId, DateTime? startDate = null, DateTime? endDate = null);

    // Complete maintenance
    Task<CompleteMaintenanceResponse> CompleteMaintenanceAsync(Guid scheduleId, CompleteMaintenanceRequest request, Guid userId, string accessToken, bool isAdmin = false);

    // Query upcoming and overdue maintenance
    Task<UpcomingMaintenanceResponse> GetUpcomingMaintenanceAsync(int days = 30, CoOwnershipVehicle.Domain.Enums.MaintenancePriority? priority = null, ServiceType? serviceType = null);
    Task<OverdueMaintenanceResponse> GetOverdueMaintenanceAsync();

    // Reschedule and cancel
    Task<RescheduleMaintenanceResponse> RescheduleMaintenanceAsync(Guid scheduleId, RescheduleMaintenanceRequest request, Guid userId, string accessToken, bool isAdmin = false);
    Task<bool> CancelMaintenanceAsync(Guid scheduleId, CancelMaintenanceRequest request, Guid userId, string accessToken, bool isAdmin = false);
}
