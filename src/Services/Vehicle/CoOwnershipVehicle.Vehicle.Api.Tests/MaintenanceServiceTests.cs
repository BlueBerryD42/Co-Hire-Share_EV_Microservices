using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Domain.Enums;
using CoOwnershipVehicle.Vehicle.Api.Data;
using CoOwnershipVehicle.Vehicle.Api.DTOs;
using CoOwnershipVehicle.Vehicle.Api.Services;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace CoOwnershipVehicle.Vehicle.Api.Tests;

/// <summary>
/// Unit tests for MaintenanceService
/// Tests conflict detection, overdue calculation, reschedule/cancel logic, and status transitions
/// </summary>
public class MaintenanceServiceTests : IDisposable
{
    private readonly VehicleDbContext _context;
    private readonly Mock<IPublishEndpoint> _mockPublishEndpoint;
    private readonly Mock<ILogger<MaintenanceService>> _mockLogger;
    private readonly Mock<IBookingServiceClient> _mockBookingClient;
    private readonly Mock<IGroupServiceClient> _mockGroupClient;
    private readonly MaintenanceService _service;

    public MaintenanceServiceTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<VehicleDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new VehicleDbContext(options);

        // Setup mocks
        _mockPublishEndpoint = new Mock<IPublishEndpoint>();
        _mockLogger = new Mock<ILogger<MaintenanceService>>();
        _mockBookingClient = new Mock<IBookingServiceClient>();
        _mockGroupClient = new Mock<IGroupServiceClient>();

        // Create service instance
        _service = new MaintenanceService(
            _context,
            _mockLogger.Object,
            _mockPublishEndpoint.Object,
            _mockGroupClient.Object,
            _mockBookingClient.Object
        );
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region Conflict Detection Tests

    [Fact]
    public async Task CheckMaintenanceConflictsAsync_NoConflicts_ReturnsEmptyList()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var vehicle = new Domain.Entities.Vehicle
        {
            Id = vehicleId,
            Vin = "TEST12345VIN67890",
            PlateNumber = "ABC-123",
            Model = "Tesla Model 3",
            Year = 2023,
            Status = VehicleStatus.Available,
            GroupId = Guid.NewGuid()
        };
        await _context.Vehicles.AddAsync(vehicle);
        await _context.SaveChangesAsync();

        var startTime = DateTime.UtcNow.AddDays(5);
        var endTime = startTime.AddHours(2);

        // Act
        var conflicts = await _service.CheckMaintenanceConflictsAsync(vehicleId, startTime, endTime);

        // Assert
        Assert.Empty(conflicts);
    }

    [Fact]
    public async Task CheckMaintenanceConflictsAsync_HasMaintenanceConflict_ReturnsConflict()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var vehicle = new Domain.Entities.Vehicle
        {
            Id = vehicleId,
            Vin = "TEST12345VIN67891",
            PlateNumber = "XYZ-789",
            Model = "Nissan Leaf",
            Year = 2022,
            Status = VehicleStatus.Available,
            GroupId = Guid.NewGuid()
        };
        await _context.Vehicles.AddAsync(vehicle);

        // Existing maintenance schedule
        var existingMaintenance = new MaintenanceSchedule
        {
            Id = Guid.NewGuid(),
            VehicleId = vehicleId,
            ServiceType = ServiceType.OilChange,
            ScheduledDate = DateTime.UtcNow.AddDays(5).AddHours(1),
            EstimatedDuration = 120,
            Status = MaintenanceStatus.Scheduled,
            Priority = MaintenancePriority.Medium,
            CreatedBy = Guid.NewGuid()
        };
        await _context.MaintenanceSchedules.AddAsync(existingMaintenance);
        await _context.SaveChangesAsync();

        var startTime = DateTime.UtcNow.AddDays(5);
        var endTime = startTime.AddHours(3);

        // Act
        var conflicts = await _service.CheckMaintenanceConflictsAsync(vehicleId, startTime, endTime);

        // Assert
        Assert.Single(conflicts);
        Assert.Equal(ConflictType.Maintenance, conflicts[0].Type);
        Assert.Equal(existingMaintenance.Id, conflicts[0].ConflictingId);
    }

    [Fact]
    public async Task CheckMaintenanceConflictsAsync_ExcludesSpecifiedSchedule_NoConflict()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var vehicle = new Domain.Entities.Vehicle
        {
            Id = vehicleId,
            Vin = "TEST12345VIN67892",
            PlateNumber = "DEF-456",
            Model = "Chevy Bolt",
            Year = 2021,
            Status = VehicleStatus.Available,
            GroupId = Guid.NewGuid()
        };
        await _context.Vehicles.AddAsync(vehicle);

        var existingScheduleId = Guid.NewGuid();
        var existingMaintenance = new MaintenanceSchedule
        {
            Id = existingScheduleId,
            VehicleId = vehicleId,
            ServiceType = ServiceType.TireRotation,
            ScheduledDate = DateTime.UtcNow.AddDays(5).AddHours(1),
            EstimatedDuration = 60,
            Status = MaintenanceStatus.Scheduled,
            Priority = MaintenancePriority.Low,
            CreatedBy = Guid.NewGuid()
        };
        await _context.MaintenanceSchedules.AddAsync(existingMaintenance);
        await _context.SaveChangesAsync();

        var startTime = DateTime.UtcNow.AddDays(5);
        var endTime = startTime.AddHours(3);

        // Act - exclude the existing schedule
        var conflicts = await _service.CheckMaintenanceConflictsAsync(
            vehicleId,
            startTime,
            endTime,
            existingScheduleId);

        // Assert
        Assert.Empty(conflicts);
    }

    #endregion

    #region Overdue Calculation Tests

    [Fact]
    public async Task GetOverdueMaintenanceAsync_NoOverdueItems_ReturnsEmptyList()
    {
        // Arrange - all maintenance in future
        var vehicleId = Guid.NewGuid();
        var vehicle = new Domain.Entities.Vehicle
        {
            Id = vehicleId,
            Vin = "FUTURE123VIN45678",
            PlateNumber = "FUT-123",
            Model = "Tesla Model Y",
            Year = 2024,
            Status = VehicleStatus.Available
        };
        await _context.Vehicles.AddAsync(vehicle);

        var futureSchedule = new MaintenanceSchedule
        {
            Id = Guid.NewGuid(),
            VehicleId = vehicleId,
            ServiceType = ServiceType.BatteryCheck,
            ScheduledDate = DateTime.UtcNow.AddDays(10),
            EstimatedDuration = 30,
            Status = MaintenanceStatus.Scheduled,
            Priority = MaintenancePriority.Low,
            CreatedBy = Guid.NewGuid(),
            Vehicle = vehicle
        };
        await _context.MaintenanceSchedules.AddAsync(futureSchedule);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetOverdueMaintenanceAsync();

        // Assert
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalOverdue);
        Assert.Equal(0, result.CriticalCount);
    }

    [Fact]
    public async Task GetOverdueMaintenanceAsync_HasOverdueItems_ReturnsCorrectList()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var vehicle = new Domain.Entities.Vehicle
        {
            Id = vehicleId,
            Vin = "OVERDUE123VIN678",
            PlateNumber = "OVD-123",
            Model = "BMW i4",
            Year = 2023,
            Status = VehicleStatus.Available
        };
        await _context.Vehicles.AddAsync(vehicle);

        // Overdue by 5 days
        var overdueSchedule = new MaintenanceSchedule
        {
            Id = Guid.NewGuid(),
            VehicleId = vehicleId,
            ServiceType = ServiceType.OilChange,
            ScheduledDate = DateTime.UtcNow.AddDays(-5),
            EstimatedDuration = 120,
            Status = MaintenanceStatus.Scheduled,
            Priority = MaintenancePriority.Medium,
            ServiceProvider = "Quick Service",
            EstimatedCost = 150.00m,
            CreatedBy = Guid.NewGuid(),
            Vehicle = vehicle
        };
        await _context.MaintenanceSchedules.AddAsync(overdueSchedule);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetOverdueMaintenanceAsync();

        // Assert
        Assert.Single(result.Items);
        Assert.Equal(1, result.TotalOverdue);

        var item = result.Items[0];
        Assert.Equal(overdueSchedule.Id, item.ScheduleId);
        Assert.True(item.DaysOverdue >= 4 && item.DaysOverdue <= 6); // Allow some time variance
        Assert.Equal(ServiceType.OilChange, item.ServiceType);
        Assert.Equal("OVD-123", item.PlateNumber);
    }

    [Fact]
    public async Task GetOverdueMaintenanceAsync_CriticalOverdue_FlaggedCorrectly()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var vehicle = new Domain.Entities.Vehicle
        {
            Id = vehicleId,
            Vin = "CRITICAL123VIN89",
            PlateNumber = "CRT-999",
            Model = "Audi e-tron",
            Year = 2022,
            Status = VehicleStatus.Available
        };
        await _context.Vehicles.AddAsync(vehicle);

        // Overdue by 35 days (should be critical)
        var criticalOverdueSchedule = new MaintenanceSchedule
        {
            Id = Guid.NewGuid(),
            VehicleId = vehicleId,
            ServiceType = ServiceType.BrakeInspection,
            ScheduledDate = DateTime.UtcNow.AddDays(-35),
            EstimatedDuration = 90,
            Status = MaintenanceStatus.Scheduled,
            Priority = MaintenancePriority.Medium,
            CreatedBy = Guid.NewGuid(),
            Vehicle = vehicle
        };

        // Urgent priority (also critical)
        var urgentSchedule = new MaintenanceSchedule
        {
            Id = Guid.NewGuid(),
            VehicleId = vehicleId,
            ServiceType = ServiceType.BatteryCheck,
            ScheduledDate = DateTime.UtcNow.AddDays(-10),
            EstimatedDuration = 60,
            Status = MaintenanceStatus.Scheduled,
            Priority = MaintenancePriority.Urgent,
            CreatedBy = Guid.NewGuid(),
            Vehicle = vehicle
        };

        await _context.MaintenanceSchedules.AddRangeAsync(criticalOverdueSchedule, urgentSchedule);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetOverdueMaintenanceAsync();

        // Assert
        Assert.Equal(2, result.TotalOverdue);
        Assert.Equal(2, result.CriticalCount);
        Assert.All(result.Items, item => Assert.True(item.IsCritical));
    }

    [Fact]
    public async Task GetOverdueMaintenanceAsync_CompletedMaintenance_NotIncluded()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var vehicle = new Domain.Entities.Vehicle
        {
            Id = vehicleId,
            Vin = "COMPLETED123VIN",
            PlateNumber = "CMP-001",
            Model = "Ford Mustang Mach-E",
            Year = 2023,
            Status = VehicleStatus.Available
        };
        await _context.Vehicles.AddAsync(vehicle);

        // Completed maintenance (should not appear in overdue)
        var completedSchedule = new MaintenanceSchedule
        {
            Id = Guid.NewGuid(),
            VehicleId = vehicleId,
            ServiceType = ServiceType.OilChange,
            ScheduledDate = DateTime.UtcNow.AddDays(-10),
            EstimatedDuration = 120,
            Status = MaintenanceStatus.Completed,
            Priority = MaintenancePriority.Medium,
            CreatedBy = Guid.NewGuid(),
            Vehicle = vehicle
        };
        await _context.MaintenanceSchedules.AddAsync(completedSchedule);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetOverdueMaintenanceAsync();

        // Assert
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalOverdue);
    }

    #endregion

    #region Upcoming Maintenance Tests

    [Fact]
    public async Task GetUpcomingMaintenanceAsync_DefaultDays_ReturnsNext30Days()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var vehicle = new Domain.Entities.Vehicle
        {
            Id = vehicleId,
            Vin = "UPCOMING123VIN01",
            PlateNumber = "UPC-100",
            Model = "Hyundai Ioniq 5",
            Year = 2024,
            Status = VehicleStatus.Available
        };
        await _context.Vehicles.AddAsync(vehicle);

        // Within 30 days
        var withinRange = new MaintenanceSchedule
        {
            Id = Guid.NewGuid(),
            VehicleId = vehicleId,
            ServiceType = ServiceType.TireRotation,
            ScheduledDate = DateTime.UtcNow.AddDays(15),
            EstimatedDuration = 60,
            Status = MaintenanceStatus.Scheduled,
            Priority = MaintenancePriority.Low,
            CreatedBy = Guid.NewGuid(),
            Vehicle = vehicle
        };

        // Outside 30 days
        var outsideRange = new MaintenanceSchedule
        {
            Id = Guid.NewGuid(),
            VehicleId = vehicleId,
            ServiceType = ServiceType.OilChange,
            ScheduledDate = DateTime.UtcNow.AddDays(40),
            EstimatedDuration = 120,
            Status = MaintenanceStatus.Scheduled,
            Priority = MaintenancePriority.Medium,
            CreatedBy = Guid.NewGuid(),
            Vehicle = vehicle
        };

        await _context.MaintenanceSchedules.AddRangeAsync(withinRange, outsideRange);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetUpcomingMaintenanceAsync();

        // Assert
        Assert.Equal(30, result.DaysAhead);
        Assert.Single(result.Vehicles);
        Assert.Single(result.Vehicles[0].MaintenanceItems);
        Assert.Equal(withinRange.Id, result.Vehicles[0].MaintenanceItems[0].ScheduleId);
    }

    [Fact]
    public async Task GetUpcomingMaintenanceAsync_FilterByPriority_ReturnsOnlyMatchingPriority()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var vehicle = new Domain.Entities.Vehicle
        {
            Id = vehicleId,
            Vin = "PRIORITY123VIN",
            PlateNumber = "PRI-200",
            Model = "Kia EV6",
            Year = 2023,
            Status = VehicleStatus.Available
        };
        await _context.Vehicles.AddAsync(vehicle);

        var highPriority = new MaintenanceSchedule
        {
            Id = Guid.NewGuid(),
            VehicleId = vehicleId,
            ServiceType = ServiceType.BrakeInspection,
            ScheduledDate = DateTime.UtcNow.AddDays(10),
            EstimatedDuration = 90,
            Status = MaintenanceStatus.Scheduled,
            Priority = MaintenancePriority.High,
            CreatedBy = Guid.NewGuid(),
            Vehicle = vehicle
        };

        var lowPriority = new MaintenanceSchedule
        {
            Id = Guid.NewGuid(),
            VehicleId = vehicleId,
            ServiceType = ServiceType.TireRotation,
            ScheduledDate = DateTime.UtcNow.AddDays(12),
            EstimatedDuration = 60,
            Status = MaintenanceStatus.Scheduled,
            Priority = MaintenancePriority.Low,
            CreatedBy = Guid.NewGuid(),
            Vehicle = vehicle
        };

        await _context.MaintenanceSchedules.AddRangeAsync(highPriority, lowPriority);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetUpcomingMaintenanceAsync(priority: MaintenancePriority.High);

        // Assert
        Assert.Single(result.Vehicles[0].MaintenanceItems);
        Assert.Equal(highPriority.Id, result.Vehicles[0].MaintenanceItems[0].ScheduleId);
        Assert.Equal(MaintenancePriority.High, result.Vehicles[0].MaintenanceItems[0].Priority);
    }

    [Fact]
    public async Task GetUpcomingMaintenanceAsync_GroupsByVehicle_Correctly()
    {
        // Arrange
        var vehicle1Id = Guid.NewGuid();
        var vehicle1 = new Domain.Entities.Vehicle
        {
            Id = vehicle1Id,
            Vin = "VEHICLE1VIN12345",
            PlateNumber = "V1-111",
            Model = "Tesla Model S",
            Year = 2023,
            Status = VehicleStatus.Available
        };

        var vehicle2Id = Guid.NewGuid();
        var vehicle2 = new Domain.Entities.Vehicle
        {
            Id = vehicle2Id,
            Vin = "VEHICLE2VIN67890",
            PlateNumber = "V2-222",
            Model = "Tesla Model X",
            Year = 2022,
            Status = VehicleStatus.Available
        };

        await _context.Vehicles.AddRangeAsync(vehicle1, vehicle2);

        var maintenance1 = new MaintenanceSchedule
        {
            Id = Guid.NewGuid(),
            VehicleId = vehicle1Id,
            ServiceType = ServiceType.OilChange,
            ScheduledDate = DateTime.UtcNow.AddDays(5),
            EstimatedDuration = 120,
            Status = MaintenanceStatus.Scheduled,
            Priority = MaintenancePriority.Medium,
            CreatedBy = Guid.NewGuid(),
            Vehicle = vehicle1
        };

        var maintenance2 = new MaintenanceSchedule
        {
            Id = Guid.NewGuid(),
            VehicleId = vehicle2Id,
            ServiceType = ServiceType.TireRotation,
            ScheduledDate = DateTime.UtcNow.AddDays(7),
            EstimatedDuration = 60,
            Status = MaintenanceStatus.Scheduled,
            Priority = MaintenancePriority.Low,
            CreatedBy = Guid.NewGuid(),
            Vehicle = vehicle2
        };

        await _context.MaintenanceSchedules.AddRangeAsync(maintenance1, maintenance2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetUpcomingMaintenanceAsync();

        // Assert
        Assert.Equal(2, result.Vehicles.Count);
        Assert.All(result.Vehicles, v => Assert.Single(v.MaintenanceItems));
    }

    #endregion

    #region Reschedule Tests

    [Fact]
    public async Task RescheduleMaintenanceAsync_ValidRequest_UpdatesScheduleCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        var vehicle = new Domain.Entities.Vehicle
        {
            Id = vehicleId,
            Vin = "RESCHEDULE123VIN",
            PlateNumber = "RSC-001",
            Model = "Porsche Taycan",
            Year = 2024,
            Status = VehicleStatus.Available,
            GroupId = groupId
        };
        await _context.Vehicles.AddAsync(vehicle);

        var originalDate = DateTime.UtcNow.AddDays(10);
        var schedule = new MaintenanceSchedule
        {
            Id = Guid.NewGuid(),
            VehicleId = vehicleId,
            ServiceType = ServiceType.BatteryCheck,
            ScheduledDate = originalDate,
            EstimatedDuration = 60,
            Status = MaintenanceStatus.Scheduled,
            Priority = MaintenancePriority.Medium,
            CreatedBy = userId,
            Vehicle = vehicle
        };
        await _context.MaintenanceSchedules.AddAsync(schedule);
        await _context.SaveChangesAsync();

        _mockGroupClient
            .Setup(x => x.IsUserInGroupAsync(groupId, userId, It.IsAny<string>()))
            .ReturnsAsync(true);

        var request = new RescheduleMaintenanceRequest
        {
            NewScheduledDate = DateTime.UtcNow.AddDays(15),
            Reason = "Service provider requested different date due to staffing",
            ForceReschedule = false
        };

        // Act
        var result = await _service.RescheduleMaintenanceAsync(
            schedule.Id,
            request,
            userId,
            "test-token",
            isAdmin: false);

        // Assert
        Assert.Equal(schedule.Id, result.ScheduleId);
        Assert.Equal(originalDate, result.OldScheduledDate);
        Assert.Equal(request.NewScheduledDate, result.NewScheduledDate);
        Assert.Equal(request.Reason, result.Reason);
        Assert.False(result.HasConflicts);

        // Verify database update
        var updatedSchedule = await _context.MaintenanceSchedules.FindAsync(schedule.Id);
        Assert.NotNull(updatedSchedule);
        Assert.Equal(request.NewScheduledDate, updatedSchedule.ScheduledDate);
        Assert.Equal(1, updatedSchedule.RescheduleCount);
        Assert.Equal(originalDate, updatedSchedule.OriginalScheduledDate);
        Assert.Equal(request.Reason, updatedSchedule.LastRescheduleReason);
        Assert.Equal(userId, updatedSchedule.LastRescheduledBy);
    }

    [Fact]
    public async Task RescheduleMaintenanceAsync_PastDate_ThrowsException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        var vehicle = new Domain.Entities.Vehicle
        {
            Id = vehicleId,
            Vin = "PASTDATE123VIN",
            PlateNumber = "PST-001",
            Model = "Rivian R1T",
            Year = 2023,
            Status = VehicleStatus.Available,
            GroupId = groupId
        };
        await _context.Vehicles.AddAsync(vehicle);

        var schedule = new MaintenanceSchedule
        {
            Id = Guid.NewGuid(),
            VehicleId = vehicleId,
            ServiceType = ServiceType.OilChange,
            ScheduledDate = DateTime.UtcNow.AddDays(10),
            EstimatedDuration = 120,
            Status = MaintenanceStatus.Scheduled,
            Priority = MaintenancePriority.Medium,
            CreatedBy = userId,
            Vehicle = vehicle
        };
        await _context.MaintenanceSchedules.AddAsync(schedule);
        await _context.SaveChangesAsync();

        _mockGroupClient
            .Setup(x => x.IsUserInGroupAsync(groupId, userId, It.IsAny<string>()))
            .ReturnsAsync(true);

        var request = new RescheduleMaintenanceRequest
        {
            NewScheduledDate = DateTime.UtcNow.AddDays(-5), // Past date
            Reason = "Trying to reschedule to past",
            ForceReschedule = false
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.RescheduleMaintenanceAsync(
                schedule.Id,
                request,
                userId,
                "test-token",
                isAdmin: false));
    }

    [Fact]
    public async Task RescheduleMaintenanceAsync_MultipleReschedules_IncrementsCount()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        var vehicle = new Domain.Entities.Vehicle
        {
            Id = vehicleId,
            Vin = "MULTIPLE123VIN",
            PlateNumber = "MLT-001",
            Model = "Mercedes EQS",
            Year = 2024,
            Status = VehicleStatus.Available,
            GroupId = groupId
        };
        await _context.Vehicles.AddAsync(vehicle);

        var originalDate = DateTime.UtcNow.AddDays(10);
        var schedule = new MaintenanceSchedule
        {
            Id = Guid.NewGuid(),
            VehicleId = vehicleId,
            ServiceType = ServiceType.BrakeInspection,
            ScheduledDate = originalDate,
            EstimatedDuration = 90,
            Status = MaintenanceStatus.Scheduled,
            Priority = MaintenancePriority.High,
            CreatedBy = userId,
            Vehicle = vehicle
        };
        await _context.MaintenanceSchedules.AddAsync(schedule);
        await _context.SaveChangesAsync();

        _mockGroupClient
            .Setup(x => x.IsUserInGroupAsync(groupId, userId, It.IsAny<string>()))
            .ReturnsAsync(true);

        // Act - First reschedule
        var request1 = new RescheduleMaintenanceRequest
        {
            NewScheduledDate = DateTime.UtcNow.AddDays(15),
            Reason = "First reschedule reason",
            ForceReschedule = false
        };
        await _service.RescheduleMaintenanceAsync(schedule.Id, request1, userId, "token", false);

        // Act - Second reschedule
        var request2 = new RescheduleMaintenanceRequest
        {
            NewScheduledDate = DateTime.UtcNow.AddDays(20),
            Reason = "Second reschedule reason",
            ForceReschedule = false
        };
        await _service.RescheduleMaintenanceAsync(schedule.Id, request2, userId, "token", false);

        // Assert
        var updatedSchedule = await _context.MaintenanceSchedules.FindAsync(schedule.Id);
        Assert.NotNull(updatedSchedule);
        Assert.Equal(2, updatedSchedule.RescheduleCount);
        Assert.Equal(originalDate, updatedSchedule.OriginalScheduledDate); // Original never changes
        Assert.Equal(request2.Reason, updatedSchedule.LastRescheduleReason);
    }

    #endregion

    #region Cancel Tests

    [Fact]
    public async Task CancelMaintenanceAsync_ValidRequest_UpdatesStatusCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        var vehicle = new Domain.Entities.Vehicle
        {
            Id = vehicleId,
            Vin = "CANCEL123VIN456",
            PlateNumber = "CXL-001",
            Model = "Lucid Air",
            Year = 2024,
            Status = VehicleStatus.Maintenance,
            GroupId = groupId
        };
        await _context.Vehicles.AddAsync(vehicle);

        var schedule = new MaintenanceSchedule
        {
            Id = Guid.NewGuid(),
            VehicleId = vehicleId,
            ServiceType = ServiceType.TireRotation,
            ScheduledDate = DateTime.UtcNow.AddDays(5),
            EstimatedDuration = 60,
            Status = MaintenanceStatus.Scheduled,
            Priority = MaintenancePriority.Low,
            CreatedBy = userId,
            Vehicle = vehicle
        };
        await _context.MaintenanceSchedules.AddAsync(schedule);
        await _context.SaveChangesAsync();

        _mockGroupClient
            .Setup(x => x.IsUserInGroupAsync(groupId, userId, It.IsAny<string>()))
            .ReturnsAsync(true);

        var request = new CancelMaintenanceRequest
        {
            CancellationReason = "Service provider is no longer available, will find alternative"
        };

        // Act
        var result = await _service.CancelMaintenanceAsync(
            schedule.Id,
            request,
            userId,
            "test-token",
            isAdmin: false);

        // Assert
        Assert.True(result);

        // Verify database update
        var updatedSchedule = await _context.MaintenanceSchedules.FindAsync(schedule.Id);
        Assert.NotNull(updatedSchedule);
        Assert.Equal(MaintenanceStatus.Cancelled, updatedSchedule.Status);
        Assert.Equal(request.CancellationReason, updatedSchedule.CancellationReason);
        Assert.Equal(userId, updatedSchedule.CancelledBy);

        // Verify vehicle status reverted
        var updatedVehicle = await _context.Vehicles.FindAsync(vehicleId);
        Assert.NotNull(updatedVehicle);
        Assert.Equal(VehicleStatus.Available, updatedVehicle.Status);
    }

    [Fact]
    public async Task CancelMaintenanceAsync_CompletedMaintenance_ThrowsException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        var vehicle = new Domain.Entities.Vehicle
        {
            Id = vehicleId,
            Vin = "COMPLETED123VIN",
            PlateNumber = "CPL-001",
            Model = "Polestar 2",
            Year = 2023,
            Status = VehicleStatus.Available,
            GroupId = groupId
        };
        await _context.Vehicles.AddAsync(vehicle);

        var schedule = new MaintenanceSchedule
        {
            Id = Guid.NewGuid(),
            VehicleId = vehicleId,
            ServiceType = ServiceType.OilChange,
            ScheduledDate = DateTime.UtcNow.AddDays(-5),
            EstimatedDuration = 120,
            Status = MaintenanceStatus.Completed,
            Priority = MaintenancePriority.Medium,
            CreatedBy = userId,
            Vehicle = vehicle
        };
        await _context.MaintenanceSchedules.AddAsync(schedule);
        await _context.SaveChangesAsync();

        _mockGroupClient
            .Setup(x => x.IsUserInGroupAsync(groupId, userId, It.IsAny<string>()))
            .ReturnsAsync(true);

        var request = new CancelMaintenanceRequest
        {
            CancellationReason = "Trying to cancel completed maintenance"
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CancelMaintenanceAsync(
                schedule.Id,
                request,
                userId,
                "test-token",
                isAdmin: false));
    }

    [Fact]
    public async Task CancelMaintenanceAsync_UnauthorizedUser_ThrowsException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();

        var vehicle = new Domain.Entities.Vehicle
        {
            Id = vehicleId,
            Vin = "UNAUTH123VIN789",
            PlateNumber = "UNA-001",
            Model = "Volkswagen ID.4",
            Year = 2023,
            Status = VehicleStatus.Available,
            GroupId = groupId
        };
        await _context.Vehicles.AddAsync(vehicle);

        var schedule = new MaintenanceSchedule
        {
            Id = Guid.NewGuid(),
            VehicleId = vehicleId,
            ServiceType = ServiceType.BatteryCheck,
            ScheduledDate = DateTime.UtcNow.AddDays(10),
            EstimatedDuration = 60,
            Status = MaintenanceStatus.Scheduled,
            Priority = MaintenancePriority.Medium,
            CreatedBy = Guid.NewGuid(),
            Vehicle = vehicle
        };
        await _context.MaintenanceSchedules.AddAsync(schedule);
        await _context.SaveChangesAsync();

        // User is NOT in group
        _mockGroupClient
            .Setup(x => x.IsUserInGroupAsync(groupId, userId, It.IsAny<string>()))
            .ReturnsAsync(false);

        var request = new CancelMaintenanceRequest
        {
            CancellationReason = "Unauthorized user trying to cancel"
        };

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.CancelMaintenanceAsync(
                schedule.Id,
                request,
                userId,
                "test-token",
                isAdmin: false));
    }

    #endregion
}
