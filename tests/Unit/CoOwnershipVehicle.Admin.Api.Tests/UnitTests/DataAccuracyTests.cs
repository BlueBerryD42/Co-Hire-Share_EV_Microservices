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

public class DataAccuracyTests : IDisposable
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

    public DataAccuracyTests()
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

    #region Dashboard Metrics Accuracy Tests

    [Fact]
    public async Task DashboardMetrics_UserCounts_MatchDatabase()
    {
        // Arrange
        var users = new List<User>
        {
            new User { Id = Guid.NewGuid(), Email = "user1@test.com", FirstName = "User1", LastName = "Test", KycStatus = KycStatus.Approved, Role = UserRole.CoOwner, CreatedAt = DateTime.UtcNow, LockoutEnd = null },
            new User { Id = Guid.NewGuid(), Email = "user2@test.com", FirstName = "User2", LastName = "Test", KycStatus = KycStatus.Pending, Role = UserRole.CoOwner, CreatedAt = DateTime.UtcNow, LockoutEnd = null },
            new User { Id = Guid.NewGuid(), Email = "user3@test.com", FirstName = "User3", LastName = "Test", KycStatus = KycStatus.Rejected, Role = UserRole.CoOwner, CreatedAt = DateTime.UtcNow, LockoutEnd = DateTime.UtcNow.AddYears(1) }
        };
        // TODO: AdminDbContext no longer has Users DbSet - use UserServiceClient mock instead
        // _context.Users.AddRange(users);
        // await _context.SaveChangesAsync();
        var testUsers = users.Select(u => new UserProfileDto { Id = u.Id, Email = u.Email, FirstName = u.FirstName, LastName = u.LastName, KycStatus = u.KycStatus, Role = u.Role, CreatedAt = u.CreatedAt }).ToList();
        _userServiceClientMock.Setup(x => x.GetUsersAsync(It.IsAny<UserListRequestDto>()))
            .ReturnsAsync(testUsers);

        // Act
        var result = await _adminService.GetDashboardMetricsAsync(new DashboardRequestDto { Period = TimePeriod.Monthly });

        // Assert
        result.Users.TotalUsers.Should().Be(3);
        result.Users.ApprovedKyc.Should().Be(1);
        result.Users.PendingKyc.Should().Be(1);
        result.Users.RejectedKyc.Should().Be(1);
        result.Users.ActiveUsers.Should().Be(2); // 2 users without lockout
        result.Users.InactiveUsers.Should().Be(1); // 1 user with lockout
    }

    [Fact]
    public async Task DashboardMetrics_GroupCounts_MatchDatabase()
    {
        // Arrange
        var adminUser = new User { Id = Guid.NewGuid(), Email = "admin@test.com", FirstName = "Admin", LastName = "User", KycStatus = KycStatus.Approved, Role = UserRole.SystemAdmin, CreatedAt = DateTime.UtcNow };
        // TODO: AdminDbContext no longer has Users DbSet - use UserServiceClient mock instead
        // _context.Users.Add(adminUser);
        // await _context.SaveChangesAsync();
        _userServiceClientMock.Setup(x => x.GetUsersAsync(It.IsAny<UserListRequestDto>()))
            .ReturnsAsync(new List<UserProfileDto> { new UserProfileDto { Id = adminUser.Id, Email = adminUser.Email, FirstName = adminUser.FirstName, LastName = adminUser.LastName, KycStatus = adminUser.KycStatus, Role = adminUser.Role, CreatedAt = adminUser.CreatedAt } });

        var groups = new List<OwnershipGroup>
        {
            new OwnershipGroup { Id = Guid.NewGuid(), Name = "Active Group", Status = GroupStatus.Active, CreatedBy = adminUser.Id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new OwnershipGroup { Id = Guid.NewGuid(), Name = "Inactive Group", Status = GroupStatus.Inactive, CreatedBy = adminUser.Id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new OwnershipGroup { Id = Guid.NewGuid(), Name = "Dissolved Group", Status = GroupStatus.Dissolved, CreatedBy = adminUser.Id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
        };
        // TODO: AdminDbContext no longer has OwnershipGroups DbSet - use GroupServiceClient mock instead
        // _context.OwnershipGroups.AddRange(groups);
        // await _context.SaveChangesAsync();
        var testGroups = groups.Select(g => new GroupDto { Id = g.Id, Name = g.Name, Status = g.Status, CreatedBy = g.CreatedBy, CreatedAt = g.CreatedAt, Members = new List<GroupMemberDto>(), Vehicles = new List<VehicleDto>() }).ToList();
        _groupServiceClientMock.Setup(x => x.GetGroupsAsync(It.IsAny<GroupListRequestDto>()))
            .ReturnsAsync(testGroups);

        // Act
        var result = await _adminService.GetDashboardMetricsAsync(new DashboardRequestDto { Period = TimePeriod.Monthly });

        // Assert
        result.Groups.TotalGroups.Should().Be(3);
        result.Groups.ActiveGroups.Should().Be(1);
        result.Groups.InactiveGroups.Should().Be(1);
        result.Groups.DissolvedGroups.Should().Be(1);
    }

    [Fact]
    public async Task DashboardMetrics_VehicleCounts_MatchDatabase()
    {
        // Arrange
        var group = new OwnershipGroup { Id = Guid.NewGuid(), Name = "Test Group", Status = GroupStatus.Active, CreatedBy = Guid.NewGuid(), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        // TODO: AdminDbContext no longer has OwnershipGroups DbSet - use GroupServiceClient mock instead
        // _context.OwnershipGroups.Add(group);
        // await _context.SaveChangesAsync();
        _groupServiceClientMock.Setup(x => x.GetGroupsAsync(It.IsAny<GroupListRequestDto>()))
            .ReturnsAsync(new List<GroupDto> { new GroupDto { Id = group.Id, Name = group.Name, Status = group.Status, CreatedBy = group.CreatedBy, CreatedAt = group.CreatedAt, Members = new List<GroupMemberDto>(), Vehicles = new List<VehicleDto>() } });

        var vehicles = new List<Vehicle>
        {
            new Vehicle { Id = Guid.NewGuid(), Vin = "VIN001", PlateNumber = "PLT001", Model = "Model1", Year = 2023, Status = VehicleStatus.Available, GroupId = group.Id, CreatedAt = DateTime.UtcNow },
            new Vehicle { Id = Guid.NewGuid(), Vin = "VIN002", PlateNumber = "PLT002", Model = "Model2", Year = 2023, Status = VehicleStatus.InUse, GroupId = group.Id, CreatedAt = DateTime.UtcNow },
            new Vehicle { Id = Guid.NewGuid(), Vin = "VIN003", PlateNumber = "PLT003", Model = "Model3", Year = 2023, Status = VehicleStatus.Maintenance, GroupId = group.Id, CreatedAt = DateTime.UtcNow },
            new Vehicle { Id = Guid.NewGuid(), Vin = "VIN004", PlateNumber = "PLT004", Model = "Model4", Year = 2023, Status = VehicleStatus.Unavailable, GroupId = group.Id, CreatedAt = DateTime.UtcNow }
        };
        // TODO: AdminDbContext no longer has Vehicles DbSet - use VehicleServiceClient mock instead
        // _context.Vehicles.AddRange(vehicles);
        // await _context.SaveChangesAsync();
        var testVehicles = vehicles.Select(v => new VehicleDto { Id = v.Id, Vin = v.Vin, PlateNumber = v.PlateNumber, Model = v.Model, Year = v.Year, Status = v.Status, GroupId = v.GroupId, CreatedAt = v.CreatedAt }).ToList();
        _vehicleServiceClientMock.Setup(x => x.GetVehiclesAsync())
            .ReturnsAsync(testVehicles);

        // Act
        var result = await _adminService.GetDashboardMetricsAsync(new DashboardRequestDto { Period = TimePeriod.Monthly });

        // Assert
        result.Vehicles.TotalVehicles.Should().Be(4);
        result.Vehicles.AvailableVehicles.Should().Be(1);
        result.Vehicles.InUseVehicles.Should().Be(1);
        result.Vehicles.MaintenanceVehicles.Should().Be(1);
        result.Vehicles.UnavailableVehicles.Should().Be(1);
    }

    [Fact]
    public async Task DashboardMetrics_BookingCounts_MatchDatabase()
    {
        // Arrange
        var user = new User { Id = Guid.NewGuid(), Email = "user@test.com", FirstName = "User", LastName = "Test", KycStatus = KycStatus.Approved, Role = UserRole.CoOwner, CreatedAt = DateTime.UtcNow };
        var group = new OwnershipGroup { Id = Guid.NewGuid(), Name = "Test Group", Status = GroupStatus.Active, CreatedBy = user.Id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var vehicle = new Vehicle { Id = Guid.NewGuid(), Vin = "VIN001", PlateNumber = "PLT001", Model = "Model1", Year = 2023, Status = VehicleStatus.Available, GroupId = group.Id, CreatedAt = DateTime.UtcNow };
        // TODO: AdminDbContext no longer has Users/OwnershipGroups/Vehicles DbSets - use HTTP client mocks instead
        // _context.Users.Add(user);
        // _context.OwnershipGroups.Add(group);
        _userServiceClientMock.Setup(x => x.GetUsersAsync(It.IsAny<UserListRequestDto>()))
            .ReturnsAsync(new List<UserProfileDto> { new UserProfileDto { Id = user.Id, Email = user.Email, FirstName = user.FirstName, LastName = user.LastName, KycStatus = user.KycStatus, Role = user.Role, CreatedAt = user.CreatedAt } });
        _groupServiceClientMock.Setup(x => x.GetGroupsAsync(It.IsAny<GroupListRequestDto>()))
            .ReturnsAsync(new List<GroupDto> { new GroupDto { Id = group.Id, Name = group.Name, Status = group.Status, CreatedBy = group.CreatedBy, CreatedAt = group.CreatedAt, Members = new List<GroupMemberDto>(), Vehicles = new List<VehicleDto>() } });
        // TODO: AdminDbContext no longer has Vehicles DbSet - use VehicleServiceClient mock instead
        // _context.Vehicles.Add(vehicle);
        // await _context.SaveChangesAsync();
        _vehicleServiceClientMock.Setup(x => x.GetVehiclesAsync())
            .ReturnsAsync(new List<VehicleDto> { new VehicleDto { Id = vehicle.Id, Vin = vehicle.Vin, PlateNumber = vehicle.PlateNumber, Model = vehicle.Model, Year = vehicle.Year, Status = vehicle.Status, GroupId = vehicle.GroupId, CreatedAt = vehicle.CreatedAt } });

        var bookings = new List<Booking>
        {
            new Booking { Id = Guid.NewGuid(), VehicleId = vehicle.Id, GroupId = group.Id, UserId = user.Id, StartAt = DateTime.UtcNow.AddDays(1), EndAt = DateTime.UtcNow.AddDays(2), Status = BookingStatus.Pending, CreatedAt = DateTime.UtcNow },
            new Booking { Id = Guid.NewGuid(), VehicleId = vehicle.Id, GroupId = group.Id, UserId = user.Id, StartAt = DateTime.UtcNow.AddDays(3), EndAt = DateTime.UtcNow.AddDays(4), Status = BookingStatus.Confirmed, CreatedAt = DateTime.UtcNow },
            new Booking { Id = Guid.NewGuid(), VehicleId = vehicle.Id, GroupId = group.Id, UserId = user.Id, StartAt = DateTime.UtcNow, EndAt = DateTime.UtcNow.AddDays(1), Status = BookingStatus.InProgress, CreatedAt = DateTime.UtcNow },
            new Booking { Id = Guid.NewGuid(), VehicleId = vehicle.Id, GroupId = group.Id, UserId = user.Id, StartAt = DateTime.UtcNow.AddDays(-10), EndAt = DateTime.UtcNow.AddDays(-9), Status = BookingStatus.Completed, CreatedAt = DateTime.UtcNow.AddDays(-12) },
            new Booking { Id = Guid.NewGuid(), VehicleId = vehicle.Id, GroupId = group.Id, UserId = user.Id, StartAt = DateTime.UtcNow.AddDays(5), EndAt = DateTime.UtcNow.AddDays(6), Status = BookingStatus.Cancelled, CreatedAt = DateTime.UtcNow }
        };
        // TODO: AdminDbContext no longer has Bookings DbSet - use BookingServiceClient mock instead
        // _context.Bookings.AddRange(bookings);
        // await _context.SaveChangesAsync();
        var testBookings = bookings.Select(b => new BookingDto { Id = b.Id, VehicleId = b.VehicleId, GroupId = b.GroupId, UserId = b.UserId, StartAt = b.StartAt, EndAt = b.EndAt, Status = b.Status, CreatedAt = b.CreatedAt, TripFeeAmount = 0 }).ToList();
        _bookingServiceClientMock.Setup(x => x.GetBookingsAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>()))
            .ReturnsAsync(testBookings);

        // Act
        var result = await _adminService.GetDashboardMetricsAsync(new DashboardRequestDto { Period = TimePeriod.Monthly });

        // Assert
        result.Bookings.TotalBookings.Should().Be(5);
        result.Bookings.PendingBookings.Should().Be(1);
        result.Bookings.ConfirmedBookings.Should().Be(1);
        result.Bookings.InProgressBookings.Should().Be(1);
        result.Bookings.CompletedBookings.Should().Be(1);
        result.Bookings.CancelledBookings.Should().Be(1);
        result.Bookings.ActiveTrips.Should().Be(1);
    }

    #endregion

    #region Financial Totals Accuracy Tests

    [Fact]
    public async Task FinancialOverview_TotalRevenue_CalculatedCorrectly()
    {
        // Arrange
        var payments = new List<Payment>
        {
            new Payment { Id = Guid.NewGuid(), PayerId = Guid.NewGuid(), Amount = 1000, Status = PaymentStatus.Completed, Method = PaymentMethod.CreditCard, CreatedAt = DateTime.UtcNow, PaidAt = DateTime.UtcNow },
            new Payment { Id = Guid.NewGuid(), PayerId = Guid.NewGuid(), Amount = 2000, Status = PaymentStatus.Completed, Method = PaymentMethod.CreditCard, CreatedAt = DateTime.UtcNow, PaidAt = DateTime.UtcNow },
            new Payment { Id = Guid.NewGuid(), PayerId = Guid.NewGuid(), Amount = 3000, Status = PaymentStatus.Failed, Method = PaymentMethod.CreditCard, CreatedAt = DateTime.UtcNow },
            new Payment { Id = Guid.NewGuid(), PayerId = Guid.NewGuid(), Amount = 500, Status = PaymentStatus.Completed, Method = PaymentMethod.CreditCard, CreatedAt = DateTime.UtcNow.AddDays(-35), PaidAt = DateTime.UtcNow.AddDays(-35) }
        };
        // TODO: AdminDbContext no longer has Payments DbSet - use PaymentServiceClient mock instead
        // _context.Payments.AddRange(payments);
        // await _context.SaveChangesAsync();
        var testPayments = payments.Select(p => new PaymentDto { Id = p.Id, InvoiceId = Guid.NewGuid(), PayerId = p.PayerId, Amount = p.Amount, Status = p.Status, Method = p.Method, CreatedAt = p.CreatedAt, PaidAt = p.PaidAt }).ToList();
        _paymentServiceClientMock.Setup(x => x.GetPaymentsAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<PaymentStatus?>()))
            .ReturnsAsync(testPayments);

        // Act
        var result = await _adminService.GetFinancialOverviewAsync();

        // Assert
        result.TotalRevenueAllTime.Should().Be(3500); // Only completed payments
        result.FailedPaymentsCount.Should().Be(1);
        result.FailedPaymentsAmount.Should().Be(3000);
    }

    [Fact]
    public async Task FinancialOverview_MonthlyRevenue_CalculatedCorrectly()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var payments = new List<Payment>
        {
            new Payment { Id = Guid.NewGuid(), PayerId = Guid.NewGuid(), Amount = 1000, Status = PaymentStatus.Completed, Method = PaymentMethod.CreditCard, CreatedAt = now.AddDays(-10), PaidAt = now.AddDays(-10) },
            new Payment { Id = Guid.NewGuid(), PayerId = Guid.NewGuid(), Amount = 2000, Status = PaymentStatus.Completed, Method = PaymentMethod.CreditCard, CreatedAt = now.AddDays(-5), PaidAt = now.AddDays(-5) },
            new Payment { Id = Guid.NewGuid(), PayerId = Guid.NewGuid(), Amount = 500, Status = PaymentStatus.Completed, Method = PaymentMethod.CreditCard, CreatedAt = now.AddDays(-35), PaidAt = now.AddDays(-35) }
        };

        var isolatedOptions = new DbContextOptionsBuilder<AdminDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        await using var isolatedContext = new AdminDbContext(isolatedOptions);
        using var isolatedCache = new MemoryCache(new MemoryCacheOptions());
        var isolatedService = new AdminService(
            isolatedContext,
            isolatedCache,
            _loggerMock.Object,
            _userServiceClientMock.Object,
            _groupServiceClientMock.Object,
            _vehicleServiceClientMock.Object,
            _bookingServiceClientMock.Object,
            _paymentServiceClientMock.Object);

        // TODO: Update test to use PaymentServiceClient mock instead of direct DB access
        // isolatedContext.Payments.AddRange(payments); // AdminDbContext no longer has Payments DbSet
        var testPayments = payments.Select(p => new PaymentDto 
        { 
            Id = p.Id, 
            InvoiceId = Guid.NewGuid(), 
            PayerId = p.PayerId, 
            Amount = p.Amount, 
            Status = p.Status, 
            Method = p.Method, 
            CreatedAt = p.CreatedAt, 
            PaidAt = p.PaidAt 
        }).ToList();
        _paymentServiceClientMock.Setup(x => x.GetPaymentsAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<PaymentStatus?>()))
            .ReturnsAsync(testPayments);

        // Act
        var result = await isolatedService.GetFinancialOverviewAsync();

        // Assert
        result.TotalRevenueMonth.Should().Be(3000); // Only payments in last 30 days
    }

    [Fact]
    public async Task FinancialOverview_PaymentSuccessRate_CalculatedCorrectly()
    {
        // Arrange
        var payments = new List<Payment>
        {
            new Payment { Id = Guid.NewGuid(), PayerId = Guid.NewGuid(), Amount = 1000, Status = PaymentStatus.Completed, Method = PaymentMethod.CreditCard, CreatedAt = DateTime.UtcNow, PaidAt = DateTime.UtcNow },
            new Payment { Id = Guid.NewGuid(), PayerId = Guid.NewGuid(), Amount = 1000, Status = PaymentStatus.Completed, Method = PaymentMethod.CreditCard, CreatedAt = DateTime.UtcNow, PaidAt = DateTime.UtcNow },
            new Payment { Id = Guid.NewGuid(), PayerId = Guid.NewGuid(), Amount = 1000, Status = PaymentStatus.Completed, Method = PaymentMethod.CreditCard, CreatedAt = DateTime.UtcNow, PaidAt = DateTime.UtcNow },
            new Payment { Id = Guid.NewGuid(), PayerId = Guid.NewGuid(), Amount = 1000, Status = PaymentStatus.Completed, Method = PaymentMethod.CreditCard, CreatedAt = DateTime.UtcNow, PaidAt = DateTime.UtcNow },
            new Payment { Id = Guid.NewGuid(), PayerId = Guid.NewGuid(), Amount = 1000, Status = PaymentStatus.Failed, Method = PaymentMethod.CreditCard, CreatedAt = DateTime.UtcNow }
        };
        // TODO: AdminDbContext no longer has Payments DbSet - use PaymentServiceClient mock instead
        // _context.Payments.AddRange(payments);
        // await _context.SaveChangesAsync();
        var testPayments = payments.Select(p => new PaymentDto { Id = p.Id, InvoiceId = Guid.NewGuid(), PayerId = p.PayerId, Amount = p.Amount, Status = p.Status, Method = p.Method, CreatedAt = p.CreatedAt, PaidAt = p.PaidAt }).ToList();
        _paymentServiceClientMock.Setup(x => x.GetPaymentsAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<PaymentStatus?>()))
            .ReturnsAsync(testPayments);

        // Act
        var result = await _adminService.GetFinancialOverviewAsync();

        // Assert
        result.PaymentSuccessRate.Should().BeApproximately(80.0, 0.1); // 4 out of 5 successful
    }

    [Fact]
    public async Task FinancialByGroups_ExpenseTotals_MatchDatabase()
    {
        // Arrange
        var group1 = new OwnershipGroup { Id = Guid.NewGuid(), Name = "Group 1", Status = GroupStatus.Active, CreatedBy = Guid.NewGuid(), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var group2 = new OwnershipGroup { Id = Guid.NewGuid(), Name = "Group 2", Status = GroupStatus.Active, CreatedBy = Guid.NewGuid(), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        // TODO: AdminDbContext no longer has OwnershipGroups DbSet - use GroupServiceClient mock instead
        // _context.OwnershipGroups.AddRange(group1, group2);
        // await _context.SaveChangesAsync();
        var testGroups = new[] { group1, group2 }.Select(g => new GroupDto { Id = g.Id, Name = g.Name, Status = g.Status, CreatedBy = g.CreatedBy, CreatedAt = g.CreatedAt, Members = new List<GroupMemberDto>(), Vehicles = new List<VehicleDto>() }).ToList();
        _groupServiceClientMock.Setup(x => x.GetGroupsAsync(It.IsAny<GroupListRequestDto>()))
            .ReturnsAsync(testGroups);

        var expenses = new List<Expense>
        {
            new Expense { Id = Guid.NewGuid(), GroupId = group1.Id, ExpenseType = ExpenseType.Fuel, Amount = 1000, Description = "Fuel", CreatedBy = Guid.NewGuid(), CreatedAt = DateTime.UtcNow },
            new Expense { Id = Guid.NewGuid(), GroupId = group1.Id, ExpenseType = ExpenseType.Maintenance, Amount = 2000, Description = "Maintenance", CreatedBy = Guid.NewGuid(), CreatedAt = DateTime.UtcNow },
            new Expense { Id = Guid.NewGuid(), GroupId = group2.Id, ExpenseType = ExpenseType.Fuel, Amount = 500, Description = "Fuel", CreatedBy = Guid.NewGuid(), CreatedAt = DateTime.UtcNow }
        };
        // TODO: AdminDbContext no longer has Expenses DbSet - use PaymentServiceClient mock instead
        // _context.Expenses.AddRange(expenses);
        // await _context.SaveChangesAsync();
        var testExpenses = expenses.Select(e => new ExpenseDto { Id = e.Id, GroupId = e.GroupId, VehicleId = e.VehicleId, ExpenseType = e.ExpenseType, Amount = e.Amount, Description = e.Description, DateIncurred = e.DateIncurred, CreatedBy = e.CreatedBy, CreatedAt = e.CreatedAt }).ToList();
        _paymentServiceClientMock.Setup(x => x.GetExpensesAsync(It.IsAny<Guid?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(testExpenses);

        // Act
        var result = await _adminService.GetFinancialByGroupsAsync();

        // Assert
        result.Groups.Should().HaveCount(2);
        var group1Financial = result.Groups.First(g => g.GroupId == group1.Id);
        group1Financial.TotalExpenses.Should().Be(3000);
        
        var group2Financial = result.Groups.First(g => g.GroupId == group2.Id);
        group2Financial.TotalExpenses.Should().Be(500);
    }

    #endregion

    #region Audit Trail Completeness Tests

    [Fact]
    public async Task UserStatusUpdate_CreatesAuditLog()
    {
        // Arrange
        var user = new User { Id = Guid.NewGuid(), Email = "user@test.com", FirstName = "User", LastName = "Test", KycStatus = KycStatus.Approved, Role = UserRole.CoOwner, CreatedAt = DateTime.UtcNow, LockoutEnd = null };
        var adminUser = new User { Id = Guid.NewGuid(), Email = "admin@test.com", FirstName = "Admin", LastName = "User", KycStatus = KycStatus.Approved, Role = UserRole.SystemAdmin, CreatedAt = DateTime.UtcNow };
        // TODO: AdminDbContext no longer has Users DbSet - use UserServiceClient mock instead
        // _context.Users.AddRange(user, adminUser);
        // await _context.SaveChangesAsync();
        _userServiceClientMock.Setup(x => x.GetUsersAsync(It.IsAny<UserListRequestDto>()))
            .ReturnsAsync(new List<UserProfileDto> 
            { 
                new UserProfileDto { Id = user.Id, Email = user.Email, FirstName = user.FirstName, LastName = user.LastName, KycStatus = user.KycStatus, Role = user.Role, CreatedAt = user.CreatedAt },
                new UserProfileDto { Id = adminUser.Id, Email = adminUser.Email, FirstName = adminUser.FirstName, LastName = adminUser.LastName, KycStatus = adminUser.KycStatus, Role = adminUser.Role, CreatedAt = adminUser.CreatedAt }
            });

        var request = new UpdateUserStatusDto
        {
            Status = UserAccountStatus.Suspended,
            Reason = "Policy violation"
        };

        // Act
        await _adminService.UpdateUserStatusAsync(user.Id, request, adminUser.Id);

        // Assert
        var auditLog = await _context.AuditLogs
            .FirstOrDefaultAsync(al => al.EntityId == user.Id && al.Action == "StatusUpdated");
        auditLog.Should().NotBeNull();
        auditLog!.PerformedBy.Should().Be(adminUser.Id);
        auditLog.Details.Should().Contain("Suspended");
        auditLog.Details.Should().Contain("Policy violation");
    }

    [Fact]
    public async Task UserRoleUpdate_CreatesAuditLog()
    {
        // Arrange
        var user = new User { Id = Guid.NewGuid(), Email = "user@test.com", FirstName = "User", LastName = "Test", KycStatus = KycStatus.Approved, Role = UserRole.CoOwner, CreatedAt = DateTime.UtcNow };
        var adminUser = new User { Id = Guid.NewGuid(), Email = "admin@test.com", FirstName = "Admin", LastName = "User", KycStatus = KycStatus.Approved, Role = UserRole.SystemAdmin, CreatedAt = DateTime.UtcNow };
        // TODO: AdminDbContext no longer has Users DbSet - use UserServiceClient mock instead
        // _context.Users.AddRange(user, adminUser);
        // await _context.SaveChangesAsync();
        _userServiceClientMock.Setup(x => x.GetUsersAsync(It.IsAny<UserListRequestDto>()))
            .ReturnsAsync(new List<UserProfileDto> 
            { 
                new UserProfileDto { Id = user.Id, Email = user.Email, FirstName = user.FirstName, LastName = user.LastName, KycStatus = user.KycStatus, Role = user.Role, CreatedAt = user.CreatedAt },
                new UserProfileDto { Id = adminUser.Id, Email = adminUser.Email, FirstName = adminUser.FirstName, LastName = adminUser.LastName, KycStatus = adminUser.KycStatus, Role = adminUser.Role, CreatedAt = adminUser.CreatedAt }
            });

        var request = new UpdateUserRoleDto
        {
            Role = UserRole.GroupAdmin,
            Reason = "Promotion"
        };

        // Act
        await _adminService.UpdateUserRoleAsync(user.Id, request, adminUser.Id);

        // Assert
        var auditLog = await _context.AuditLogs
            .FirstOrDefaultAsync(al => al.EntityId == user.Id && al.Action == "RoleUpdated");
        auditLog.Should().NotBeNull();
        auditLog!.PerformedBy.Should().Be(adminUser.Id);
        auditLog.Details.Should().Contain("GroupAdmin");
        auditLog.Details.Should().Contain("Promotion");
    }

    [Fact]
    public async Task GroupStatusUpdate_CreatesAuditLog()
    {
        // Arrange
        var group = new OwnershipGroup { Id = Guid.NewGuid(), Name = "Test Group", Status = GroupStatus.Active, CreatedBy = Guid.NewGuid(), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var adminUser = new User { Id = Guid.NewGuid(), Email = "admin@test.com", FirstName = "Admin", LastName = "User", KycStatus = KycStatus.Approved, Role = UserRole.SystemAdmin, CreatedAt = DateTime.UtcNow };
        // TODO: AdminDbContext no longer has OwnershipGroups/Users DbSets - use HTTP client mocks instead
        // _context.OwnershipGroups.Add(group);
        // _context.Users.Add(adminUser);
        _groupServiceClientMock.Setup(x => x.GetGroupsAsync(It.IsAny<GroupListRequestDto>()))
            .ReturnsAsync(new List<GroupDto> { new GroupDto { Id = group.Id, Name = group.Name, Status = group.Status, CreatedBy = group.CreatedBy, CreatedAt = group.CreatedAt, Members = new List<GroupMemberDto>(), Vehicles = new List<VehicleDto>() } });
        _userServiceClientMock.Setup(x => x.GetUsersAsync(It.IsAny<UserListRequestDto>()))
            .ReturnsAsync(new List<UserProfileDto> { new UserProfileDto { Id = adminUser.Id, Email = adminUser.Email, FirstName = adminUser.FirstName, LastName = adminUser.LastName, KycStatus = adminUser.KycStatus, Role = adminUser.Role, CreatedAt = adminUser.CreatedAt } });
        // await _context.SaveChangesAsync();
        _userServiceClientMock.Setup(x => x.GetUsersAsync(It.IsAny<UserListRequestDto>()))
            .ReturnsAsync(new List<UserProfileDto> { new UserProfileDto { Id = adminUser.Id, Email = adminUser.Email, FirstName = adminUser.FirstName, LastName = adminUser.LastName, KycStatus = adminUser.KycStatus, Role = adminUser.Role, CreatedAt = adminUser.CreatedAt } });

        var request = new UpdateGroupStatusDto
        {
            Status = GroupStatus.Inactive,
            Reason = "Maintenance"
        };

        // Act
        await _adminService.UpdateGroupStatusAsync(group.Id, request, adminUser.Id);

        // Assert
        var auditLog = await _context.AuditLogs
            .FirstOrDefaultAsync(al => al.EntityId == group.Id && al.Action == "StatusUpdated");
        auditLog.Should().NotBeNull();
        auditLog!.PerformedBy.Should().Be(adminUser.Id);
        auditLog.Details.Should().Contain("Inactive");
        auditLog.Details.Should().Contain("Maintenance");
    }

    [Fact]
    public async Task DisputeCreation_CreatesAuditLog()
    {
        // Arrange
        var group = new OwnershipGroup { Id = Guid.NewGuid(), Name = "Test Group", Status = GroupStatus.Active, CreatedBy = Guid.NewGuid(), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var adminUser = new User { Id = Guid.NewGuid(), Email = "admin@test.com", FirstName = "Admin", LastName = "User", KycStatus = KycStatus.Approved, Role = UserRole.SystemAdmin, CreatedAt = DateTime.UtcNow };
        // TODO: AdminDbContext no longer has OwnershipGroups/Users DbSets - use HTTP client mocks instead
        // _context.OwnershipGroups.Add(group);
        // _context.Users.Add(adminUser);
        _groupServiceClientMock.Setup(x => x.GetGroupsAsync(It.IsAny<GroupListRequestDto>()))
            .ReturnsAsync(new List<GroupDto> { new GroupDto { Id = group.Id, Name = group.Name, Status = group.Status, CreatedBy = group.CreatedBy, CreatedAt = group.CreatedAt, Members = new List<GroupMemberDto>(), Vehicles = new List<VehicleDto>() } });
        _userServiceClientMock.Setup(x => x.GetUsersAsync(It.IsAny<UserListRequestDto>()))
            .ReturnsAsync(new List<UserProfileDto> { new UserProfileDto { Id = adminUser.Id, Email = adminUser.Email, FirstName = adminUser.FirstName, LastName = adminUser.LastName, KycStatus = adminUser.KycStatus, Role = adminUser.Role, CreatedAt = adminUser.CreatedAt } });
        // await _context.SaveChangesAsync();
        _userServiceClientMock.Setup(x => x.GetUsersAsync(It.IsAny<UserListRequestDto>()))
            .ReturnsAsync(new List<UserProfileDto> { new UserProfileDto { Id = adminUser.Id, Email = adminUser.Email, FirstName = adminUser.FirstName, LastName = adminUser.LastName, KycStatus = adminUser.KycStatus, Role = adminUser.Role, CreatedAt = adminUser.CreatedAt } });

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
        var auditLog = await _context.AuditLogs
            .FirstOrDefaultAsync(al => al.EntityId == disputeId && al.Action == "DisputeCreated");
        auditLog.Should().NotBeNull();
        auditLog!.PerformedBy.Should().Be(adminUser.Id);
        auditLog.Details.Should().Contain("Test Dispute");
    }

    [Fact]
    public async Task DisputeResolution_CreatesAuditLog()
    {
        // Arrange
        var group = new OwnershipGroup { Id = Guid.NewGuid(), Name = "Test Group", Status = GroupStatus.Active, CreatedBy = Guid.NewGuid(), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var adminUser = new User { Id = Guid.NewGuid(), Email = "admin@test.com", FirstName = "Admin", LastName = "User", KycStatus = KycStatus.Approved, Role = UserRole.SystemAdmin, CreatedAt = DateTime.UtcNow };
        // TODO: AdminDbContext no longer has OwnershipGroups/Users DbSets - use HTTP client mocks instead
        // _context.OwnershipGroups.Add(group);
        // _context.Users.Add(adminUser);
        _groupServiceClientMock.Setup(x => x.GetGroupsAsync(It.IsAny<GroupListRequestDto>()))
            .ReturnsAsync(new List<GroupDto> { new GroupDto { Id = group.Id, Name = group.Name, Status = group.Status, CreatedBy = group.CreatedBy, CreatedAt = group.CreatedAt, Members = new List<GroupMemberDto>(), Vehicles = new List<VehicleDto>() } });
        _userServiceClientMock.Setup(x => x.GetUsersAsync(It.IsAny<UserListRequestDto>()))
            .ReturnsAsync(new List<UserProfileDto> { new UserProfileDto { Id = adminUser.Id, Email = adminUser.Email, FirstName = adminUser.FirstName, LastName = adminUser.LastName, KycStatus = adminUser.KycStatus, Role = adminUser.Role, CreatedAt = adminUser.CreatedAt } });
        // await _context.SaveChangesAsync();
        _userServiceClientMock.Setup(x => x.GetUsersAsync(It.IsAny<UserListRequestDto>()))
            .ReturnsAsync(new List<UserProfileDto> { new UserProfileDto { Id = adminUser.Id, Email = adminUser.Email, FirstName = adminUser.FirstName, LastName = adminUser.LastName, KycStatus = adminUser.KycStatus, Role = adminUser.Role, CreatedAt = adminUser.CreatedAt } });

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
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Disputes.Add(dispute);
        await _context.SaveChangesAsync();

        var request = new ResolveDisputeDto
        {
            Resolution = "Resolved",
            Note = "Issue addressed"
        };

        // Act
        await _adminService.ResolveDisputeAsync(dispute.Id, request, adminUser.Id);

        // Assert
        var auditLog = await _context.AuditLogs
            .FirstOrDefaultAsync(al => al.EntityId == dispute.Id && al.Action == "DisputeResolved");
        auditLog.Should().NotBeNull();
        auditLog!.PerformedBy.Should().Be(adminUser.Id);
        auditLog.Details.Should().Contain("Resolved");
    }

    #endregion
}

