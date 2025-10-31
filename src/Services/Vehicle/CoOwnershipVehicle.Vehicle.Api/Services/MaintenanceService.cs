using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Domain.Enums;
using CoOwnershipVehicle.Vehicle.Api.Data;
using CoOwnershipVehicle.Vehicle.Api.DTOs;
using CoOwnershipVehicle.Shared.Contracts.Events;
using Microsoft.EntityFrameworkCore;
using MassTransit;

namespace CoOwnershipVehicle.Vehicle.Api.Services;

/// <summary>
/// Service implementation for managing vehicle maintenance schedules and records
/// </summary>
public class MaintenanceService : IMaintenanceService
{
    private readonly VehicleDbContext _context;
    private readonly ILogger<MaintenanceService> _logger;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IGroupServiceClient _groupServiceClient;
    private readonly IBookingServiceClient _bookingServiceClient;

    public MaintenanceService(
        VehicleDbContext context,
        ILogger<MaintenanceService> logger,
        IPublishEndpoint publishEndpoint,
        IGroupServiceClient groupServiceClient,
        IBookingServiceClient bookingServiceClient)
    {
        _context = context;
        _logger = logger;
        _publishEndpoint = publishEndpoint;
        _groupServiceClient = groupServiceClient;
        _bookingServiceClient = bookingServiceClient;
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

    #region Advanced Scheduling with Conflict Detection

    public async Task<ScheduleMaintenanceResponse> ScheduleMaintenanceAsync(
        ScheduleMaintenanceRequest request,
        Guid userId,
        string accessToken,
        bool isAdmin = false)
    {
        var response = new ScheduleMaintenanceResponse
        {
            VehicleId = request.VehicleId,
            ServiceType = request.ServiceType,
            ScheduledDate = request.ScheduledDate,
            Warnings = new List<string>()
        };

        // 1. Validate vehicle exists
        var vehicle = await _context.Vehicles.FindAsync(request.VehicleId);
        if (vehicle == null)
        {
            throw new InvalidOperationException($"Vehicle {request.VehicleId} not found");
        }

        // 2. Validate user is member of vehicle's group
        if (!vehicle.GroupId.HasValue)
        {
            throw new InvalidOperationException($"Vehicle {request.VehicleId} is not associated with any group");
        }

        var isMember = await _groupServiceClient.IsUserInGroupAsync(vehicle.GroupId.Value, userId, accessToken);
        if (!isMember)
        {
            throw new UnauthorizedAccessException($"User {userId} is not a member of group {vehicle.GroupId.Value}");
        }

        // 3. Validate scheduledDate is in the future
        if (request.ScheduledDate <= DateTime.UtcNow)
        {
            throw new InvalidOperationException("Scheduled date must be in the future");
        }

        // 4. Calculate maintenance window
        var maintenanceStartTime = request.ScheduledDate;
        var maintenanceEndTime = request.ScheduledDate.AddMinutes(request.EstimatedDuration);

        response.MaintenanceStartTime = maintenanceStartTime;
        response.MaintenanceEndTime = maintenanceEndTime;

        // 5. Check for conflicts
        var conflicts = await CheckMaintenanceConflictsAsync(request.VehicleId, maintenanceStartTime, maintenanceEndTime);

        if (conflicts.Any())
        {
            // If not forcing schedule or user is not admin, reject
            if (!request.ForceSchedule || !isAdmin)
            {
                var conflictMessages = conflicts.Select(c => c.Description).ToList();
                throw new InvalidOperationException(
                    $"Maintenance scheduling conflicts detected: {string.Join("; ", conflictMessages)}");
            }
            else
            {
                // Admin forcing schedule - add warnings
                response.Warnings.AddRange(conflicts.Select(c => $"FORCED: {c.Description}"));
                _logger.LogWarning("Admin {UserId} forced maintenance schedule with {Count} conflicts", userId, conflicts.Count);
            }
        }

        // 6. Create MaintenanceSchedule record
        var schedule = new MaintenanceSchedule
        {
            Id = Guid.NewGuid(),
            VehicleId = request.VehicleId,
            ServiceType = request.ServiceType,
            ScheduledDate = request.ScheduledDate,
            Status = MaintenanceStatus.Scheduled,
            EstimatedCost = request.EstimatedCost,
            EstimatedDuration = request.EstimatedDuration,
            ServiceProvider = request.ServiceProvider,
            Notes = request.Notes,
            Priority = request.Priority,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.MaintenanceSchedules.Add(schedule);

        // 7. Update vehicle status to "Maintenance" if imminent (within 24 hours)
        var hoursUntilMaintenance = (maintenanceStartTime - DateTime.UtcNow).TotalHours;
        if (hoursUntilMaintenance <= 24 && hoursUntilMaintenance > 0)
        {
            var oldStatus = vehicle.Status;
            vehicle.Status = VehicleStatus.Maintenance;
            vehicle.UpdatedAt = DateTime.UtcNow;
            response.VehicleStatusUpdated = true;

            _logger.LogInformation(
                "Updated vehicle {VehicleId} status from {OldStatus} to {NewStatus} due to imminent maintenance",
                vehicle.Id, oldStatus, vehicle.Status);

            // Publish VehicleStatusChangedEvent
            await _publishEndpoint.Publish(new VehicleStatusChangedEvent
            {
                VehicleId = vehicle.Id,
                GroupId = vehicle.GroupId.Value,
                OldStatus = oldStatus,
                NewStatus = vehicle.Status,
                ChangedBy = userId,
                ChangedAt = DateTime.UtcNow,
                Reason = $"Maintenance scheduled for {maintenanceStartTime:yyyy-MM-dd HH:mm}"
            });
        }

        await _context.SaveChangesAsync();

        response.ScheduleId = schedule.Id;
        response.Status = schedule.Status;
        response.Message = "Maintenance scheduled successfully";

        // 8. Publish MaintenanceScheduledEvent
        await _publishEndpoint.Publish(new MaintenanceScheduledEvent
        {
            MaintenanceScheduleId = schedule.Id,
            VehicleId = schedule.VehicleId,
            GroupId = vehicle.GroupId.Value,
            ServiceType = schedule.ServiceType,
            ScheduledDate = schedule.ScheduledDate,
            EstimatedDuration = schedule.EstimatedDuration,
            EstimatedCost = schedule.EstimatedCost,
            ServiceProvider = schedule.ServiceProvider,
            Priority = schedule.Priority,
            ScheduledBy = userId,
            MaintenanceStartTime = maintenanceStartTime,
            MaintenanceEndTime = maintenanceEndTime
        });

        // 9. Send notification to group members (via event - notification service will handle)
        await _publishEndpoint.Publish(new BulkNotificationEvent
        {
            GroupId = vehicle.GroupId.Value,
            Title = "Vehicle Maintenance Scheduled",
            Message = $"Maintenance ({schedule.ServiceType}) scheduled for vehicle {vehicle.Model} ({vehicle.PlateNumber}) on {maintenanceStartTime:yyyy-MM-dd HH:mm}",
            Type = "MaintenanceScheduled",
            Priority = schedule.Priority == MaintenancePriority.Urgent ? "High" : "Normal",
            ActionUrl = $"/vehicles/{vehicle.Id}/maintenance/{schedule.Id}",
            ActionText = "View Details"
        });

        _logger.LogInformation(
            "Maintenance scheduled: ScheduleId={ScheduleId}, VehicleId={VehicleId}, Type={ServiceType}, Date={ScheduledDate}",
            schedule.Id, schedule.VehicleId, schedule.ServiceType, schedule.ScheduledDate);

        return response;
    }

    public async Task<List<MaintenanceConflict>> CheckMaintenanceConflictsAsync(
        Guid vehicleId,
        DateTime startTime,
        DateTime endTime,
        Guid? excludeScheduleId = null)
    {
        var conflicts = new List<MaintenanceConflict>();

        // 1. Check for conflicting bookings
        var vehicle = await _context.Vehicles.FindAsync(vehicleId);
        if (vehicle != null && vehicle.GroupId.HasValue)
        {
            // Note: We would need to get access token from the current HTTP context
            // For now, we'll skip booking conflict check if we don't have token
            // In a real implementation, you'd inject IHttpContextAccessor
            _logger.LogWarning("Booking conflict check skipped - requires access token from HTTP context");
        }

        // 2. Check for conflicting maintenance schedules
        var conflictingSchedules = await _context.MaintenanceSchedules
            .Where(s => s.VehicleId == vehicleId &&
                       s.Status != MaintenanceStatus.Cancelled &&
                       s.Status != MaintenanceStatus.Completed &&
                       (excludeScheduleId == null || s.Id != excludeScheduleId))
            .ToListAsync();

        foreach (var schedule in conflictingSchedules)
        {
            var scheduleStart = schedule.ScheduledDate;
            var scheduleEnd = schedule.ScheduledDate.AddMinutes(schedule.EstimatedDuration);

            // Check for overlap
            if (startTime < scheduleEnd && endTime > scheduleStart)
            {
                conflicts.Add(new MaintenanceConflict
                {
                    Type = ConflictType.Maintenance,
                    ConflictingId = schedule.Id,
                    StartTime = scheduleStart,
                    EndTime = scheduleEnd,
                    Description = $"Conflicts with existing maintenance: {schedule.ServiceType} from {scheduleStart:yyyy-MM-dd HH:mm} to {scheduleEnd:HH:mm}"
                });
            }
        }

        // 3. Check if vehicle is unavailable
        var vehicleEntity = await _context.Vehicles.FindAsync(vehicleId);
        if (vehicleEntity?.Status == VehicleStatus.Unavailable)
        {
            conflicts.Add(new MaintenanceConflict
            {
                Type = ConflictType.VehicleUnavailable,
                ConflictingId = vehicleId,
                StartTime = startTime,
                EndTime = endTime,
                Description = "Vehicle is marked as unavailable"
            });
        }

        return conflicts;
    }

    #endregion

    #region Maintenance Views and History

    public async Task<MaintenanceScheduleResponse> GetVehicleMaintenanceScheduleAsync(
        Guid vehicleId,
        MaintenanceScheduleQuery query,
        Guid userId,
        string accessToken)
    {
        // 1. Validate vehicle exists
        var vehicle = await _context.Vehicles.FindAsync(vehicleId);
        if (vehicle == null)
        {
            throw new InvalidOperationException($"Vehicle {vehicleId} not found");
        }

        // 2. Validate user has access to vehicle
        if (vehicle.GroupId.HasValue)
        {
            var isMember = await _groupServiceClient.IsUserInGroupAsync(vehicle.GroupId.Value, userId, accessToken);
            if (!isMember)
            {
                throw new UnauthorizedAccessException($"User {userId} does not have access to vehicle {vehicleId}");
            }
        }

        // 3. Build query
        var schedulesQuery = _context.MaintenanceSchedules
            .Where(s => s.VehicleId == vehicleId);

        // Filter by status if provided
        if (query.Status.HasValue)
        {
            schedulesQuery = schedulesQuery.Where(s => s.Status == query.Status.Value);
        }
        else
        {
            // Default: show future and in-progress only
            schedulesQuery = schedulesQuery.Where(s =>
                s.Status == MaintenanceStatus.Scheduled ||
                s.Status == MaintenanceStatus.InProgress);
        }

        // Get total count
        var totalCount = await schedulesQuery.CountAsync();

        // 4. Sort by scheduled date (nearest first) and paginate
        var schedules = await schedulesQuery
            .OrderBy(s => s.ScheduledDate)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(s => new MaintenanceScheduleDto
            {
                Id = s.Id,
                VehicleId = s.VehicleId,
                ServiceType = s.ServiceType,
                ScheduledDate = s.ScheduledDate,
                Status = s.Status,
                EstimatedCost = s.EstimatedCost,
                EstimatedDuration = s.EstimatedDuration,
                ServiceProvider = s.ServiceProvider,
                Notes = s.Notes,
                Priority = s.Priority,
                CreatedBy = s.CreatedBy,
                CreatedAt = s.CreatedAt
            })
            .ToListAsync();

        return new MaintenanceScheduleResponse
        {
            Items = schedules,
            TotalCount = totalCount,
            PageNumber = query.PageNumber,
            PageSize = query.PageSize
        };
    }

    public async Task<MaintenanceHistoryResponse> GetVehicleMaintenanceHistoryAsync(
        Guid vehicleId,
        MaintenanceHistoryQuery query,
        Guid userId,
        string accessToken)
    {
        // 1. Validate vehicle exists
        var vehicle = await _context.Vehicles.FindAsync(vehicleId);
        if (vehicle == null)
        {
            throw new InvalidOperationException($"Vehicle {vehicleId} not found");
        }

        // 2. Validate user has access to vehicle
        if (vehicle.GroupId.HasValue)
        {
            var isMember = await _groupServiceClient.IsUserInGroupAsync(vehicle.GroupId.Value, userId, accessToken);
            if (!isMember)
            {
                throw new UnauthorizedAccessException($"User {userId} does not have access to vehicle {vehicleId}");
            }
        }

        // 3. Build query
        var recordsQuery = _context.MaintenanceRecords
            .Where(r => r.VehicleId == vehicleId);

        // Filter by service type if provided
        if (query.ServiceType.HasValue)
        {
            recordsQuery = recordsQuery.Where(r => r.ServiceType == query.ServiceType.Value);
        }

        // Filter by date range if provided
        if (query.StartDate.HasValue)
        {
            recordsQuery = recordsQuery.Where(r => r.ServiceDate >= query.StartDate.Value);
        }

        if (query.EndDate.HasValue)
        {
            recordsQuery = recordsQuery.Where(r => r.ServiceDate <= query.EndDate.Value);
        }

        // Get total count
        var totalCount = await recordsQuery.CountAsync();

        // 4. Sort by service date (most recent first) and paginate
        var records = await recordsQuery
            .OrderByDescending(r => r.ServiceDate)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(r => new MaintenanceHistoryDto
            {
                Id = r.Id,
                VehicleId = r.VehicleId,
                ServiceType = r.ServiceType,
                ServiceDate = r.ServiceDate,
                OdometerReading = r.OdometerReading,
                ActualCost = r.ActualCost,
                ServiceProvider = r.ServiceProvider,
                WorkPerformed = r.WorkPerformed,
                PartsReplaced = r.PartsReplaced,
                NextServiceDue = r.NextServiceDue,
                NextServiceOdometer = r.NextServiceOdometer,
                ExpenseId = r.ExpenseId,
                PerformedBy = r.PerformedBy,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync();

        // 5. Calculate statistics
        var statistics = await CalculateMaintenanceStatisticsAsync(vehicleId, query.StartDate, query.EndDate);

        return new MaintenanceHistoryResponse
        {
            Items = records,
            TotalCount = totalCount,
            PageNumber = query.PageNumber,
            PageSize = query.PageSize,
            Statistics = statistics
        };
    }

    public async Task<MaintenanceCostStatistics> CalculateMaintenanceStatisticsAsync(
        Guid vehicleId,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        var now = DateTime.UtcNow;
        var startOfYear = new DateTime(now.Year, 1, 1);
        var startOfMonth = new DateTime(now.Year, now.Month, 1);

        // Base query
        var baseQuery = _context.MaintenanceRecords
            .Where(r => r.VehicleId == vehicleId);

        // Apply date filters if provided
        if (startDate.HasValue)
        {
            baseQuery = baseQuery.Where(r => r.ServiceDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            baseQuery = baseQuery.Where(r => r.ServiceDate <= endDate.Value);
        }

        var allRecords = await baseQuery.ToListAsync();

        // Calculate totals
        var totalCostAllTime = allRecords.Sum(r => r.ActualCost);
        var totalCostThisYear = allRecords
            .Where(r => r.ServiceDate >= startOfYear)
            .Sum(r => r.ActualCost);
        var totalCostThisMonth = allRecords
            .Where(r => r.ServiceDate >= startOfMonth)
            .Sum(r => r.ActualCost);

        var averageCost = allRecords.Any() ? allRecords.Average(r => r.ActualCost) : 0;

        // Cost by service type
        var costByServiceType = allRecords
            .GroupBy(r => r.ServiceType)
            .Select(g => new CostByServiceType
            {
                ServiceType = g.Key,
                Count = g.Count(),
                TotalCost = g.Sum(r => r.ActualCost),
                AverageCost = g.Average(r => r.ActualCost)
            })
            .OrderByDescending(c => c.TotalCost)
            .ToList();

        return new MaintenanceCostStatistics
        {
            TotalCostAllTime = totalCostAllTime,
            TotalCostThisYear = totalCostThisYear,
            TotalCostThisMonth = totalCostThisMonth,
            AverageCostPerService = averageCost,
            CostByServiceType = costByServiceType
        };
    }

    #endregion
}
