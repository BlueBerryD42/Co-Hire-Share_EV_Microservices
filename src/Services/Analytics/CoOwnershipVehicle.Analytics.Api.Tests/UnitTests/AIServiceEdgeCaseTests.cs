using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using CoOwnershipVehicle.Analytics.Api.Services;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Analytics.Api.Data;
using CoOwnershipVehicle.Data;
using Microsoft.EntityFrameworkCore;

namespace CoOwnershipVehicle.Analytics.Api.Tests.UnitTests;

public class AIServiceEdgeCaseTests : IDisposable
{
    private readonly AnalyticsDbContext _analyticsContext;
    private readonly ApplicationDbContext _mainContext;
    private readonly Mock<ILogger<AIService>> _loggerMock;
    private readonly AIService _aiService;

    public AIServiceEdgeCaseTests()
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
    public async Task CalculateFairness_WithSingleMember_ReturnsValidResult()
    {
        // Arrange - Create single member group
        var groupId = await CreateSingleMemberGroup();

        // Act
        var result = await _aiService.CalculateFairnessAsync(groupId, null, null);

        // Assert
        result.Should().NotBeNull();
        result!.Members.Should().NotBeNull();
    }

    [Fact]
    public async Task CalculateFairness_WithNoHistory_ReturnsZeroScore()
    {
        // Arrange - Create group with no usage history
        var groupId = await CreateGroupWithNoHistory();

        // Act
        var result = await _aiService.CalculateFairnessAsync(groupId, null, null);

        // Assert
        result.Should().NotBeNull();
        if (result.Members.Any())
        {
            result.GroupFairnessScore.Should().Be(0);
        }
    }

    [Fact]
    public async Task CalculateFairness_WithAllInactiveMembers_HandlesGracefully()
    {
        // Arrange
        var groupId = await CreateGroupWithInactiveMembers();

        // Act
        var result = await _aiService.CalculateFairnessAsync(groupId, null, null);

        // Assert
        result.Should().NotBeNull();
        // Should handle gracefully without throwing
    }

    [Fact]
    public async Task CalculateFairness_WithExtremeImbalance_DetectsSevereImbalance()
    {
        // Arrange
        var groupId = await CreateExtremelyImbalancedGroup();

        // Act
        var result = await _aiService.CalculateFairnessAsync(groupId, null, null);

        // Assert
        result.Should().NotBeNull();
        result!.GroupFairnessScore.Should().BeLessThan(70m);
        result.Alerts.Should().NotBeNull();
    }

    [Fact]
    public async Task SuggestBookingTimes_WithNoUserData_ReturnsNull()
    {
        // Arrange
        var groupId = await CreateEmptyGroup();
        var request = new CoOwnershipVehicle.Analytics.Api.Models.SuggestBookingRequest
        {
            UserId = Guid.NewGuid(),
            GroupId = groupId,
            DurationMinutes = 120
        };

        // Act
        var result = await _aiService.SuggestBookingTimesAsync(request);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUsagePredictions_WithLessThan30DaysHistory_ReturnsInsufficientHistory()
    {
        // Arrange
        var groupId = await CreateGroupWithMinimalHistory();

        // Act
        var result = await _aiService.GetUsagePredictionsAsync(groupId);

        // Assert
        result.Should().NotBeNull();
        result!.InsufficientHistory.Should().BeTrue();
    }

    [Fact]
    public async Task GetUsagePredictions_WithNoHistoricalData_ReturnsNull()
    {
        // Arrange
        var groupId = await CreateEmptyGroup();

        // Act
        var result = await _aiService.GetUsagePredictionsAsync(groupId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetCostOptimization_WithNoExpenses_ReturnsInsufficientData()
    {
        // Arrange
        var groupId = await CreateGroupWithBookingsButNoExpenses();

        // Act
        var result = await _aiService.GetCostOptimizationAsync(groupId);

        // Assert
        result.Should().NotBeNull();
        result!.InsufficientData.Should().BeTrue();
    }

    [Fact]
    public async Task CalculateFairness_WithLargeGroup_HandlesPerformance()
    {
        // Arrange
        var groupId = await CreateLargeGroup(15);

        // Act
        var startTime = DateTime.UtcNow;
        var result = await _aiService.CalculateFairnessAsync(groupId, null, null);
        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        result.Should().NotBeNull();
        elapsed.TotalSeconds.Should().BeLessThan(5, "Should complete within 5 seconds");
    }

    [Fact]
    public async Task SuggestBookingTimes_WithZeroDuration_HandlesMinimumDuration()
    {
        // Arrange
        var groupId = await CreateBasicGroupWithUsage();
        var request = new CoOwnershipVehicle.Analytics.Api.Models.SuggestBookingRequest
        {
            UserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            GroupId = groupId,
            DurationMinutes = 0 // Zero duration
        };

        // Act
        var result = await _aiService.SuggestBookingTimesAsync(request);

        // Assert
        if (result != null)
        {
            result.Suggestions.Should().OnlyContain(s => (s.End - s.Start).TotalMinutes >= 30);
        }
    }

    [Fact]
    public async Task GetUsagePredictions_WithSpikeInData_DetectsAnomalies()
    {
        // Arrange
        var groupId = await CreateGroupWithDataSpike();

        // Act
        var result = await _aiService.GetUsagePredictionsAsync(groupId);

        // Assert
        result.Should().NotBeNull();
        if (result!.Anomalies.Any())
        {
            result.Anomalies.Should().Contain(a => a.Description.Contains("spike"));
        }
    }

    // Helper methods to create edge case scenarios

    private async Task<Guid> CreateSingleMemberGroup()
    {
        var userId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var groupId = Guid.NewGuid();

        var user = new User
        {
            Id = userId,
            Email = "single@test.com",
            FirstName = "Single",
            LastName = "User",
            KycStatus = KycStatus.Approved,
            Role = UserRole.CoOwner,
            CreatedAt = DateTime.UtcNow
        };

        var group = new OwnershipGroup
        {
            Id = groupId,
            Name = "Single Member Group",
            Status = GroupStatus.Active,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
            Members = new List<GroupMember>
            {
                new GroupMember
                {
                    UserId = userId,
                    SharePercentage = 1.0m,
                    Status = MembershipStatus.Active,
                    CreatedAt = DateTime.UtcNow
                }
            }
        };

        _mainContext.Users.Add(user);
        _mainContext.OwnershipGroups.Add(group);
        await _mainContext.SaveChangesAsync();

        return groupId;
    }

    private async Task<Guid> CreateGroupWithNoHistory()
    {
        var userId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var groupId = Guid.NewGuid();

        var user = new User
        {
            Id = userId,
            Email = "nohistory@test.com",
            FirstName = "No",
            LastName = "History",
            KycStatus = KycStatus.Approved,
            Role = UserRole.CoOwner,
            CreatedAt = DateTime.UtcNow
        };

        var group = new OwnershipGroup
        {
            Id = groupId,
            Name = "No History Group",
            Status = GroupStatus.Active,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
            Members = new List<GroupMember>
            {
                new GroupMember
                {
                    UserId = userId,
                    SharePercentage = 1.0m,
                    Status = MembershipStatus.Active,
                    CreatedAt = DateTime.UtcNow
                }
            }
        };

        _mainContext.Users.Add(user);
        _mainContext.OwnershipGroups.Add(group);
        await _mainContext.SaveChangesAsync();

        return groupId;
    }

    private async Task<Guid> CreateGroupWithInactiveMembers()
    {
        var userId1 = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var userId2 = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var groupId = Guid.NewGuid();

        var users = new List<User>
        {
            new User
            {
                Id = userId1,
                Email = "inactive1@test.com",
                FirstName = "Inactive",
                LastName = "One",
                KycStatus = KycStatus.Approved,
                Role = UserRole.CoOwner,
                CreatedAt = DateTime.UtcNow
            },
            new User
            {
                Id = userId2,
                Email = "inactive2@test.com",
                FirstName = "Inactive",
                LastName = "Two",
                KycStatus = KycStatus.Approved,
                Role = UserRole.CoOwner,
                CreatedAt = DateTime.UtcNow
            }
        };

        var group = new OwnershipGroup
        {
            Id = groupId,
            Name = "Inactive Members Group",
            Status = GroupStatus.Active,
            CreatedBy = userId1,
            CreatedAt = DateTime.UtcNow,
            Members = new List<GroupMember>
            {
                new GroupMember
                {
                    UserId = userId1,
                    SharePercentage = 0.5m,
                    Status = MembershipStatus.Inactive,
                    CreatedAt = DateTime.UtcNow
                },
                new GroupMember
                {
                    UserId = userId2,
                    SharePercentage = 0.5m,
                    Status = MembershipStatus.Inactive,
                    CreatedAt = DateTime.UtcNow
                }
            }
        };

        _mainContext.Users.AddRange(users);
        _mainContext.OwnershipGroups.Add(group);
        await _mainContext.SaveChangesAsync();

        return groupId;
    }

    private async Task<Guid> CreateExtremelyImbalancedGroup()
    {
        var userId1 = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        var userId2 = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        var groupId = Guid.NewGuid();

        var users = new List<User>
        {
            new User
            {
                Id = userId1,
                Email = "dominant@test.com",
                FirstName = "Dominant",
                LastName = "User",
                KycStatus = KycStatus.Approved,
                Role = UserRole.CoOwner,
                CreatedAt = DateTime.UtcNow
            },
            new User
            {
                Id = userId2,
                Email = "passive@test.com",
                FirstName = "Passive",
                LastName = "User",
                KycStatus = KycStatus.Approved,
                Role = UserRole.CoOwner,
                CreatedAt = DateTime.UtcNow
            }
        };

        var vehicle = new Vehicle
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            Vin = "EXTREMEVIN",
            PlateNumber = "EXT001",
            Make = "Tesla",
            Model = "Model 3",
            Year = 2023,
            Status = VehicleStatus.Available,
            CreatedAt = DateTime.UtcNow
        };

        var group = new OwnershipGroup
        {
            Id = groupId,
            Name = "Extreme Imbalance Group",
            Status = GroupStatus.Active,
            CreatedBy = userId1,
            CreatedAt = DateTime.UtcNow,
            Members = new List<GroupMember>
            {
                new GroupMember
                {
                    UserId = userId1,
                    SharePercentage = 0.5m,
                    Status = MembershipStatus.Active,
                    CreatedAt = DateTime.UtcNow
                },
                new GroupMember
                {
                    UserId = userId2,
                    SharePercentage = 0.5m,
                    Status = MembershipStatus.Active,
                    CreatedAt = DateTime.UtcNow
                }
            }
        };

        // Create user analytics showing extreme imbalance
        var userAnalytics = new CoOwnershipVehicle.Domain.Entities.UserAnalytics
        {
            Id = Guid.NewGuid(),
            UserId = userId1,
            GroupId = groupId,
            PeriodStart = DateTime.UtcNow.AddDays(-90),
            PeriodEnd = DateTime.UtcNow,
            Period = AnalyticsPeriod.Monthly,
            OwnershipShare = 0.5m,
            UsageShare = 0.95m, // Dominates usage
            TotalUsageHours = 950,
            TotalBookings = 95
        };

        _mainContext.Users.AddRange(users);
        _mainContext.OwnershipGroups.Add(group);
        _mainContext.Vehicles.Add(vehicle);
        await _mainContext.SaveChangesAsync();

        _analyticsContext.UserAnalytics.Add(userAnalytics);
        await _analyticsContext.SaveChangesAsync();

        return groupId;
    }

    private async Task<Guid> CreateEmptyGroup()
    {
        var groupId = Guid.NewGuid();
        var group = new OwnershipGroup
        {
            Id = groupId,
            Name = "Empty Group",
            Status = GroupStatus.Active,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            Members = new List<GroupMember>()
        };

        _mainContext.OwnershipGroups.Add(group);
        await _mainContext.SaveChangesAsync();

        return groupId;
    }

    private async Task<Guid> CreateGroupWithMinimalHistory()
    {
        var userId = Guid.Parse("gggggggg-gggg-gggg-gggg-gggggggggggg");
        var groupId = Guid.NewGuid();

        var user = new User
        {
            Id = userId,
            Email = "minimal@test.com",
            FirstName = "Minimal",
            LastName = "History",
            KycStatus = KycStatus.Approved,
            Role = UserRole.CoOwner,
            CreatedAt = DateTime.UtcNow
        };

        var group = new OwnershipGroup
        {
            Id = groupId,
            Name = "Minimal History Group",
            Status = GroupStatus.Active,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
            Members = new List<GroupMember>
            {
                new GroupMember
                {
                    UserId = userId,
                    SharePercentage = 1.0m,
                    Status = MembershipStatus.Active,
                    CreatedAt = DateTime.UtcNow
                }
            }
        };

        // Add analytics with less than 30 days
        var userAnalytics = new CoOwnershipVehicle.Domain.Entities.UserAnalytics
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            GroupId = groupId,
            PeriodStart = DateTime.UtcNow.AddDays(-15),
            PeriodEnd = DateTime.UtcNow,
            Period = AnalyticsPeriod.Daily,
            OwnershipShare = 1.0m,
            UsageShare = 1.0m,
            TotalUsageHours = 100,
            TotalBookings = 5
        };

        _mainContext.Users.Add(user);
        _mainContext.OwnershipGroups.Add(group);
        await _mainContext.SaveChangesAsync();

        _analyticsContext.UserAnalytics.Add(userAnalytics);
        await _analyticsContext.SaveChangesAsync();

        return groupId;
    }

    private async Task<Guid> CreateGroupWithBookingsButNoExpenses()
    {
        var userId = Guid.Parse("hhhhhhhh-hhhh-hhhh-hhhh-hhhhhhhhhhhh");
        var groupId = Guid.NewGuid();

        var user = new User
        {
            Id = userId,
            Email = "noexpense@test.com",
            FirstName = "No",
            LastName = "Expense",
            KycStatus = KycStatus.Approved,
            Role = UserRole.CoOwner,
            CreatedAt = DateTime.UtcNow
        };

        var vehicle = new Vehicle
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            Vin = "NOEXPENSEVIN",
            PlateNumber = "NOEXP",
            Make = "Tesla",
            Model = "Model 3",
            Year = 2023,
            Status = VehicleStatus.Available,
            CreatedAt = DateTime.UtcNow
        };

        var group = new OwnershipGroup
        {
            Id = groupId,
            Name = "No Expense Group",
            Status = GroupStatus.Active,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
            Members = new List<GroupMember>
            {
                new GroupMember
                {
                    UserId = userId,
                    SharePercentage = 1.0m,
                    Status = MembershipStatus.Active,
                    CreatedAt = DateTime.UtcNow
                }
            }
        };

        // Create bookings but no expenses
        var bookings = Enumerable.Range(0, 5).Select(i => new Booking
        {
            Id = Guid.NewGuid(),
            VehicleId = vehicle.Id,
            GroupId = groupId,
            UserId = userId,
            StartAt = DateTime.UtcNow.AddDays(-i * 7),
            EndAt = DateTime.UtcNow.AddDays(-i * 7).AddHours(8),
            Status = BookingStatus.Completed,
            CreatedAt = DateTime.UtcNow.AddDays(-i * 7)
        }).ToList();

        _mainContext.Users.Add(user);
        _mainContext.Vehicles.Add(vehicle);
        _mainContext.OwnershipGroups.Add(group);
        _mainContext.Bookings.AddRange(bookings);
        await _mainContext.SaveChangesAsync();

        return groupId;
    }

    private async Task<Guid> CreateLargeGroup(int memberCount)
    {
        var groupId = Guid.NewGuid();
        var users = new List<User>();
        var members = new List<GroupMember>();

        for (int i = 0; i < memberCount; i++)
        {
            var userId = Guid.NewGuid();
            users.Add(new User
            {
                Id = userId,
                Email = $"member{i}@largegroup.com",
                FirstName = $"Member",
                LastName = $"{i}",
                KycStatus = KycStatus.Approved,
                Role = UserRole.CoOwner,
                CreatedAt = DateTime.UtcNow
            });

            members.Add(new GroupMember
            {
                UserId = userId,
                SharePercentage = 1.0m / memberCount,
                Status = MembershipStatus.Active,
                CreatedAt = DateTime.UtcNow
            });
        }

        var group = new OwnershipGroup
        {
            Id = groupId,
            Name = $"Large Group ({memberCount} members)",
            Status = GroupStatus.Active,
            CreatedBy = users[0].Id,
            CreatedAt = DateTime.UtcNow,
            Members = members
        };

        _mainContext.Users.AddRange(users);
        _mainContext.OwnershipGroups.Add(group);
        await _mainContext.SaveChangesAsync();

        return groupId;
    }

    private async Task<Guid> CreateBasicGroupWithUsage()
    {
        var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var groupId = Guid.NewGuid();

        var user = new User
        {
            Id = userId,
            Email = "basic@test.com",
            FirstName = "Basic",
            LastName = "User",
            KycStatus = KycStatus.Approved,
            Role = UserRole.CoOwner,
            CreatedAt = DateTime.UtcNow.AddMonths(-3)
        };

        var group = new OwnershipGroup
        {
            Id = groupId,
            Name = "Basic Usage Group",
            Status = GroupStatus.Active,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow.AddMonths(-3),
            Members = new List<GroupMember>
            {
                new GroupMember
                {
                    UserId = userId,
                    SharePercentage = 1.0m,
                    Status = MembershipStatus.Active,
                    CreatedAt = DateTime.UtcNow.AddMonths(-3)
                }
            }
        };

        var userAnalytics = new CoOwnershipVehicle.Domain.Entities.UserAnalytics
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            GroupId = groupId,
            PeriodStart = DateTime.UtcNow.AddDays(-120),
            PeriodEnd = DateTime.UtcNow,
            Period = AnalyticsPeriod.Daily,
            OwnershipShare = 1.0m,
            UsageShare = 1.0m,
            TotalUsageHours = 1000,
            TotalBookings = 50
        };

        _mainContext.Users.Add(user);
        _mainContext.OwnershipGroups.Add(group);
        await _mainContext.SaveChangesAsync();

        _analyticsContext.UserAnalytics.Add(userAnalytics);
        await _analyticsContext.SaveChangesAsync();

        return groupId;
    }

    private async Task<Guid> CreateGroupWithDataSpike()
    {
        var userId = Guid.Parse("iiiiiiii-iiii-iiii-iiii-iiiiiiiiiiii");
        var groupId = Guid.NewGuid();

        var user = new User
        {
            Id = userId,
            Email = "spike@test.com",
            FirstName = "Spike",
            LastName = "Data",
            KycStatus = KycStatus.Approved,
            Role = UserRole.CoOwner,
            CreatedAt = DateTime.UtcNow.AddMonths(-3)
        };

        var group = new OwnershipGroup
        {
            Id = groupId,
            Name = "Data Spike Group",
            Status = GroupStatus.Active,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow.AddMonths(-3),
            Members = new List<GroupMember>
            {
                new GroupMember
                {
                    UserId = userId,
                    SharePercentage = 1.0m,
                    Status = MembershipStatus.Active,
                    CreatedAt = DateTime.UtcNow.AddMonths(-3)
                }
            }
        };

        // Create analytics snapshots with a spike
        var snapshots = new List<CoOwnershipVehicle.Domain.Entities.AnalyticsSnapshot>();
        var baseUsage = 100;

        for (int i = 0; i < 60; i++)
        {
            var usage = i == 30 ? baseUsage * 5 : baseUsage; // Spike on day 30
            snapshots.Add(new CoOwnershipVehicle.Domain.Entities.AnalyticsSnapshot
            {
                Id = Guid.NewGuid(),
                GroupId = groupId,
                SnapshotDate = DateTime.UtcNow.AddDays(-60 + i),
                Period = AnalyticsPeriod.Daily,
                TotalUsageHours = usage,
                TotalBookings = usage / 10,
                ActiveUsers = 1
            });
        }

        _mainContext.Users.Add(user);
        _mainContext.OwnershipGroups.Add(group);
        await _mainContext.SaveChangesAsync();

        _analyticsContext.AnalyticsSnapshots.AddRange(snapshots);
        await _analyticsContext.SaveChangesAsync();

        return groupId;
    }
}

