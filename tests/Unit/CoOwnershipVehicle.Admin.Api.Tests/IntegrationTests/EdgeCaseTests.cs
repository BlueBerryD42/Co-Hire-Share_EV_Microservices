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

namespace CoOwnershipVehicle.Admin.Api.Tests.IntegrationTests;

public class EdgeCaseTests : IDisposable
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

    public EdgeCaseTests()
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

    #region Empty Database Tests

    [Fact]
    public async Task GetDashboardMetrics_WithEmptyDatabase_ReturnsZeroMetrics()
    {
        // Arrange
        var request = new DashboardRequestDto { Period = TimePeriod.Monthly };

        // Act
        var result = await _adminService.GetDashboardMetricsAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Users.TotalUsers.Should().Be(0);
        result.Groups.TotalGroups.Should().Be(0);
        result.Vehicles.TotalVehicles.Should().Be(0);
        result.Bookings.TotalBookings.Should().Be(0);
    }

    [Fact]
    public async Task GetFinancialOverview_WithEmptyDatabase_ReturnsZeroRevenue()
    {
        // Act
        var result = await _adminService.GetFinancialOverviewAsync();

        // Assert
        result.Should().NotBeNull();
        result.TotalRevenueAllTime.Should().Be(0);
        result.TotalRevenueMonth.Should().Be(0);
        result.FinancialHealthScore.Should().BeInRange(0, 100);
    }

    [Fact]
    public async Task GetUsers_WithEmptyDatabase_ReturnsEmptyList()
    {
        // Arrange
        var request = new UserListRequestDto { Page = 1, PageSize = 20 };

        // Act
        var result = await _adminService.GetUsersAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Users.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetDisputeStatistics_WithEmptyDatabase_ReturnsZeroStats()
    {
        // Act
        var result = await _adminService.GetDisputeStatisticsAsync();

        // Assert
        result.Should().NotBeNull();
        result.TotalDisputes.Should().Be(0);
        result.OpenDisputes.Should().Be(0);
        result.ResolvedDisputes.Should().Be(0);
    }

    #endregion

    #region Large Dataset Tests

    [Fact]
    public async Task GetUsers_WithLargeDataset_HandlesPagination()
    {
        // Arrange - Create 50 users
        var users = Enumerable.Range(1, 50).Select(i => new User
        {
            Id = Guid.NewGuid(),
            Email = $"user{i}@test.com",
            FirstName = $"User{i}",
            LastName = "Test",
            KycStatus = KycStatus.Approved,
            Role = UserRole.CoOwner,
            CreatedAt = DateTime.UtcNow.AddDays(-i),
            LockoutEnd = null
        }).ToList();

        _context.Users.AddRange(users);
        await _context.SaveChangesAsync();

        var request = new UserListRequestDto
        {
            Page = 2,
            PageSize = 10
        };

        // Act
        var result = await _adminService.GetUsersAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(50);
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(10);
        result.Users.Should().HaveCount(10);
        result.TotalPages.Should().Be(5);
    }

    [Fact]
    public async Task GetGroups_WithLargeDataset_PerformsWell()
    {
        // Arrange - Create 100 groups
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

        var groups = Enumerable.Range(1, 100).Select(i => new OwnershipGroup
        {
            Id = Guid.NewGuid(),
            Name = $"Group {i}",
            Description = $"Test Group {i}",
            Status = i % 2 == 0 ? GroupStatus.Active : GroupStatus.Inactive,
            CreatedBy = adminUser.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-i),
            UpdatedAt = DateTime.UtcNow.AddDays(-i)
        }).ToList();

        _context.OwnershipGroups.AddRange(groups);
        await _context.SaveChangesAsync();

        var request = new GroupListRequestDto
        {
            Page = 1,
            PageSize = 20
        };

        // Act
        var result = await _adminService.GetGroupsAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(100);
        result.Groups.Should().HaveCount(20);
    }

    #endregion

    #region Concurrent Operations Tests

    [Fact]
    public async Task ConcurrentUserStatusUpdates_HandlesGracefully()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "concurrent@test.com",
            FirstName = "Concurrent",
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

        var request1 = new UpdateUserStatusDto
        {
            Status = UserAccountStatus.Suspended,
            Reason = "Reason 1"
        };
        var request2 = new UpdateUserStatusDto
        {
            Status = UserAccountStatus.Inactive,
            Reason = "Reason 2"
        };

        // Act - Simulate concurrent updates
        var task1 = _adminService.UpdateUserStatusAsync(user.Id, request1, adminUser.Id);
        var task2 = _adminService.UpdateUserStatusAsync(user.Id, request2, adminUser.Id);

        var results = await Task.WhenAll(task1, task2);

        // Assert - Both should succeed (last write wins)
        results[0].Should().BeTrue();
        results[1].Should().BeTrue();
    }

    [Fact]
    public async Task ConcurrentDisputeCreations_CreatesBothDisputes()
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

        var request1 = new CreateDisputeDto
        {
            GroupId = group.Id,
            Subject = "Dispute 1",
            Description = "Description 1",
            Category = DisputeCategory.Financial,
            Priority = DisputePriority.High
        };
        var request2 = new CreateDisputeDto
        {
            GroupId = group.Id,
            Subject = "Dispute 2",
            Description = "Description 2",
            Category = DisputeCategory.Usage,
            Priority = DisputePriority.Medium
        };

        // Act
        var task1 = _adminService.CreateDisputeAsync(request1, adminUser.Id);
        var task2 = _adminService.CreateDisputeAsync(request2, adminUser.Id);

        var id1 = await task1;
        var id2 = await task2;

        // Assert
        id1.Should().NotBeEmpty();
        id2.Should().NotBeEmpty();
        id1.Should().NotBe(id2);

        var disputes = await _context.Disputes
            .Where(d => d.GroupId == group.Id)
            .ToListAsync();
        disputes.Should().HaveCount(2);
    }

    #endregion

    #region Boundary Tests

    [Fact]
    public async Task GetUserDetails_WithLastPage_ReturnsRemainingItems()
    {
        // Arrange - Create 15 users
        var users = Enumerable.Range(1, 15).Select(i => new User
        {
            Id = Guid.NewGuid(),
            Email = $"user{i}@test.com",
            FirstName = $"User{i}",
            LastName = "Test",
            KycStatus = KycStatus.Approved,
            Role = UserRole.CoOwner,
            CreatedAt = DateTime.UtcNow.AddDays(-i),
            LockoutEnd = null
        }).ToList();

        _context.Users.AddRange(users);
        await _context.SaveChangesAsync();

        var request = new UserListRequestDto
        {
            Page = 2,
            PageSize = 10
        };

        // Act
        var result = await _adminService.GetUsersAsync(request);

        // Assert
        result.Users.Should().HaveCount(5);
        result.TotalPages.Should().Be(2);
    }

    [Fact]
    public async Task GetUsers_WithPageBeyondTotalPages_ReturnsEmptyList()
    {
        // Arrange
        var users = Enumerable.Range(1, 10).Select(i => new User
        {
            Id = Guid.NewGuid(),
            Email = $"user{i}@test.com",
            FirstName = $"User{i}",
            LastName = "Test",
            KycStatus = KycStatus.Approved,
            Role = UserRole.CoOwner,
            CreatedAt = DateTime.UtcNow.AddDays(-i),
            LockoutEnd = null
        }).ToList();

        _context.Users.AddRange(users);
        await _context.SaveChangesAsync();

        var request = new UserListRequestDto
        {
            Page = 5,
            PageSize = 10
        };

        // Act
        var result = await _adminService.GetUsersAsync(request);

        // Assert
        result.Users.Should().BeEmpty();
        result.TotalCount.Should().Be(10);
    }

    [Fact]
    public async Task SearchUsers_WithSpecialCharacters_HandlesGracefully()
    {
        // Arrange
        var users = new List<User>
        {
            new User
            {
                Id = Guid.NewGuid(),
                Email = "special@test.com",
                FirstName = "O'Brien",
                LastName = "McDonald-Smith",
                KycStatus = KycStatus.Approved,
                Role = UserRole.CoOwner,
                CreatedAt = DateTime.UtcNow,
                LockoutEnd = null
            }
        };

        _context.Users.AddRange(users);
        await _context.SaveChangesAsync();

        var request = new UserListRequestDto
        {
            Page = 1,
            PageSize = 10,
            Search = "O'Brien"
        };

        // Act
        var result = await _adminService.GetUsersAsync(request);

        // Assert
        result.Users.Should().HaveCount(1);
        result.Users.First().FirstName.Should().Contain("O'Brien");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task UpdateUserStatus_WithNonExistentUser_ReturnsFalse()
    {
        // Arrange
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
            Reason = "Test"
        };

        // Act
        var result = await _adminService.UpdateUserStatusAsync(Guid.NewGuid(), request, adminUser.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetUserDetails_WithInvalidId_ThrowsArgumentException()
    {
        // Act
        var act = async () => await _adminService.GetUserDetailsAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("User not found");
    }

    [Fact]
    public async Task CreateDispute_WithInvalidGroup_ThrowsArgumentException()
    {
        // Arrange
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
            GroupId = Guid.NewGuid(),
            Subject = "Test",
            Description = "Test",
            Category = DisputeCategory.Other,
            Priority = DisputePriority.Medium
        };

        // Act
        var act = async () => await _adminService.CreateDisputeAsync(request, adminUser.Id);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Group not found");
    }

    #endregion

    #region Very Large Dataset Tests (10000+ users)

    [Fact]
    public async Task GetUsers_WithVeryLargeDataset_HandlesPaginationEfficiently()
    {
        // Arrange - Create 10000+ users (using batch approach for performance)
        var batchSize = 1000;
        var totalUsers = 10000;

        for (int batch = 0; batch < totalUsers / batchSize; batch++)
        {
            var users = Enumerable.Range(batch * batchSize + 1, batchSize).Select(i => new User
            {
                Id = Guid.NewGuid(),
                Email = $"user{i}@test.com",
                FirstName = $"User{i}",
                LastName = "Test",
                KycStatus = i % 3 == 0 ? KycStatus.Approved : (i % 3 == 1 ? KycStatus.Pending : KycStatus.Rejected),
                Role = UserRole.CoOwner,
                CreatedAt = DateTime.UtcNow.AddDays(-i % 365),
                LockoutEnd = i % 10 == 0 ? DateTime.UtcNow.AddYears(1) : null
            }).ToList();

            _context.Users.AddRange(users);
            await _context.SaveChangesAsync();
        }

        var request = new UserListRequestDto
        {
            Page = 1,
            PageSize = 20
        };

        // Act
        var startTime = DateTime.UtcNow;
        var result = await _adminService.GetUsersAsync(request);
        var endTime = DateTime.UtcNow;

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(totalUsers);
        result.Users.Should().HaveCount(20);
        result.TotalPages.Should().Be(500);
        
        // Performance check - should complete in reasonable time
        var duration = (endTime - startTime).TotalSeconds;
        duration.Should().BeLessThan(10); // Should complete in under 10 seconds even with 10k users
    }

    [Fact]
    public async Task GetDashboardMetrics_WithVeryLargeDataset_CalculatesAccurately()
    {
        // Arrange - Create large dataset
        var adminUser = new User { Id = Guid.NewGuid(), Email = "admin@test.com", FirstName = "Admin", LastName = "User", KycStatus = KycStatus.Approved, Role = UserRole.SystemAdmin, CreatedAt = DateTime.UtcNow };
        _context.Users.Add(adminUser);
        await _context.SaveChangesAsync();

        // Create 5000 users
        var users = Enumerable.Range(1, 5000).Select(i => new User
        {
            Id = Guid.NewGuid(),
            Email = $"user{i}@test.com",
            FirstName = $"User{i}",
            LastName = "Test",
            KycStatus = i % 2 == 0 ? KycStatus.Approved : KycStatus.Pending,
            Role = UserRole.CoOwner,
            CreatedAt = DateTime.UtcNow.AddDays(-i % 365),
            LockoutEnd = null
        }).ToList();
        _context.Users.AddRange(users);
        await _context.SaveChangesAsync();

        // Create 1000 groups
        var groups = Enumerable.Range(1, 1000).Select(i => new OwnershipGroup
        {
            Id = Guid.NewGuid(),
            Name = $"Group {i}",
            Status = i % 3 == 0 ? GroupStatus.Active : (i % 3 == 1 ? GroupStatus.Inactive : GroupStatus.Dissolved),
            CreatedBy = adminUser.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-i % 365),
            UpdatedAt = DateTime.UtcNow.AddDays(-i % 365)
        }).ToList();
        _context.OwnershipGroups.AddRange(groups);
        await _context.SaveChangesAsync();

        // Act
        var startTime = DateTime.UtcNow;
        var result = await _adminService.GetDashboardMetricsAsync(new DashboardRequestDto { Period = TimePeriod.Monthly });
        var endTime = DateTime.UtcNow;

        // Assert
        result.Should().NotBeNull();
        result.Users.TotalUsers.Should().Be(5001); // 5000 + 1 admin
        result.Groups.TotalGroups.Should().Be(1000);
        
        // Performance check
        var duration = (endTime - startTime).TotalSeconds;
        duration.Should().BeLessThan(15); // Should complete in under 15 seconds
    }

    [Fact]
    public async Task GetFinancialOverview_WithLargeDataset_CalculatesCorrectly()
    {
        // Arrange - Create 5000 payments
        var payments = Enumerable.Range(1, 5000).Select(i => new Payment
        {
            Id = Guid.NewGuid(),
            PayerId = Guid.NewGuid(),
            Amount = (decimal)(100 + (i % 900)), // Random amounts between 100 and 1000
            Status = i % 20 == 0 ? PaymentStatus.Failed : PaymentStatus.Completed,
            Method = i % 3 == 0 ? PaymentMethod.CreditCard : (i % 3 == 1 ? PaymentMethod.EWallet : PaymentMethod.BankTransfer),
            CreatedAt = DateTime.UtcNow.AddDays(-i % 365),
            PaidAt = i % 20 == 0 ? (DateTime?)null : DateTime.UtcNow.AddDays(-i % 365)
        }).ToList();
        _context.Payments.AddRange(payments);
        await _context.SaveChangesAsync();

        // Act
        var startTime = DateTime.UtcNow;
        var result = await _adminService.GetFinancialOverviewAsync();
        var endTime = DateTime.UtcNow;

        // Assert
        result.Should().NotBeNull();
        result.TotalRevenueAllTime.Should().BeGreaterThan(0);
        result.TotalRevenueAllTime.Should().Be(payments.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount));
        
        // Performance check
        var duration = (endTime - startTime).TotalSeconds;
        duration.Should().BeLessThan(10);
    }

    #endregion

    #region Concurrent Admin Actions Tests

    [Fact]
    public async Task ConcurrentUserStatusUpdates_MultipleAdmins_HandlesGracefully()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "concurrent@test.com",
            FirstName = "Concurrent",
            LastName = "User",
            KycStatus = KycStatus.Approved,
            Role = UserRole.CoOwner,
            CreatedAt = DateTime.UtcNow,
            LockoutEnd = null
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var admin1 = new User { Id = Guid.NewGuid(), Email = "admin1@test.com", FirstName = "Admin1", LastName = "User", KycStatus = KycStatus.Approved, Role = UserRole.SystemAdmin, CreatedAt = DateTime.UtcNow };
        var admin2 = new User { Id = Guid.NewGuid(), Email = "admin2@test.com", FirstName = "Admin2", LastName = "User", KycStatus = KycStatus.Approved, Role = UserRole.SystemAdmin, CreatedAt = DateTime.UtcNow };
        var admin3 = new User { Id = Guid.NewGuid(), Email = "admin3@test.com", FirstName = "Admin3", LastName = "User", KycStatus = KycStatus.Approved, Role = UserRole.Staff, CreatedAt = DateTime.UtcNow };
        _context.Users.AddRange(admin1, admin2, admin3);
        await _context.SaveChangesAsync();

        var tasks = new List<Task<bool>>();

        // Act - Simulate 10 concurrent updates
        for (int i = 0; i < 10; i++)
        {
            var admin = i % 3 == 0 ? admin1 : (i % 3 == 1 ? admin2 : admin3);
            var status = i % 2 == 0 ? UserAccountStatus.Suspended : UserAccountStatus.Active;
            
            tasks.Add(_adminService.UpdateUserStatusAsync(user.Id, new UpdateUserStatusDto
            {
                Status = status,
                Reason = $"Concurrent update {i}"
            }, admin.Id));
        }

        var results = await Task.WhenAll(tasks);

        // Assert - All should succeed (last write wins in EF Core)
        results.Should().AllBeEquivalentTo(true);
        
        // Verify final state
        var finalUser = await _context.Users.FindAsync(user.Id);
        finalUser.Should().NotBeNull();
        
        // Verify audit logs were created for all updates
        var auditLogs = await _context.AuditLogs
            .Where(al => al.EntityId == user.Id && al.Action == "StatusUpdated")
            .ToListAsync();
        auditLogs.Should().HaveCount(10);
    }

    [Fact]
    public async Task ConcurrentGroupStatusUpdates_MultipleAdmins_HandlesGracefully()
    {
        // Arrange
        var group = new OwnershipGroup
        {
            Id = Guid.NewGuid(),
            Name = "Concurrent Group",
            Status = GroupStatus.Active,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.OwnershipGroups.Add(group);
        await _context.SaveChangesAsync();

        var admin1 = new User { Id = Guid.NewGuid(), Email = "admin1@test.com", FirstName = "Admin1", LastName = "User", KycStatus = KycStatus.Approved, Role = UserRole.SystemAdmin, CreatedAt = DateTime.UtcNow };
        var admin2 = new User { Id = Guid.NewGuid(), Email = "admin2@test.com", FirstName = "Admin2", LastName = "User", KycStatus = KycStatus.Approved, Role = UserRole.SystemAdmin, CreatedAt = DateTime.UtcNow };
        _context.Users.AddRange(admin1, admin2);
        await _context.SaveChangesAsync();

        // Act - Concurrent updates
        var task1 = _adminService.UpdateGroupStatusAsync(group.Id, new UpdateGroupStatusDto
        {
            Status = GroupStatus.Inactive,
            Reason = "Update 1"
        }, admin1.Id);

        var task2 = _adminService.UpdateGroupStatusAsync(group.Id, new UpdateGroupStatusDto
        {
            Status = GroupStatus.Dissolved,
            Reason = "Update 2"
        }, admin2.Id);

        var results = await Task.WhenAll(task1, task2);

        // Assert
        results.Should().AllBeEquivalentTo(true);
        
        // Verify audit logs
        var auditLogs = await _context.AuditLogs
            .Where(al => al.EntityId == group.Id && al.Action == "StatusUpdated")
            .ToListAsync();
        auditLogs.Should().HaveCount(2);
    }

    [Fact]
    public async Task ConcurrentDisputeCreations_MultipleAdmins_CreatesAllDisputes()
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

        var admin1 = new User { Id = Guid.NewGuid(), Email = "admin1@test.com", FirstName = "Admin1", LastName = "User", KycStatus = KycStatus.Approved, Role = UserRole.SystemAdmin, CreatedAt = DateTime.UtcNow };
        var admin2 = new User { Id = Guid.NewGuid(), Email = "admin2@test.com", FirstName = "Admin2", LastName = "User", KycStatus = KycStatus.Approved, Role = UserRole.SystemAdmin, CreatedAt = DateTime.UtcNow };
        _context.Users.AddRange(admin1, admin2);
        await _context.SaveChangesAsync();

        // Act - Create 20 disputes concurrently
        var tasks = Enumerable.Range(1, 20).Select(i => 
            _adminService.CreateDisputeAsync(new CreateDisputeDto
            {
                GroupId = group.Id,
                Subject = $"Concurrent Dispute {i}",
                Description = $"Description {i}",
                Category = DisputeCategory.Financial,
                Priority = DisputePriority.Medium
            }, i % 2 == 0 ? admin1.Id : admin2.Id)
        ).ToList();

        var disputeIds = await Task.WhenAll(tasks);

        // Assert
        disputeIds.Should().HaveCount(20);
        disputeIds.Should().OnlyContain(id => id != Guid.Empty);
        disputeIds.Should().OnlyHaveUniqueItems();

        var disputes = await _context.Disputes
            .Where(d => d.GroupId == group.Id)
            .ToListAsync();
        disputes.Should().HaveCount(20);
    }

    #endregion
}

