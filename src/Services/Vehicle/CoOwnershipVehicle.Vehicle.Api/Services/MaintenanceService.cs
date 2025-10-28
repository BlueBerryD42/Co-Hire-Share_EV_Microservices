using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Domain.Enums;
using CoOwnershipVehicle.Vehicle.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CoOwnershipVehicle.Vehicle.Api.Services;

/// <summary>
/// Service implementation for managing vehicle maintenance schedules and records
/// </summary>
public class MaintenanceService : IMaintenanceService
{
    private readonly VehicleDbContext _context;
    private readonly ILogger<MaintenanceService> _logger;

    public MaintenanceService(VehicleDbContext context, ILogger<MaintenanceService> logger)
    {
        _context = context;
        _logger = logger;
    }

    #region MaintenanceSchedule Operations

    public async Task<MaintenanceSchedule?> GetScheduleByIdAsync(Guid id)
    {
        return await _context.MaintenanceSchedules
            .Include(s => s.Vehicle)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<IEnumerable<MaintenanceSchedule>> GetSchedulesByVehicleIdAsync(Guid vehicleId)
    {
        return await _context.MaintenanceSchedules
            .Where(s => s.VehicleId == vehicleId)
            .OrderBy(s => s.ScheduledDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MaintenanceSchedule>> GetSchedulesByStatusAsync(MaintenanceStatus status)
    {
        return await _context.MaintenanceSchedules
            .Where(s => s.Status == status)
            .OrderBy(s => s.ScheduledDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MaintenanceSchedule>> GetOverdueSchedulesAsync()
    {
        var now = DateTime.UtcNow;
        return await _context.MaintenanceSchedules
            .Where(s => s.ScheduledDate < now && s.Status == MaintenanceStatus.Scheduled)
            .OrderBy(s => s.ScheduledDate)
            .ToListAsync();
    }

    public async Task<MaintenanceSchedule> CreateScheduleAsync(MaintenanceSchedule schedule)
    {
        schedule.Id = Guid.NewGuid();
        schedule.CreatedAt = DateTime.UtcNow;
        schedule.UpdatedAt = DateTime.UtcNow;

        _context.MaintenanceSchedules.Add(schedule);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created maintenance schedule {ScheduleId} for vehicle {VehicleId}",
            schedule.Id, schedule.VehicleId);

        return schedule;
    }

    public async Task<MaintenanceSchedule?> UpdateScheduleAsync(Guid id, MaintenanceSchedule schedule)
    {
        var existing = await _context.MaintenanceSchedules.FindAsync(id);
        if (existing == null)
            return null;

        existing.ServiceType = schedule.ServiceType;
        existing.ScheduledDate = schedule.ScheduledDate;
        existing.Status = schedule.Status;
        existing.EstimatedCost = schedule.EstimatedCost;
        existing.EstimatedDuration = schedule.EstimatedDuration;
        existing.ServiceProvider = schedule.ServiceProvider;
        existing.Notes = schedule.Notes;
        existing.Priority = schedule.Priority;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated maintenance schedule {ScheduleId}", id);

        return existing;
    }

    public async Task<bool> DeleteScheduleAsync(Guid id)
    {
        var schedule = await _context.MaintenanceSchedules.FindAsync(id);
        if (schedule == null)
            return false;

        _context.MaintenanceSchedules.Remove(schedule);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted maintenance schedule {ScheduleId}", id);

        return true;
    }

    public async Task<bool> UpdateScheduleStatusAsync(Guid id, MaintenanceStatus status)
    {
        var schedule = await _context.MaintenanceSchedules.FindAsync(id);
        if (schedule == null)
            return false;

        schedule.Status = status;
        schedule.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated schedule {ScheduleId} status to {Status}", id, status);

        return true;
    }

    #endregion

    #region MaintenanceRecord Operations

    public async Task<MaintenanceRecord?> GetRecordByIdAsync(Guid id)
    {
        return await _context.MaintenanceRecords
            .Include(r => r.Vehicle)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<IEnumerable<MaintenanceRecord>> GetRecordsByVehicleIdAsync(Guid vehicleId)
    {
        return await _context.MaintenanceRecords
            .Where(r => r.VehicleId == vehicleId)
            .OrderByDescending(r => r.ServiceDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<MaintenanceRecord>> GetRecordsByServiceTypeAsync(Guid vehicleId, ServiceType serviceType)
    {
        return await _context.MaintenanceRecords
            .Where(r => r.VehicleId == vehicleId && r.ServiceType == serviceType)
            .OrderByDescending(r => r.ServiceDate)
            .ToListAsync();
    }

    public async Task<MaintenanceRecord?> GetLatestRecordByTypeAsync(Guid vehicleId, ServiceType serviceType)
    {
        return await _context.MaintenanceRecords
            .Where(r => r.VehicleId == vehicleId && r.ServiceType == serviceType)
            .OrderByDescending(r => r.ServiceDate)
            .FirstOrDefaultAsync();
    }

    public async Task<MaintenanceRecord> CreateRecordAsync(MaintenanceRecord record)
    {
        record.Id = Guid.NewGuid();
        record.CreatedAt = DateTime.UtcNow;
        record.UpdatedAt = DateTime.UtcNow;

        _context.MaintenanceRecords.Add(record);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created maintenance record {RecordId} for vehicle {VehicleId}",
            record.Id, record.VehicleId);

        return record;
    }

    public async Task<MaintenanceRecord?> UpdateRecordAsync(Guid id, MaintenanceRecord record)
    {
        var existing = await _context.MaintenanceRecords.FindAsync(id);
        if (existing == null)
            return null;

        existing.ServiceType = record.ServiceType;
        existing.ServiceDate = record.ServiceDate;
        existing.OdometerReading = record.OdometerReading;
        existing.ActualCost = record.ActualCost;
        existing.ServiceProvider = record.ServiceProvider;
        existing.WorkPerformed = record.WorkPerformed;
        existing.PartsReplaced = record.PartsReplaced;
        existing.NextServiceDue = record.NextServiceDue;
        existing.NextServiceOdometer = record.NextServiceOdometer;
        existing.ExpenseId = record.ExpenseId;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated maintenance record {RecordId}", id);

        return existing;
    }

    public async Task<bool> DeleteRecordAsync(Guid id)
    {
        var record = await _context.MaintenanceRecords.FindAsync(id);
        if (record == null)
            return false;

        _context.MaintenanceRecords.Remove(record);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted maintenance record {RecordId}", id);

        return true;
    }

    #endregion

    #region Helper Methods

    public async Task<decimal> GetTotalMaintenanceCostAsync(Guid vehicleId, DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _context.MaintenanceRecords.Where(r => r.VehicleId == vehicleId);

        if (startDate.HasValue)
            query = query.Where(r => r.ServiceDate >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(r => r.ServiceDate <= endDate.Value);

        return await query.SumAsync(r => r.ActualCost);
    }

    public async Task<IEnumerable<MaintenanceRecord>> GetMaintenanceHistoryAsync(Guid vehicleId, int? limit = null)
    {
        var query = _context.MaintenanceRecords
            .Where(r => r.VehicleId == vehicleId)
            .OrderByDescending(r => r.ServiceDate);

        if (limit.HasValue)
            query = (IOrderedQueryable<MaintenanceRecord>)query.Take(limit.Value);

        return await query.ToListAsync();
    }

    #endregion
}
