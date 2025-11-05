using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using CoOwnershipVehicle.Admin.Api.Data;
using CoOwnershipVehicle.Admin.Api.Services;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Shared.Contracts.DTOs;

namespace CoOwnershipVehicle.Admin.Api.Tests.UnitTests;

public class AdminServiceTests : IDisposable
{
    private readonly AdminDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly Mock<ILogger<AdminService>> _loggerMock;
    private readonly AdminService _adminService;
    private readonly DbContextOptions<AdminDbContext> _options;

    public AdminServiceTests()
    {
        _options = new DbContextOptionsBuilder<AdminDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new AdminDbContext(_options);
        _cache = new MemoryCache(new MemoryCacheOptions());
        _loggerMock = new Mock<ILogger<AdminService>>();

        _adminService = new AdminService(_context, _cache, _loggerMock.Object);
        
        SeedTestData();
    }

    public void Dispose()
    {
        _context.Dispose();
        _cache.Dispose();
    }

    private void SeedTestData()
    {
        // Add test users
        var users = new List<User>
        {
            new User
            {
                Id = Guid.NewGuid(),
                Email = "user1@test.com",
                FirstName = "John",
                LastName = "Doe",
                KycStatus = KycStatus.Approved,
                Role = UserRole.CoOwner,
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                LockoutEnd = null
            },
            new User
            {
                Id = Guid.NewGuid(),
                Email = "user2@test.com",
                FirstName = "Jane",
                LastName = "Smith",
                KycStatus = KycStatus.Pending,
                Role = UserRole.CoOwner,
                CreatedAt = DateTime.UtcNow.AddDays(-7),
                LockoutEnd = null
            },
            new User
            {
                Id = Guid.NewGuid(),
                Email = "admin@test.com",
                FirstName = "Admin",
                LastName = "User",
                KycStatus = KycStatus.Approved,
                Role = UserRole.SystemAdmin,
                CreatedAt = DateTime.UtcNow.AddDays(-90),
                LockoutEnd = null
            }
        };

        _context.Users.AddRange(users);
        _context.SaveChanges();

        // Add test groups
        var groups = new List<OwnershipGroup>
        {
            new OwnershipGroup
            {
                Id = Guid.NewGuid(),
                Name = "Test Group 1",
                Description = "First test group",
                Status = GroupStatus.Active,
                CreatedBy = users[0].Id,
                CreatedAt = DateTime.UtcNow.AddDays(-60),
                UpdatedAt = DateTime.UtcNow.AddDays(-5)
            },
            new OwnershipGroup
            {
                Id = Guid.NewGuid(),
                Name = "Test Group 2",
                Description = "Second test group",
                Status = GroupStatus.Inactive,
                CreatedBy = users[1].Id,
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                UpdatedAt = DateTime.UtcNow.AddDays(-50)
            }
        };

        _context.OwnershipGroups.AddRange(groups);
        _context.SaveChanges();

        // Add test vehicles
        var vehicles = new List<Vehicle>
        {
            new Vehicle
            {
                Id = Guid.NewGuid(),
                Vin = "1HGBH41JXMN109186",
                PlateNumber = "TEST001",
                Model = "Tesla Model 3",
                Year = 2023,
                Status = VehicleStatus.Available,
                GroupId = groups[0].Id,
                CreatedAt = DateTime.UtcNow.AddDays(-45),
                LastServiceDate = DateTime.UtcNow.AddMonths(-3)
            },
            new Vehicle
            {
                Id = Guid.NewGuid(),
                Vin = "5YJ3E1EA1KF123456",
                PlateNumber = "TEST002",
                Model = "Tesla Model Y",
                Year = 2024,
                Status = VehicleStatus.InUse,
                GroupId = groups[0].Id,
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                LastServiceDate = DateTime.UtcNow.AddMonths(-8)
            }
        };

        _context.Vehicles.AddRange(vehicles);
        _context.SaveChanges();

        // Add test bookings
        var bookings = new List<Booking>
        {
            new Booking
            {
                Id = Guid.NewGuid(),
                VehicleId = vehicles[0].Id,
                GroupId = groups[0].Id,
                UserId = users[0].Id,
                StartAt = DateTime.UtcNow.AddDays(1),
                EndAt = DateTime.UtcNow.AddDays(2),
                Status = BookingStatus.Confirmed,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            },
            new Booking
            {
                Id = Guid.NewGuid(),
                VehicleId = vehicles[0].Id,
                GroupId = groups[0].Id,
                UserId = users[0].Id,
                StartAt = DateTime.UtcNow.AddDays(-30),
                EndAt = DateTime.UtcNow.AddDays(-29),
                Status = BookingStatus.Completed,
                CreatedAt = DateTime.UtcNow.AddDays(-32)
            },
            new Booking
            {
                Id = Guid.NewGuid(),
                VehicleId = vehicles[1].Id,
                GroupId = groups[0].Id,
                UserId = users[1].Id,
                StartAt = DateTime.UtcNow,
                EndAt = DateTime.UtcNow.AddDays(1),
                Status = BookingStatus.InProgress,
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            }
        };

        _context.Bookings.AddRange(bookings);
        _context.SaveChanges();

        // Add test expenses
        var expenses = new List<Expense>
        {
            new Expense
            {
                Id = Guid.NewGuid(),
                GroupId = groups[0].Id,
                VehicleId = vehicles[0].Id,
                ExpenseType = ExpenseType.Fuel,
                Amount = 500,
                Description = "Fuel purchase",
                DateIncurred = DateTime.UtcNow.AddDays(-10),
                CreatedBy = users[0].Id,
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            },
            new Expense
            {
                Id = Guid.NewGuid(),
                GroupId = groups[0].Id,
                VehicleId = vehicles[0].Id,
                ExpenseType = ExpenseType.Maintenance,
                Amount = 1500,
                Description = "Regular maintenance",
                DateIncurred = DateTime.UtcNow.AddDays(-20),
                CreatedBy = users[0].Id,
                CreatedAt = DateTime.UtcNow.AddDays(-20)
            }
        };

        _context.Expenses.AddRange(expenses);
        _context.SaveChanges();

        // Add group members
        var groupMembers = new List<GroupMember>
        {
            new GroupMember
            {
                Id = Guid.NewGuid(),
                GroupId = groups[0].Id,
                UserId = users[0].Id,
                RoleInGroup = GroupRole.Admin,
                SharePercentage = 0.5m,
                JoinedAt = DateTime.UtcNow.AddDays(-60)
            },
            new GroupMember
            {
                Id = Guid.NewGuid(),
                GroupId = groups[0].Id,
                UserId = users[1].Id,
                RoleInGroup = GroupRole.Member,
                SharePercentage = 0.5m,
                JoinedAt = DateTime.UtcNow.AddDays(-55)
            }
        };

        _context.GroupMembers.AddRange(groupMembers);
        _context.SaveChanges();
    }

    #region Dashboard Tests

    [Fact]
    public async Task GetDashboardMetricsAsync_WithValidRequest_ReturnsCompleteMetrics()
    {
        // Arrange
        var request = new DashboardRequestDto { Period = TimePeriod.Monthly };

        // Act
        var result = await _adminService.GetDashboardMetricsAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Users.Should().NotBeNull();
        result.Groups.Should().NotBeNull();
        result.Vehicles.Should().NotBeNull();
        result.Bookings.Should().NotBeNull();
        result.Revenue.Should().NotBeNull();
    }

    [Fact]
    public async Task GetDashboardMetricsAsync_UserMetrics_AreAccurate()
    {
        // Arrange
        var request = new DashboardRequestDto { Period = TimePeriod.Monthly };

        // Act
        var result = await _adminService.GetDashboardMetricsAsync(request);

        // Assert
        result.Users.TotalUsers.Should().BeGreaterThanOrEqualTo(3);
        result.Users.ActiveUsers.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task GetDashboardMetricsAsync_GroupMetrics_AreAccurate()
    {
        // Arrange
        var request = new DashboardRequestDto { Period = TimePeriod.Monthly };

        // Act
        var result = await _adminService.GetDashboardMetricsAsync(request);

        // Assert
        result.Groups.TotalGroups.Should().Be(2);
        result.Groups.ActiveGroups.Should().Be(1);
        result.Groups.InactiveGroups.Should().Be(1);
    }

    [Fact]
    public async Task GetDashboardMetricsAsync_VehicleMetrics_AreAccurate()
    {
        // Arrange
        var request = new DashboardRequestDto { Period = TimePeriod.Monthly };

        // Act
        var result = await _adminService.GetDashboardMetricsAsync(request);

        // Assert
        result.Vehicles.TotalVehicles.Should().Be(2);
        result.Vehicles.AvailableVehicles.Should().Be(1);
        result.Vehicles.InUseVehicles.Should().Be(1);
    }

    [Fact]
    public async Task GetDashboardMetricsAsync_BookingMetrics_AreAccurate()
    {
        // Arrange
        var request = new DashboardRequestDto { Period = TimePeriod.Monthly };

        // Act
        var result = await _adminService.GetDashboardMetricsAsync(request);

        // Assert
        result.Bookings.TotalBookings.Should().Be(3);
        result.Bookings.ConfirmedBookings.Should().Be(1);
        result.Bookings.CompletedBookings.Should().Be(1);
        result.Bookings.InProgressBookings.Should().Be(1);
    }

    [Fact]
    public async Task GetDashboardMetricsAsync_IsCached()
    {
        // Arrange
        var request = new DashboardRequestDto { Period = TimePeriod.Monthly };

        // Act
        var result1 = await _adminService.GetDashboardMetricsAsync(request);
        var result2 = await _adminService.GetDashboardMetricsAsync(request);

        // Assert
        result1.Should().BeEquivalentTo(result2);
    }

    #endregion

    #region System Health Tests

    [Fact]
    public async Task GetSystemHealthAsync_ReturnsHealthStatus()
    {
        // Act
        var result = await _adminService.GetSystemHealthAsync();

        // Assert
        result.Should().NotBeNull();
        result.DatabaseConnected.Should().BeTrue();
        result.LastHealthCheck.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetRecentActivityAsync_ReturnsActivities()
    {
        // Arrange
        var count = 10;

        // Act
        var result = await _adminService.GetRecentActivityAsync(count);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCountLessOrEqualTo(count);
    }

    [Fact]
    public async Task GetAlertsAsync_ReturnsAlerts()
    {
        // Act
        var result = await _adminService.GetAlertsAsync();

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region User Management Tests

    [Fact]
    public async Task GetUsersAsync_WithSearch_ReturnsFilteredResults()
    {
        // Arrange
        var request = new UserListRequestDto
        {
            Page = 1,
            PageSize = 10,
            Search = "John"
        };

        // Act
        var result = await _adminService.GetUsersAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Users.Should().OnlyContain(u => u.FirstName.Contains("John") || u.LastName.Contains("John"));
    }

    [Fact]
    public async Task GetUsersAsync_WithRoleFilter_ReturnsFilteredResults()
    {
        // Arrange
        var request = new UserListRequestDto
        {
            Page = 1,
            PageSize = 10,
            Role = UserRole.SystemAdmin
        };

        // Act
        var result = await _adminService.GetUsersAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Users.Should().OnlyContain(u => u.Role == UserRole.SystemAdmin);
    }

    [Fact]
    public async Task GetUsersAsync_WithKycStatusFilter_ReturnsFilteredResults()
    {
        // Arrange
        var request = new UserListRequestDto
        {
            Page = 1,
            PageSize = 10,
            KycStatus = KycStatus.Pending
        };

        // Act
        var result = await _adminService.GetUsersAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Users.Should().OnlyContain(u => u.KycStatus == KycStatus.Pending);
    }

    [Fact]
    public async Task GetUserDetailsAsync_WithValidId_ReturnsUserDetails()
    {
        // Arrange
        var user = await _context.Users.FirstAsync();
        var userId = user.Id;

        // Act
        var result = await _adminService.GetUserDetailsAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(userId);
        result.Email.Should().Be(user.Email);
        result.FirstName.Should().Be(user.FirstName);
        result.LastName.Should().Be(user.LastName);
    }

    [Fact]
    public async Task GetUserDetailsAsync_WithInvalidId_ThrowsArgumentException()
    {
        // Arrange
        var invalidId = Guid.NewGuid();

        // Act
        var act = async () => await _adminService.GetUserDetailsAsync(invalidId);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("User not found");
    }

    [Fact]
    public async Task GetPendingKycUsersAsync_ReturnsPendingUsers()
    {
        // Act
        var result = await _adminService.GetPendingKycUsersAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().OnlyContain(u => u.KycStatus == KycStatus.Pending || u.KycStatus == KycStatus.InReview);
    }

    #endregion

    #region Group Management Tests

    [Fact]
    public async Task GetGroupsAsync_WithSearch_ReturnsFilteredResults()
    {
        // Arrange
        var request = new GroupListRequestDto
        {
            Page = 1,
            PageSize = 10,
            Search = "Group 1"
        };

        // Act
        var result = await _adminService.GetGroupsAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Groups.Should().OnlyContain(g => g.Name.Contains("Group 1"));
    }

    [Fact]
    public async Task GetGroupsAsync_WithStatusFilter_ReturnsFilteredResults()
    {
        // Arrange
        var request = new GroupListRequestDto
        {
            Page = 1,
            PageSize = 10,
            Status = GroupStatus.Active
        };

        // Act
        var result = await _adminService.GetGroupsAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Groups.Should().OnlyContain(g => g.Status == GroupStatus.Active);
    }

    [Fact]
    public async Task GetGroupDetailsAsync_WithValidId_ReturnsGroupDetails()
    {
        // Arrange
        var group = await _context.OwnershipGroups.FirstAsync();
        var groupId = group.Id;

        // Act
        var result = await _adminService.GetGroupDetailsAsync(groupId);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(groupId);
        result.Name.Should().Be(group.Name);
    }

    [Fact]
    public async Task GetGroupDetailsAsync_WithInvalidId_ThrowsArgumentException()
    {
        // Arrange
        var invalidId = Guid.NewGuid();

        // Act
        var act = async () => await _adminService.GetGroupDetailsAsync(invalidId);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Group not found");
    }

    [Fact]
    public async Task GetGroupHealthAsync_WithValidId_ReturnsHealthStatus()
    {
        // Arrange
        var group = await _context.OwnershipGroups.FirstAsync();
        var groupId = group.Id;

        // Act
        var result = await _adminService.GetGroupHealthAsync(groupId);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().BeOneOf(
            GroupHealthStatus.Healthy,
            GroupHealthStatus.Warning,
            GroupHealthStatus.Unhealthy,
            GroupHealthStatus.Critical);
        result.Score.Should().BeInRange(0, 100);
    }

    #endregion

    #region Financial Tests

    [Fact]
    public async Task GetFinancialOverviewAsync_ReturnsFinancialData()
    {
        // Act
        var result = await _adminService.GetFinancialOverviewAsync();

        // Assert
        result.Should().NotBeNull();
        result.TotalRevenueAllTime.Should().BeGreaterThanOrEqualTo(0);
        result.TotalRevenueMonth.Should().BeGreaterThanOrEqualTo(0);
        result.FinancialHealthScore.Should().BeInRange(0, 100);
    }

    [Fact]
    public async Task GetFinancialByGroupsAsync_ReturnsGroupBreakdown()
    {
        // Act
        var result = await _adminService.GetFinancialByGroupsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Groups.Should().NotBeNull();
    }

    [Fact]
    public async Task GetPaymentStatisticsAsync_ReturnsPaymentStats()
    {
        // Act
        var result = await _adminService.GetPaymentStatisticsAsync();

        // Assert
        result.Should().NotBeNull();
        result.SuccessRate.Should().BeGreaterThanOrEqualTo(0);
        result.FailureRate.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetExpenseAnalysisAsync_ReturnsExpenseAnalysis()
    {
        // Act
        var result = await _adminService.GetExpenseAnalysisAsync();

        // Assert
        result.Should().NotBeNull();
        result.TotalByType.Should().NotBeNull();
    }

    [Fact]
    public async Task GetFinancialAnomaliesAsync_ReturnsAnomalies()
    {
        // Act
        var result = await _adminService.GetFinancialAnomaliesAsync();

        // Assert
        result.Should().NotBeNull();
        result.UnusualTransactions.Should().NotBeNull();
        result.SuspiciousPaymentPatterns.Should().NotBeNull();
        result.NegativeBalanceGroups.Should().NotBeNull();
    }

    #endregion

    #region Dispute Tests

    [Fact]
    public async Task CreateDisputeAsync_WithValidData_CreatesDispute()
    {
        // Arrange
        var group = await _context.OwnershipGroups.FirstAsync();
        var adminUser = await _context.Users.Where(u => u.Role == UserRole.SystemAdmin).FirstAsync();
        var request = new CreateDisputeDto
        {
            GroupId = group.Id,
            Subject = "Test Dispute",
            Description = "This is a test dispute",
            Category = DisputeCategory.Financial,
            Priority = DisputePriority.High
        };

        // Act
        var disputeId = await _adminService.CreateDisputeAsync(request, adminUser.Id);

        // Assert
        disputeId.Should().NotBeEmpty();
        var dispute = await _context.Disputes.FindAsync(disputeId);
        dispute.Should().NotBeNull();
        dispute!.Subject.Should().Be(request.Subject);
    }

    [Fact]
    public async Task CreateDisputeAsync_WithInvalidGroup_ThrowsArgumentException()
    {
        // Arrange
        var adminUser = await _context.Users.Where(u => u.Role == UserRole.SystemAdmin).FirstAsync();
        var request = new CreateDisputeDto
        {
            GroupId = Guid.NewGuid(),
            Subject = "Test Dispute",
            Description = "This is a test dispute",
            Category = DisputeCategory.Financial,
            Priority = DisputePriority.High
        };

        // Act
        var act = async () => await _adminService.CreateDisputeAsync(request, adminUser.Id);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Group not found");
    }

    [Fact]
    public async Task GetDisputesAsync_ReturnsDisputes()
    {
        // Arrange
        var request = new DisputeListRequestDto { Page = 1, PageSize = 10 };

        // Act
        var result = await _adminService.GetDisputesAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Disputes.Should().NotBeNull();
    }

    [Fact]
    public async Task GetDisputeStatisticsAsync_ReturnsStatistics()
    {
        // Act
        var result = await _adminService.GetDisputeStatisticsAsync();

        // Assert
        result.Should().NotBeNull();
        result.TotalDisputes.Should().BeGreaterThanOrEqualTo(0);
    }

    #endregion

    #region Comprehensive Financial Calculation Tests

    [Fact]
    public async Task GetFinancialOverview_RevenueBySource_CalculatedCorrectly()
    {
        // Arrange
        var group = new OwnershipGroup { Id = Guid.NewGuid(), Name = "Test Group", Status = GroupStatus.Active, CreatedBy = Guid.NewGuid(), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _context.OwnershipGroups.Add(group);
        await _context.SaveChangesAsync();

        var ledgerEntries = new List<LedgerEntry>
        {
            new LedgerEntry { Id = Guid.NewGuid(), GroupId = group.Id, Type = LedgerEntryType.Deposit, Amount = 1000, BalanceAfter = 1000, CreatedAt = DateTime.UtcNow },
            new LedgerEntry { Id = Guid.NewGuid(), GroupId = group.Id, Type = LedgerEntryType.Fee, Amount = 100, BalanceAfter = 900, CreatedAt = DateTime.UtcNow },
            new LedgerEntry { Id = Guid.NewGuid(), GroupId = group.Id, Type = LedgerEntryType.RefundReceived, Amount = 50, BalanceAfter = 950, CreatedAt = DateTime.UtcNow }
        };
        _context.LedgerEntries.AddRange(ledgerEntries);
        await _context.SaveChangesAsync();

        // Act
        var result = await _adminService.GetFinancialOverviewAsync();

        // Assert
        result.Should().NotBeNull();
        result.RevenueBySource.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetFinancialByGroups_BalanceCalculation_MatchDatabase()
    {
        // Arrange
        var group = new OwnershipGroup { Id = Guid.NewGuid(), Name = "Test Group", Status = GroupStatus.Active, CreatedBy = Guid.NewGuid(), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _context.OwnershipGroups.Add(group);
        await _context.SaveChangesAsync();

        var ledgerEntries = new List<LedgerEntry>
        {
            new LedgerEntry { Id = Guid.NewGuid(), GroupId = group.Id, Type = LedgerEntryType.Deposit, Amount = 5000, BalanceAfter = 5000, CreatedAt = DateTime.UtcNow.AddDays(-10) },
            new LedgerEntry { Id = Guid.NewGuid(), GroupId = group.Id, Type = LedgerEntryType.Fee, Amount = 500, BalanceAfter = 4500, CreatedAt = DateTime.UtcNow }
        };
        _context.LedgerEntries.AddRange(ledgerEntries);
        
        var expenses = new List<Expense>
        {
            new Expense { Id = Guid.NewGuid(), GroupId = group.Id, ExpenseType = ExpenseType.Fuel, Amount = 1000, Description = "Fuel", CreatedBy = Guid.NewGuid(), CreatedAt = DateTime.UtcNow }
        };
        _context.Expenses.AddRange(expenses);
        await _context.SaveChangesAsync();

        // Act
        var result = await _adminService.GetFinancialByGroupsAsync();

        // Assert
        result.Should().NotBeNull();
        var groupFinancial = result.Groups.FirstOrDefault(g => g.GroupId == group.Id);
        groupFinancial.Should().NotBeNull();
        groupFinancial!.TotalExpenses.Should().Be(1000);
    }

    [Fact]
    public async Task GetPaymentStatistics_AllMetrics_CalculatedCorrectly()
    {
        // Arrange
        var payments = new List<Payment>
        {
            new Payment { Id = Guid.NewGuid(), PayerId = Guid.NewGuid(), Amount = 100, Status = PaymentStatus.Completed, Method = PaymentMethod.CreditCard, CreatedAt = DateTime.UtcNow, PaidAt = DateTime.UtcNow },
            new Payment { Id = Guid.NewGuid(), PayerId = Guid.NewGuid(), Amount = 200, Status = PaymentStatus.Completed, Method = PaymentMethod.EWallet, CreatedAt = DateTime.UtcNow, PaidAt = DateTime.UtcNow },
            new Payment { Id = Guid.NewGuid(), PayerId = Guid.NewGuid(), Amount = 300, Status = PaymentStatus.Failed, Method = PaymentMethod.CreditCard, CreatedAt = DateTime.UtcNow },
            new Payment { Id = Guid.NewGuid(), PayerId = Guid.NewGuid(), Amount = 400, Status = PaymentStatus.Completed, Method = PaymentMethod.BankTransfer, CreatedAt = DateTime.UtcNow, PaidAt = DateTime.UtcNow }
        };
        _context.Payments.AddRange(payments);
        await _context.SaveChangesAsync();

        // Act
        var result = await _adminService.GetPaymentStatisticsAsync();

        // Assert
        result.Should().NotBeNull();
        result.SuccessRate.Should().BeApproximately(75.0, 0.1); // 3 out of 4
        result.FailureRate.Should().BeApproximately(25.0, 0.1); // 1 out of 4
        result.AverageAmount.Should().BeInRange(249.9m, 250.1m); // Average of all amounts
        result.MethodCounts.Should().ContainKey(PaymentMethod.CreditCard);
        result.MethodCounts.Should().ContainKey(PaymentMethod.EWallet);
        result.MethodCounts.Should().ContainKey(PaymentMethod.BankTransfer);
    }

    [Fact]
    public async Task GetExpenseAnalysis_ByType_MatchDatabase()
    {
        // Arrange
        var group = new OwnershipGroup { Id = Guid.NewGuid(), Name = "Test Group", Status = GroupStatus.Active, CreatedBy = Guid.NewGuid(), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _context.OwnershipGroups.Add(group);
        await _context.SaveChangesAsync();

        var expenses = new List<Expense>
        {
            new Expense { Id = Guid.NewGuid(), GroupId = group.Id, ExpenseType = ExpenseType.Fuel, Amount = 500, Description = "Fuel 1", CreatedBy = Guid.NewGuid(), CreatedAt = DateTime.UtcNow },
            new Expense { Id = Guid.NewGuid(), GroupId = group.Id, ExpenseType = ExpenseType.Fuel, Amount = 300, Description = "Fuel 2", CreatedBy = Guid.NewGuid(), CreatedAt = DateTime.UtcNow },
            new Expense { Id = Guid.NewGuid(), GroupId = group.Id, ExpenseType = ExpenseType.Maintenance, Amount = 1000, Description = "Maintenance", CreatedBy = Guid.NewGuid(), CreatedAt = DateTime.UtcNow },
            new Expense { Id = Guid.NewGuid(), GroupId = group.Id, ExpenseType = ExpenseType.Insurance, Amount = 200, Description = "Insurance", CreatedBy = Guid.NewGuid(), CreatedAt = DateTime.UtcNow }
        };
        _context.Expenses.AddRange(expenses);
        await _context.SaveChangesAsync();

        // Act
        var result = await _adminService.GetExpenseAnalysisAsync();

        // Assert
        result.Should().NotBeNull();
        result.TotalByType.Should().ContainKey(ExpenseType.Fuel);
        result.TotalByType[ExpenseType.Fuel].Should().Be(800);
        result.TotalByType[ExpenseType.Maintenance].Should().Be(1000);
        result.TotalByType[ExpenseType.Insurance].Should().Be(200);
    }

    #endregion

    #region Dashboard Growth Calculation Tests

    [Fact]
    public async Task DashboardMetrics_GrowthPercentage_CalculatedCorrectly()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var thisMonthStart = new DateTime(now.Year, now.Month, 1);
        var lastMonthStart = thisMonthStart.AddMonths(-1);

        // Users created this month
        var usersThisMonth = Enumerable.Range(1, 20).Select(i => new User
        {
            Id = Guid.NewGuid(),
            Email = $"user{i}@test.com",
            FirstName = $"User{i}",
            LastName = "Test",
            KycStatus = KycStatus.Approved,
            Role = UserRole.CoOwner,
            CreatedAt = thisMonthStart.AddDays(i % 28),
            LockoutEnd = null
        }).ToList();

        // Users created last month
        var usersLastMonth = Enumerable.Range(1, 10).Select(i => new User
        {
            Id = Guid.NewGuid(),
            Email = $"userold{i}@test.com",
            FirstName = $"UserOld{i}",
            LastName = "Test",
            KycStatus = KycStatus.Approved,
            Role = UserRole.CoOwner,
            CreatedAt = lastMonthStart.AddDays(i % 28),
            LockoutEnd = null
        }).ToList();

        _context.Users.AddRange(usersThisMonth);
        _context.Users.AddRange(usersLastMonth);
        await _context.SaveChangesAsync();

        // Act
        var result = await _adminService.GetDashboardMetricsAsync(new DashboardRequestDto { Period = TimePeriod.Monthly });

        // Assert
        result.Users.UserGrowthPercentage.Should().BeGreaterThan(0); // Should show growth
    }

    #endregion

    #region Edge Case Calculation Tests

    [Fact]
    public async Task FinancialOverview_WithNoPayments_ReturnsZeroValues()
    {
        // Act
        var result = await _adminService.GetFinancialOverviewAsync();

        // Assert
        result.TotalRevenueAllTime.Should().Be(0);
        result.TotalRevenueMonth.Should().Be(0);
        result.PaymentSuccessRate.Should().Be(0);
        result.FailedPaymentsCount.Should().Be(0);
    }

    [Fact]
    public async Task RevenueMetrics_WithZeroUsers_GracefullyHandlesDivision()
    {
        // Arrange - Create payments but no users
        var payments = new List<Payment>
        {
            new Payment { Id = Guid.NewGuid(), PayerId = Guid.NewGuid(), Amount = 1000, Status = PaymentStatus.Completed, Method = PaymentMethod.CreditCard, CreatedAt = DateTime.UtcNow, PaidAt = DateTime.UtcNow }
        };
        _context.Payments.AddRange(payments);
        await _context.SaveChangesAsync();

        // Act
        var result = await _adminService.GetDashboardMetricsAsync(new DashboardRequestDto { Period = TimePeriod.Monthly });

        // Assert
        result.Revenue.AverageRevenuePerUser.Should().Be(0); // Should not throw division by zero
        result.Revenue.TotalRevenue.Should().Be(1000);
    }

    [Fact]
    public async Task DashboardMetrics_CachedResults_ReturnSameData()
    {
        // Arrange
        var request = new DashboardRequestDto { Period = TimePeriod.Monthly };

        // Act - Call twice
        var result1 = await _adminService.GetDashboardMetricsAsync(request);
        var result2 = await _adminService.GetDashboardMetricsAsync(request);

        // Assert - Results should be equivalent (cached)
        result1.Should().BeEquivalentTo(result2);
    }

    #endregion
}

