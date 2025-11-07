using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using CoOwnershipVehicle.Admin.Api.Data;
using CoOwnershipVehicle.Admin.Api.Services;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Shared.Contracts.DTOs;

namespace CoOwnershipVehicle.Admin.Api.Tests.IntegrationTests;

public class WorkflowTests : IDisposable
{
    private readonly AdminDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly Mock<ILogger<AdminService>> _loggerMock;
    private readonly AdminService _adminService;
    private readonly DbContextOptions<AdminDbContext> _options;

    public WorkflowTests()
    {
        _options = new DbContextOptionsBuilder<AdminDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AdminDbContext(_options);
        _cache = new MemoryCache(new MemoryCacheOptions());
        _loggerMock = new Mock<ILogger<AdminService>>();

        _adminService = new AdminService(_context, _cache, _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
        _cache.Dispose();
    }

    #region User Suspension Workflow

    [Fact]
    public async Task UserSuspension_ShouldUpdateUserStatus()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "suspended@test.com",
            FirstName = "Suspended",
            LastName = "User",
            KycStatus = KycStatus.Approved,
            Role = UserRole.CoOwner,
            CreatedAt = DateTime.UtcNow,
            LockoutEnd = null
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@test.com",
            FirstName = "Admin",
            LastName = "User",
            KycStatus = KycStatus.Approved,
            Role = UserRole.SystemAdmin,
            CreatedAt = DateTime.UtcNow,
            LockoutEnd = null
        };
        _context.Users.Add(adminUser);
        await _context.SaveChangesAsync();

        var request = new UpdateUserStatusDto
        {
            Status = UserAccountStatus.Suspended,
            Reason = "Policy violation"
        };

        // Act
        var result = await _adminService.UpdateUserStatusAsync(user.Id, request, adminUser.Id);

        // Assert
        result.Should().BeTrue();
        var updatedUser = await _context.Users.FindAsync(user.Id);
        updatedUser!.LockoutEnd.Should().NotBeNull();
        updatedUser.LockoutEnd.Should().BeAfter(DateTime.UtcNow);

        // Verify audit log was created
        var auditLog = await _context.AuditLogs
            .FirstOrDefaultAsync(al => al.EntityId == user.Id && al.Action == "StatusUpdated");
        auditLog.Should().NotBeNull();
        auditLog!.PerformedBy.Should().Be(adminUser.Id);
        auditLog.Details.Should().Contain("Suspended");
    }

    [Fact]
    public async Task UserSuspension_ThenActivation_ShouldRestoreAccess()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "reactivate@test.com",
            FirstName = "Reactivate",
            LastName = "User",
            KycStatus = KycStatus.Approved,
            Role = UserRole.CoOwner,
            CreatedAt = DateTime.UtcNow,
            LockoutEnd = DateTime.UtcNow.AddYears(1) // Suspended
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@test.com",
            FirstName = "Admin",
            LastName = "User",
            KycStatus = KycStatus.Approved,
            Role = UserRole.SystemAdmin,
            CreatedAt = DateTime.UtcNow,
            LockoutEnd = null
        };
        _context.Users.Add(adminUser);
        await _context.SaveChangesAsync();

        var request = new UpdateUserStatusDto
        {
            Status = UserAccountStatus.Active,
            Reason = "Policy violation resolved"
        };

        // Act
        var result = await _adminService.UpdateUserStatusAsync(user.Id, request, adminUser.Id);

        // Assert
        result.Should().BeTrue();
        var updatedUser = await _context.Users.FindAsync(user.Id);
        updatedUser!.LockoutEnd.Should().BeNull();
    }

    #endregion

    #region Group Deactivation Workflow

    [Fact]
    public async Task GroupDeactivation_ShouldCancelFutureBookings()
    {
        // Arrange
        var group = new OwnershipGroup
        {
            Id = Guid.NewGuid(),
            Name = "Test Group",
            Status = GroupStatus.Active,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.OwnershipGroups.Add(group);
        await _context.SaveChangesAsync();

        var vehicle = new Vehicle
        {
            Id = Guid.NewGuid(),
            Vin = "TEST123456",
            PlateNumber = "TEST001",
            Model = "Test Model",
            Year = 2023,
            Status = VehicleStatus.Available,
            GroupId = group.Id,
            CreatedAt = DateTime.UtcNow
        };
        _context.Vehicles.Add(vehicle);
        await _context.SaveChangesAsync();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@test.com",
            FirstName = "Test",
            LastName = "User",
            KycStatus = KycStatus.Approved,
            Role = UserRole.CoOwner,
            CreatedAt = DateTime.UtcNow,
            LockoutEnd = null
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Create future bookings
        var futureBooking = new Booking
        {
            Id = Guid.NewGuid(),
            VehicleId = vehicle.Id,
            GroupId = group.Id,
            UserId = user.Id,
            StartAt = DateTime.UtcNow.AddDays(7),
            EndAt = DateTime.UtcNow.AddDays(10),
            Status = BookingStatus.Confirmed,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        _context.Bookings.Add(futureBooking);
        await _context.SaveChangesAsync();

        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@test.com",
            FirstName = "Admin",
            LastName = "User",
            KycStatus = KycStatus.Approved,
            Role = UserRole.SystemAdmin,
            CreatedAt = DateTime.UtcNow,
            LockoutEnd = null
        };
        _context.Users.Add(adminUser);
        await _context.SaveChangesAsync();

        var request = new UpdateGroupStatusDto
        {
            Status = GroupStatus.Inactive,
            Reason = "Maintenance period"
        };

        // Act
        var result = await _adminService.UpdateGroupStatusAsync(group.Id, request, adminUser.Id);

        // Assert
        result.Should().BeTrue();
        
        // Verify bookings were cancelled
        var cancelledBooking = await _context.Bookings.FindAsync(futureBooking.Id);
        cancelledBooking!.Status.Should().Be(BookingStatus.Cancelled);

        // Verify audit log
        var auditLog = await _context.AuditLogs
            .FirstOrDefaultAsync(al => al.EntityId == group.Id && al.Action == "StatusUpdated");
        auditLog.Should().NotBeNull();
    }

    #endregion

    #region Dispute Creation and Resolution Workflow

    [Fact]
    public async Task DisputeCreation_ShouldAutoAssignStaff()
    {
        // Arrange
        var group = new OwnershipGroup
        {
            Id = Guid.NewGuid(),
            Name = "Test Group",
            Status = GroupStatus.Active,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.OwnershipGroups.Add(group);
        await _context.SaveChangesAsync();

        var staffUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "staff@test.com",
            FirstName = "Staff",
            LastName = "User",
            KycStatus = KycStatus.Approved,
            Role = UserRole.Staff,
            CreatedAt = DateTime.UtcNow,
            LockoutEnd = null
        };
        _context.Users.Add(staffUser);
        await _context.SaveChangesAsync();

        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@test.com",
            FirstName = "Admin",
            LastName = "User",
            KycStatus = KycStatus.Approved,
            Role = UserRole.SystemAdmin,
            CreatedAt = DateTime.UtcNow,
            LockoutEnd = null
        };
        _context.Users.Add(adminUser);
        await _context.SaveChangesAsync();

        var request = new CreateDisputeDto
        {
            GroupId = group.Id,
            Subject = "Test Dispute",
            Description = "Test Description",
            Category = DisputeCategory.Financial,
            Priority = DisputePriority.High
        };

        // Act
        var disputeId = await _adminService.CreateDisputeAsync(request, adminUser.Id);

        // Assert
        disputeId.Should().NotBeEmpty();
        var dispute = await _context.Disputes.FindAsync(disputeId);
        dispute!.Should().NotBeNull();
        dispute.Status.Should().Be(DisputeStatus.Open);
        dispute.AssignedTo.Should().NotBeNull();

        // Verify audit log
        var auditLog = await _context.AuditLogs
            .FirstOrDefaultAsync(al => al.EntityId == disputeId && al.Action == "DisputeCreated");
        auditLog.Should().NotBeNull();
    }

    [Fact]
    public async Task DisputeResolution_ShouldUpdateStatusAndCreateAuditLog()
    {
        // Arrange
        var group = new OwnershipGroup
        {
            Id = Guid.NewGuid(),
            Name = "Test Group",
            Status = GroupStatus.Active,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.OwnershipGroups.Add(group);
        await _context.SaveChangesAsync();

        var staffUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "staff@test.com",
            FirstName = "Staff",
            LastName = "User",
            KycStatus = KycStatus.Approved,
            Role = UserRole.Staff,
            CreatedAt = DateTime.UtcNow,
            LockoutEnd = null
        };
        _context.Users.Add(staffUser);
        await _context.SaveChangesAsync();

        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@test.com",
            FirstName = "Admin",
            LastName = "User",
            KycStatus = KycStatus.Approved,
            Role = UserRole.SystemAdmin,
            CreatedAt = DateTime.UtcNow,
            LockoutEnd = null
        };
        _context.Users.Add(adminUser);
        await _context.SaveChangesAsync();

        var dispute = new Dispute
        {
            Id = Guid.NewGuid(),
            GroupId = group.Id,
            ReportedBy = adminUser.Id,
            Subject = "Test Dispute",
            Description = "Test Description",
            Category = DisputeCategory.Financial,
            Priority = DisputePriority.High,
            Status = DisputeStatus.UnderReview,
            AssignedTo = staffUser.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Disputes.Add(dispute);
        await _context.SaveChangesAsync();

        var request = new ResolveDisputeDto
        {
            Resolution = "Issue resolved",
            Note = "Compensation provided"
        };

        // Act
        var result = await _adminService.ResolveDisputeAsync(dispute.Id, request, staffUser.Id);

        // Assert
        result.Should().BeTrue();
        
        var updatedDispute = await _context.Disputes.FindAsync(dispute.Id);
        updatedDispute!.Status.Should().Be(DisputeStatus.Resolved);
        updatedDispute.Resolution.Should().Be("Issue resolved");
        updatedDispute.ResolvedBy.Should().Be(staffUser.Id);
        updatedDispute.ResolvedAt.Should().NotBeNull();
        updatedDispute.ResolvedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // Verify audit log
        var auditLog = await _context.AuditLogs
            .FirstOrDefaultAsync(al => al.EntityId == dispute.Id && al.Action == "DisputeResolved");
        auditLog.Should().NotBeNull();
        auditLog!.Details.Should().Contain("Issue resolved");
    }

    [Fact]
    public async Task DisputeAssignment_ShouldUpdateAssignedTo()
    {
        // Arrange
        var group = new OwnershipGroup
        {
            Id = Guid.NewGuid(),
            Name = "Test Group",
            Status = GroupStatus.Active,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.OwnershipGroups.Add(group);
        await _context.SaveChangesAsync();

        var staffUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "staff@test.com",
            FirstName = "Staff",
            LastName = "User",
            KycStatus = KycStatus.Approved,
            Role = UserRole.Staff,
            CreatedAt = DateTime.UtcNow,
            LockoutEnd = null
        };
        _context.Users.Add(staffUser);
        await _context.SaveChangesAsync();

        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@test.com",
            FirstName = "Admin",
            LastName = "User",
            KycStatus = KycStatus.Approved,
            Role = UserRole.SystemAdmin,
            CreatedAt = DateTime.UtcNow,
            LockoutEnd = null
        };
        _context.Users.Add(adminUser);
        await _context.SaveChangesAsync();

        var dispute = new Dispute
        {
            Id = Guid.NewGuid(),
            GroupId = group.Id,
            ReportedBy = adminUser.Id,
            Subject = "Test Dispute",
            Description = "Test Description",
            Category = DisputeCategory.Financial,
            Priority = DisputePriority.High,
            Status = DisputeStatus.Open,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Disputes.Add(dispute);
        await _context.SaveChangesAsync();

        var request = new AssignDisputeDto
        {
            AssignedTo = staffUser.Id,
            Note = "Assigning to staff member"
        };

        // Act
        var result = await _adminService.AssignDisputeAsync(dispute.Id, request, adminUser.Id);

        // Assert
        result.Should().BeTrue();
        
        var updatedDispute = await _context.Disputes.FindAsync(dispute.Id);
        updatedDispute!.AssignedTo.Should().Be(staffUser.Id);
        updatedDispute.Status.Should().Be(DisputeStatus.UnderReview);

        // Verify audit log
        var auditLog = await _context.AuditLogs
            .FirstOrDefaultAsync(al => al.EntityId == dispute.Id && al.Action == "DisputeAssigned");
        auditLog.Should().NotBeNull();
    }

    #endregion
}

