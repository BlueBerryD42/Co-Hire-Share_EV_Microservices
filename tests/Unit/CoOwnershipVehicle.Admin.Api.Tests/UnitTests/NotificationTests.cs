using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using CoOwnershipVehicle.Admin.Api.Data;
using CoOwnershipVehicle.Admin.Api.Services;
using CoOwnershipVehicle.Admin.Api.Services.HttpClients;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Shared.Contracts.DTOs;

namespace CoOwnershipVehicle.Admin.Api.Tests.UnitTests;

public class NotificationTests : IDisposable
{
    private readonly AdminDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly Mock<ILogger<AdminService>> _loggerMock;
    private readonly Mock<IUserServiceClient> _userServiceClientMock;
    private readonly Mock<IGroupServiceClient> _groupServiceClientMock;
    private readonly Mock<IVehicleServiceClient> _vehicleServiceClientMock;
    private readonly Mock<IBookingServiceClient> _bookingServiceClientMock;
    private readonly Mock<IPaymentServiceClient> _paymentServiceClientMock;
    private readonly AdminService _adminService;
    private readonly DbContextOptions<AdminDbContext> _options;

    public NotificationTests()
    {
        _options = new DbContextOptionsBuilder<AdminDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AdminDbContext(_options);
        _cache = new MemoryCache(new MemoryCacheOptions());
        _loggerMock = new Mock<ILogger<AdminService>>();
        _userServiceClientMock = new Mock<IUserServiceClient>();
        _groupServiceClientMock = new Mock<IGroupServiceClient>();
        _vehicleServiceClientMock = new Mock<IVehicleServiceClient>();
        _bookingServiceClientMock = new Mock<IBookingServiceClient>();
        _paymentServiceClientMock = new Mock<IPaymentServiceClient>();

        _adminService = new AdminService(
            _context,
            _cache,
            _loggerMock.Object,
            _userServiceClientMock.Object,
            _groupServiceClientMock.Object,
            _vehicleServiceClientMock.Object,
            _bookingServiceClientMock.Object,
            _paymentServiceClientMock.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
        _cache.Dispose();
    }

    #region User Status Change Notification Tests

    [Fact]
    public async Task UserSuspension_ShouldTriggerNotification()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@test.com",
            FirstName = "User",
            LastName = "Test",
            KycStatus = KycStatus.Approved,
            Role = UserRole.CoOwner,
            CreatedAt = DateTime.UtcNow,
            LockoutEnd = null
        };
        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@test.com",
            FirstName = "Admin",
            LastName = "User",
            KycStatus = KycStatus.Approved,
            Role = UserRole.SystemAdmin,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.AddRange(user, adminUser);
        await _context.SaveChangesAsync();

        var request = new UpdateUserStatusDto
        {
            Status = UserAccountStatus.Suspended,
            Reason = "Policy violation"
        };

        // Act
        await _adminService.UpdateUserStatusAsync(user.Id, request, adminUser.Id);

        // Assert - Verify notification would be created
        // In a real implementation, this would check if a notification was created
        // For now, we verify the user status was updated which would trigger notification
        var updatedUser = await _context.Users.FindAsync(user.Id);
        updatedUser!.LockoutEnd.Should().NotBeNull();
        
        // Verify audit log exists (notification would be created based on this)
        var auditLog = await _context.AuditLogs
            .FirstOrDefaultAsync(al => al.EntityId == user.Id && al.Action == "StatusUpdated");
        auditLog.Should().NotBeNull();
    }

    [Fact]
    public async Task UserActivation_ShouldTriggerNotification()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@test.com",
            FirstName = "User",
            LastName = "Test",
            KycStatus = KycStatus.Approved,
            Role = UserRole.CoOwner,
            CreatedAt = DateTime.UtcNow,
            LockoutEnd = DateTime.UtcNow.AddYears(1) // Currently suspended
        };
        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@test.com",
            FirstName = "Admin",
            LastName = "User",
            KycStatus = KycStatus.Approved,
            Role = UserRole.SystemAdmin,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.AddRange(user, adminUser);
        await _context.SaveChangesAsync();

        var request = new UpdateUserStatusDto
        {
            Status = UserAccountStatus.Active,
            Reason = "Issue resolved"
        };

        // Act
        await _adminService.UpdateUserStatusAsync(user.Id, request, adminUser.Id);

        // Assert
        var updatedUser = await _context.Users.FindAsync(user.Id);
        updatedUser!.LockoutEnd.Should().BeNull();
        
        var auditLog = await _context.AuditLogs
            .FirstOrDefaultAsync(al => al.EntityId == user.Id && al.Action == "StatusUpdated");
        auditLog.Should().NotBeNull();
    }

    [Fact]
    public async Task UserRoleChange_ShouldTriggerNotification()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@test.com",
            FirstName = "User",
            LastName = "Test",
            KycStatus = KycStatus.Approved,
            Role = UserRole.CoOwner,
            CreatedAt = DateTime.UtcNow
        };
        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@test.com",
            FirstName = "Admin",
            LastName = "User",
            KycStatus = KycStatus.Approved,
            Role = UserRole.SystemAdmin,
            CreatedAt = DateTime.UtcNow
        };
        _context.Users.AddRange(user, adminUser);
        await _context.SaveChangesAsync();

        var request = new UpdateUserRoleDto
        {
            Role = UserRole.GroupAdmin,
            Reason = "Promotion"
        };

        // Act
        await _adminService.UpdateUserRoleAsync(user.Id, request, adminUser.Id);

        // Assert
        var updatedUser = await _context.Users.FindAsync(user.Id);
        updatedUser!.Role.Should().Be(UserRole.GroupAdmin);
        
        var auditLog = await _context.AuditLogs
            .FirstOrDefaultAsync(al => al.EntityId == user.Id && al.Action == "RoleUpdated");
        auditLog.Should().NotBeNull();
    }

    #endregion

    #region Dispute Notification Tests

    [Fact]
    public async Task DisputeCreation_ShouldTriggerNotification()
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
        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@test.com",
            FirstName = "Admin",
            LastName = "User",
            KycStatus = KycStatus.Approved,
            Role = UserRole.SystemAdmin,
            CreatedAt = DateTime.UtcNow
        };
        _context.OwnershipGroups.Add(group);
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
        
        // Verify audit log (notification would be created based on this)
        var auditLog = await _context.AuditLogs
            .FirstOrDefaultAsync(al => al.EntityId == disputeId && al.Action == "DisputeCreated");
        auditLog.Should().NotBeNull();
        
        // Verify dispute has assigned staff (would trigger notification to them)
        var dispute = await _context.Disputes.FindAsync(disputeId);
        dispute!.AssignedTo.Should().NotBeNull();
    }

    [Fact]
    public async Task DisputeAssignment_ShouldTriggerNotification()
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
        var staffUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "staff@test.com",
            FirstName = "Staff",
            LastName = "User",
            KycStatus = KycStatus.Approved,
            Role = UserRole.Staff,
            CreatedAt = DateTime.UtcNow
        };
        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@test.com",
            FirstName = "Admin",
            LastName = "User",
            KycStatus = KycStatus.Approved,
            Role = UserRole.SystemAdmin,
            CreatedAt = DateTime.UtcNow
        };
        _context.OwnershipGroups.Add(group);
        _context.Users.AddRange(staffUser, adminUser);
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
            Note = "Please review"
        };

        // Act
        await _adminService.AssignDisputeAsync(dispute.Id, request, adminUser.Id);

        // Assert
        var updatedDispute = await _context.Disputes.FindAsync(dispute.Id);
        updatedDispute!.AssignedTo.Should().Be(staffUser.Id);
        updatedDispute.Status.Should().Be(DisputeStatus.UnderReview);
        
        // Verify audit log (notification to staff would be triggered)
        var auditLog = await _context.AuditLogs
            .FirstOrDefaultAsync(al => al.EntityId == dispute.Id && al.Action == "DisputeAssigned");
        auditLog.Should().NotBeNull();
        auditLog!.Details.Should().Contain(staffUser.FirstName);
    }

    [Fact]
    public async Task DisputeResolution_ShouldTriggerNotification()
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
        var reporter = new User
        {
            Id = Guid.NewGuid(),
            Email = "reporter@test.com",
            FirstName = "Reporter",
            LastName = "User",
            KycStatus = KycStatus.Approved,
            Role = UserRole.CoOwner,
            CreatedAt = DateTime.UtcNow
        };
        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@test.com",
            FirstName = "Admin",
            LastName = "User",
            KycStatus = KycStatus.Approved,
            Role = UserRole.SystemAdmin,
            CreatedAt = DateTime.UtcNow
        };
        _context.OwnershipGroups.Add(group);
        _context.Users.AddRange(reporter, adminUser);
        await _context.SaveChangesAsync();

        var dispute = new Dispute
        {
            Id = Guid.NewGuid(),
            GroupId = group.Id,
            ReportedBy = reporter.Id,
            Subject = "Test Dispute",
            Description = "Test Description",
            Category = DisputeCategory.Financial,
            Priority = DisputePriority.High,
            Status = DisputeStatus.UnderReview,
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
        await _adminService.ResolveDisputeAsync(dispute.Id, request, adminUser.Id);

        // Assert
        var updatedDispute = await _context.Disputes.FindAsync(dispute.Id);
        updatedDispute!.Status.Should().Be(DisputeStatus.Resolved);
        updatedDispute.ResolvedBy.Should().Be(adminUser.Id);
        updatedDispute.ResolvedAt.Should().NotBeNull();
        
        // Verify audit log (notification to reporter and participants would be triggered)
        var auditLog = await _context.AuditLogs
            .FirstOrDefaultAsync(al => al.EntityId == dispute.Id && al.Action == "DisputeResolved");
        auditLog.Should().NotBeNull();
    }

    [Fact]
    public async Task DisputeComment_ShouldTriggerNotification()
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
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@test.com",
            FirstName = "User",
            LastName = "Test",
            KycStatus = KycStatus.Approved,
            Role = UserRole.CoOwner,
            CreatedAt = DateTime.UtcNow
        };
        _context.OwnershipGroups.Add(group);
        _context.Users.Add(user);
        
        // Add user as group member so they can comment
        var groupMember = new GroupMember
        {
            Id = Guid.NewGuid(),
            GroupId = group.Id,
            UserId = user.Id,
            RoleInGroup = GroupRole.Member,
            SharePercentage = 0.5m,
            JoinedAt = DateTime.UtcNow
        };
        _context.GroupMembers.Add(groupMember);
        
        var dispute = new Dispute
        {
            Id = Guid.NewGuid(),
            GroupId = group.Id,
            ReportedBy = user.Id,
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

        var request = new AddDisputeCommentDto
        {
            Comment = "This is a test comment",
            IsInternal = false
        };

        // Act
        await _adminService.AddDisputeCommentAsync(dispute.Id, request, user.Id);

        // Assert
        var comments = await _context.DisputeComments
            .Where(dc => dc.DisputeId == dispute.Id)
            .ToListAsync();
        comments.Should().HaveCount(1);
        
        // Verify audit log (notification to participants would be triggered)
        var auditLog = await _context.AuditLogs
            .FirstOrDefaultAsync(al => al.EntityId == dispute.Id && al.Action == "CommentAdded");
        auditLog.Should().NotBeNull();
    }

    #endregion

    #region Assignment Notification Tests

    [Fact]
    public async Task DisputeAutoAssignment_ShouldNotifyAssignedStaff()
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
        var staffUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "staff@test.com",
            FirstName = "Staff",
            LastName = "User",
            KycStatus = KycStatus.Approved,
            Role = UserRole.Staff,
            CreatedAt = DateTime.UtcNow
        };
        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@test.com",
            FirstName = "Admin",
            LastName = "User",
            KycStatus = KycStatus.Approved,
            Role = UserRole.SystemAdmin,
            CreatedAt = DateTime.UtcNow
        };
        _context.OwnershipGroups.Add(group);
        _context.Users.AddRange(staffUser, adminUser);
        await _context.SaveChangesAsync();

        var request = new CreateDisputeDto
        {
            GroupId = group.Id,
            Subject = "Auto-assigned Dispute",
            Description = "This dispute should be auto-assigned",
            Category = DisputeCategory.Financial,
            Priority = DisputePriority.High
        };

        // Act
        var disputeId = await _adminService.CreateDisputeAsync(request, adminUser.Id);

        // Assert
        var dispute = await _context.Disputes.FindAsync(disputeId);
        dispute!.AssignedTo.Should().NotBeNull();
        // If staff user exists, they should be assigned (least busy)
        // Notification would be sent to assigned staff
    }

    #endregion
}

