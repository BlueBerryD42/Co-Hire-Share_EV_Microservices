using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using CoOwnershipVehicle.Analytics.Api.Services;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Analytics.Api.Data;
using CoOwnershipVehicle.Data;
using Microsoft.EntityFrameworkCore;

namespace CoOwnershipVehicle.Analytics.Api.Tests.PerformanceTests;

public class AIServicePerformanceTests : IDisposable
{
    private readonly AnalyticsDbContext _analyticsContext;
    private readonly ApplicationDbContext _mainContext;
    private readonly Mock<ILogger<AIService>> _loggerMock;
    private readonly AIService _aiService;

    public AIServicePerformanceTests()
    {
        var analyticsOptions = new DbContextOptionsBuilder<AnalyticsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var mainOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _analyticsContext = new AnalyticsDbContext(analyticsOptions);
        _mainContext = new ApplicationDbContext(mainOptions);
        _loggerMock = new Mock<ILogger<AIService>>();
        _aiService = new AIService(_analyticsContext, _mainContext, _loggerMock.Object);
    }

    public void Dispose()
    {
        _analyticsContext.Database.EnsureDeleted();
        _mainContext.Database.EnsureDeleted();
        _analyticsContext.Dispose();
        _mainContext.Dispose();
    }

    [Fact]
    public async Task CalculateFairness_With1000Bookings_CompletesWithin5Seconds()
    {
        // Arrange
        var groupId = await CreateLargeDatasetGroup(1000, 5);

        // Act
        var startTime = DateTime.UtcNow;
        var result = await _aiService.CalculateFairnessAsync(groupId, null, null);
        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        result.Should().NotBeNull();
        elapsed.TotalSeconds.Should().BeLessThan(5, "Fairness calculation with 1000 bookings should complete within 5 seconds");
    }

    [Fact]
    public async Task SuggestBookingTimes_With1000Bookings_CompletesWithin5Seconds()
    {
        // Arrange
        var groupId = await CreateLargeDatasetGroup(1000, 5);
        var request = new CoOwnershipVehicle.Analytics.Api.Models.SuggestBookingRequest
        {
            UserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            GroupId = groupId,
            DurationMinutes = 120
        };

        // Act
        var startTime = DateTime.UtcNow;
        var result = await _aiService.SuggestBookingTimesAsync(request);
        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        result.Should().NotBeNull();
        elapsed.TotalSeconds.Should().BeLessThan(5, "Booking suggestions with 1000 bookings should complete within 5 seconds");
    }

    [Fact]
    public async Task GetUsagePredictions_With1000Bookings_CompletesWithin5Seconds()
    {
        // Arrange
        var groupId = await CreateLargeDatasetGroup(1000, 5);

        // Act
        var startTime = DateTime.UtcNow;
        var result = await _aiService.GetUsagePredictionsAsync(groupId);
        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        result.Should().NotBeNull();
        elapsed.TotalSeconds.Should().BeLessThan(5, "Usage predictions with 1000 bookings should complete within 5 seconds");
    }

    [Fact]
    public async Task GetCostOptimization_With1000Bookings_CompletesWithin5Seconds()
    {
        // Arrange
        var groupId = await CreateLargeDatasetGroup(1000, 5);
        await AddLargeExpenseDataset(groupId, 200);

        // Act
        var startTime = DateTime.UtcNow;
        var result = await _aiService.GetCostOptimizationAsync(groupId);
        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        result.Should().NotBeNull();
        elapsed.TotalSeconds.Should().BeLessThan(5, "Cost optimization with 1000 bookings should complete within 5 seconds");
    }

    [Fact]
    public async Task CalculateFairness_WithLargeGroup_CompletesWithin5Seconds()
    {
        // Arrange
        var groupId = await CreateLargeGroupDataset(20, 500);

        // Act
        var startTime = DateTime.UtcNow;
        var result = await _aiService.CalculateFairnessAsync(groupId, null, null);
        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        result.Should().NotBeNull();
        elapsed.TotalSeconds.Should().BeLessThan(5, "Fairness calculation with 20 members should complete within 5 seconds");
    }

    [Fact]
    public async Task GetUsagePredictions_WithExtendedHistory_CompletesWithin5Seconds()
    {
        // Arrange
        var groupId = await CreateExtendedHistoryDataset(365, 3);

        // Act
        var startTime = DateTime.UtcNow;
        var result = await _aiService.GetUsagePredictionsAsync(groupId);
        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        result.Should().NotBeNull();
        elapsed.TotalSeconds.Should().BeLessThan(5, "Usage predictions with 365 days of history should complete within 5 seconds");
    }

    [Fact]
    public async Task CalculateFairness_MultipleConcurrentCalls_PerformsAdequately()
    {
        // Arrange
        var groupId = await CreateLargeDatasetGroup(500, 5);
        var tasks = new List<Task>();

        // Act
        var startTime = DateTime.UtcNow;
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(_aiService.CalculateFairnessAsync(groupId, null, null));
        }
        await Task.WhenAll(tasks);
        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        elapsed.TotalSeconds.Should().BeLessThan(10, "10 concurrent fairness calculations should complete within 10 seconds");
    }

    [Fact]
    public async Task GetCostOptimization_WithComplexExpenseData_CompletesWithin5Seconds()
    {
        // Arrange
        var groupId = await CreateLargeDatasetGroup(500, 5);
        await AddComplexExpenseDataset(groupId);

        // Act
        var startTime = DateTime.UtcNow;
        var result = await _aiService.GetCostOptimizationAsync(groupId);
        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        result.Should().NotBeNull();
        elapsed.TotalSeconds.Should().BeLessThan(5, "Cost optimization with complex data should complete within 5 seconds");
    }

    [Fact]
    public async Task SuggestBookingTimes_WithComplexFairnessData_CompletesWithin5Seconds()
    {
        // Arrange
        var groupId = await CreateComplexFairnessDataset(10, 300);
        var request = new CoOwnershipVehicle.Analytics.Api.Models.SuggestBookingRequest
        {
            UserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            GroupId = groupId,
            DurationMinutes = 120
        };

        // Act
        var startTime = DateTime.UtcNow;
        var result = await _aiService.SuggestBookingTimesAsync(request);
        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        result.Should().NotBeNull();
        elapsed.TotalSeconds.Should().BeLessThan(5, "Booking suggestions with complex fairness data should complete within 5 seconds");
    }

    // Helper methods for performance test data

    private async Task<Guid> CreateLargeDatasetGroup(int bookingCount, int memberCount)
    {
        var groupId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var users = new List<User>();
        var bookings = new List<Booking>();

        // Create users
        for (int i = 0; i < memberCount; i++)
        {
            users.Add(new User
            {
                Id = Guid.NewGuid(),
                Email = $"user{i}@perfgroup.com",
                FirstName = "User",
                LastName = $"{i}",
                KycStatus = KycStatus.Approved,
                Role = UserRole.CoOwner,
                CreatedAt = DateTime.UtcNow.AddMonths(-6)
            });
        }

        // Create group
        var group = new OwnershipGroup
        {
            Id = groupId,
            Name = $"Performance Test Group ({memberCount} members)",
            Status = GroupStatus.Active,
            CreatedBy = users[0].Id,
            CreatedAt = DateTime.UtcNow.AddMonths(-6),
            Members = users.Select((u, i) => new GroupMember
            {
                UserId = u.Id,
                SharePercentage = 1.0m / memberCount,
                Status = MembershipStatus.Active,
                CreatedAt = DateTime.UtcNow.AddMonths(-6)
            }).ToList()
        };

        // Create vehicle
        var vehicle = new Vehicle
        {
            Id = vehicleId,
            GroupId = groupId,
            Vin = $"PERFVIN{groupId}",
            PlateNumber = $"PERF{groupId}",
            Make = "Tesla",
            Model = "Model 3",
            Year = 2023,
            Status = VehicleStatus.Available,
            CreatedAt = DateTime.UtcNow.AddMonths(-6)
        };

        // Create bookings
        var startDate = DateTime.UtcNow.AddDays(-bookingCount / 10);
        for (int i = 0; i < bookingCount; i++)
        {
            var userId = users[i % memberCount].Id;
            var bookingStart = startDate.AddHours(i * 2);
            bookings.Add(new Booking
            {
                Id = Guid.NewGuid(),
                GroupId = groupId,
                VehicleId = vehicleId,
                UserId = userId,
                StartAt = bookingStart,
                EndAt = bookingStart.AddHours(8),
                Status = BookingStatus.Completed,
                CreatedAt = bookingStart
            });
        }

        _mainContext.Users.AddRange(users);
        _mainContext.OwnershipGroups.Add(group);
        _mainContext.Vehicles.Add(vehicle);
        _mainContext.Bookings.AddRange(bookings);
        await _mainContext.SaveChangesAsync();

        // Create analytics snapshots
        var snapshots = new List<AnalyticsSnapshot>();
        for (int i = 0; i < bookingCount / 50; i++)
        {
            snapshots.Add(new AnalyticsSnapshot
            {
                Id = Guid.NewGuid(),
                GroupId = groupId,
                VehicleId = vehicleId,
                SnapshotDate = DateTime.UtcNow.AddDays(-bookingCount / 100 + i),
                Period = AnalyticsPeriod.Daily,
                TotalUsageHours = 100,
                TotalBookings = 10,
                ActiveUsers = memberCount
            });
        }

        _analyticsContext.AnalyticsSnapshots.AddRange(snapshots);
        await _analyticsContext.SaveChangesAsync();

        return groupId;
    }

    private async Task AddLargeExpenseDataset(Guid groupId, int expenseCount)
    {
        var expenses = new List<Expense>();
        var expenseTypes = Enum.GetValues(typeof(ExpenseType)).Cast<ExpenseType>().ToArray();

        for (int i = 0; i < expenseCount; i++)
        {
            expenses.Add(new Expense
            {
                Id = Guid.NewGuid(),
                GroupId = groupId,
                VehicleId = Guid.NewGuid(),
                ExpenseType = expenseTypes[i % expenseTypes.Length],
                Amount = 50 + (i * 10),
                Description = $"Expense {i}",
                DateIncurred = DateTime.UtcNow.AddDays(-i),
                CreatedBy = Guid.NewGuid(),
                IsRecurring = i % 10 == 0
            });
        }

        _mainContext.Expenses.AddRange(expenses);
        await _mainContext.SaveChangesAsync();
    }

    private async Task<Guid> CreateLargeGroupDataset(int memberCount, int bookingsPerMember)
    {
        var groupId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var users = new List<User>();
        var bookings = new List<Booking>();

        // Create users
        for (int i = 0; i < memberCount; i++)
        {
            users.Add(new User
            {
                Id = Guid.NewGuid(),
                Email = $"largeuser{i}@perfgroup.com",
                FirstName = "Large",
                LastName = $"User{i}",
                KycStatus = KycStatus.Approved,
                Role = UserRole.CoOwner,
                CreatedAt = DateTime.UtcNow.AddMonths(-6)
            });
        }

        // Create group
        var group = new OwnershipGroup
        {
            Id = groupId,
            Name = $"Large Group ({memberCount} members)",
            Status = GroupStatus.Active,
            CreatedBy = users[0].Id,
            CreatedAt = DateTime.UtcNow.AddMonths(-6),
            Members = users.Select((u, i) => new GroupMember
            {
                UserId = u.Id,
                SharePercentage = 1.0m / memberCount,
                Status = MembershipStatus.Active,
                CreatedAt = DateTime.UtcNow.AddMonths(-6)
            }).ToList()
        };

        // Create vehicle
        var vehicle = new Vehicle
        {
            Id = vehicleId,
            GroupId = groupId,
            Vin = $"LARGEVIN{groupId}",
            PlateNumber = $"LG{groupId}",
            Make = "Tesla",
            Model = "Model 3",
            Year = 2023,
            Status = VehicleStatus.Available,
            CreatedAt = DateTime.UtcNow.AddMonths(-6)
        };

        // Create bookings
        var totalBookings = memberCount * bookingsPerMember;
        for (int i = 0; i < totalBookings; i++)
        {
            var userId = users[i % memberCount].Id;
            var bookingStart = DateTime.UtcNow.AddDays(-totalBookings / 10 + i);
            bookings.Add(new Booking
            {
                Id = Guid.NewGuid(),
                GroupId = groupId,
                VehicleId = vehicleId,
                UserId = userId,
                StartAt = bookingStart,
                EndAt = bookingStart.AddHours(8),
                Status = BookingStatus.Completed,
                CreatedAt = bookingStart
            });
        }

        _mainContext.Users.AddRange(users);
        _mainContext.OwnershipGroups.Add(group);
        _mainContext.Vehicles.Add(vehicle);
        _mainContext.Bookings.AddRange(bookings);
        await _mainContext.SaveChangesAsync();

        return groupId;
    }

    private async Task<Guid> CreateExtendedHistoryDataset(int days, int members)
    {
        var groupId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var users = new List<User>();
        var snapshots = new List<AnalyticsSnapshot>();

        // Create users
        for (int i = 0; i < members; i++)
        {
            users.Add(new User
            {
                Id = Guid.NewGuid(),
                Email = $"hist{i}@perfgroup.com",
                FirstName = "History",
                LastName = $"User{i}",
                KycStatus = KycStatus.Approved,
                Role = UserRole.CoOwner,
                CreatedAt = DateTime.UtcNow.AddDays(-days)
            });
        }

        // Create group
        var group = new OwnershipGroup
        {
            Id = groupId,
            Name = "Extended History Group",
            Status = GroupStatus.Active,
            CreatedBy = users[0].Id,
            CreatedAt = DateTime.UtcNow.AddDays(-days),
            Members = users.Select((u, i) => new GroupMember
            {
                UserId = u.Id,
                SharePercentage = 1.0m / members,
                Status = MembershipStatus.Active,
                CreatedAt = DateTime.UtcNow.AddDays(-days)
            }).ToList()
        };

        // Create vehicle
        var vehicle = new Vehicle
        {
            Id = vehicleId,
            GroupId = groupId,
            Vin = "HISTORYVIN",
            PlateNumber = "HIST",
            Make = "Tesla",
            Model = "Model 3",
            Year = 2023,
            Status = VehicleStatus.Available,
            CreatedAt = DateTime.UtcNow.AddDays(-days)
        };

        _mainContext.Users.AddRange(users);
        _mainContext.OwnershipGroups.Add(group);
        _mainContext.Vehicles.Add(vehicle);
        await _mainContext.SaveChangesAsync();

        // Create daily snapshots
        for (int i = 0; i < days; i++)
        {
            snapshots.Add(new AnalyticsSnapshot
            {
                Id = Guid.NewGuid(),
                GroupId = groupId,
                VehicleId = vehicleId,
                SnapshotDate = DateTime.UtcNow.AddDays(-days + i),
                Period = AnalyticsPeriod.Daily,
                TotalUsageHours = 100 + (i % 50),
                TotalBookings = 10 + (i % 10),
                ActiveUsers = members
            });
        }

        _analyticsContext.AnalyticsSnapshots.AddRange(snapshots);
        await _analyticsContext.SaveChangesAsync();

        return groupId;
    }

    private async Task AddComplexExpenseDataset(Guid groupId)
    {
        var expenses = new List<Expense>();
        var expenseTypes = Enum.GetValues(typeof(ExpenseType)).Cast<ExpenseType>().ToArray();

        // Create varied expense patterns
        for (int i = 0; i < 100; i++)
        {
            expenses.Add(new Expense
            {
                Id = Guid.NewGuid(),
                GroupId = groupId,
                ExpenseType = expenseTypes[i % expenseTypes.Length],
                Amount = 25 + (i % 200),
                Description = $"Complex expense pattern {i} at Service Center {i % 10}",
                DateIncurred = DateTime.UtcNow.AddMonths(-12).AddDays(i),
                CreatedBy = Guid.NewGuid(),
                IsRecurring = i % 12 == 0,
                Notes = $"Provider pattern notes {i}"
            });
        }

        _mainContext.Expenses.AddRange(expenses);
        await _mainContext.SaveChangesAsync();
    }

    private async Task<Guid> CreateComplexFairnessDataset(int members, int bookings)
    {
        var groupId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var users = new List<User>();
        var bookingsList = new List<Booking>();

        // Create users with varying patterns
        for (int i = 0; i < members; i++)
        {
            users.Add(new User
            {
                Id = Guid.NewGuid(),
                Email = $"complex{i}@perfgroup.com",
                FirstName = "Complex",
                LastName = $"User{i}",
                KycStatus = KycStatus.Approved,
                Role = UserRole.CoOwner,
                CreatedAt = DateTime.UtcNow.AddMonths(-6)
            });
        }

        // Create group
        var group = new OwnershipGroup
        {
            Id = groupId,
            Name = "Complex Fairness Group",
            Status = GroupStatus.Active,
            CreatedBy = users[0].Id,
            CreatedAt = DateTime.UtcNow.AddMonths(-6),
            Members = users.Select((u, i) => new GroupMember
            {
                UserId = u.Id,
                SharePercentage = 1.0m / members,
                Status = MembershipStatus.Active,
                CreatedAt = DateTime.UtcNow.AddMonths(-6)
            }).ToList()
        };

        // Create vehicle
        var vehicle = new Vehicle
        {
            Id = vehicleId,
            GroupId = groupId,
            Vin = "COMPLEXVIN",
            PlateNumber = "COMPL",
            Make = "Tesla",
            Model = "Model 3",
            Year = 2023,
            Status = VehicleStatus.Available,
            CreatedAt = DateTime.UtcNow.AddMonths(-6)
        };

        // Create varied bookings to simulate complex patterns
        for (int i = 0; i < bookings; i++)
        {
            var userId = users[i % members].Id;
            var bookingStart = DateTime.UtcNow.AddDays(-bookings / 20 + i);
            bookingsList.Add(new Booking
            {
                Id = Guid.NewGuid(),
                GroupId = groupId,
                VehicleId = vehicleId,
                UserId = userId,
                StartAt = bookingStart,
                EndAt = bookingStart.AddHours(4 + (i % 12)),
                Status = BookingStatus.Completed,
                CreatedAt = bookingStart
            });
        }

        _mainContext.Users.AddRange(users);
        _mainContext.OwnershipGroups.Add(group);
        _mainContext.Vehicles.Add(vehicle);
        _mainContext.Bookings.AddRange(bookingsList);
        await _mainContext.SaveChangesAsync();

        return groupId;
    }
}

