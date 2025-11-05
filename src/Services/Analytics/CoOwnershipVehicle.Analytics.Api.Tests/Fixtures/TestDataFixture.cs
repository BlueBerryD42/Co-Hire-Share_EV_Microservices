using CoOwnershipVehicle.Analytics.Api.Data;
using CoOwnershipVehicle.Data;
using CoOwnershipVehicle.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoOwnershipVehicle.Analytics.Api.Tests.Fixtures;

public class TestDataFixture : IDisposable
{
    private readonly string _dbName;
    private readonly string _mainDbName;

    public AnalyticsDbContext AnalyticsContext { get; }
    public ApplicationDbContext MainContext { get; }

    public TestDataFixture()
    {
        _dbName = Guid.NewGuid().ToString();
        _mainDbName = Guid.NewGuid().ToString();

        var analyticsOptions = new DbContextOptionsBuilder<AnalyticsDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        var mainOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(_mainDbName)
            .Options;

        AnalyticsContext = new AnalyticsDbContext(analyticsOptions);
        MainContext = new ApplicationDbContext(mainOptions);

        SeedTestData();
    }

    private void SeedTestData()
    {
        // Create test users
        var users = CreateTestUsers();
        MainContext.Users.AddRange(users);
        MainContext.SaveChanges();

        // Create test groups
        var groups = CreateTestGroups(users);
        MainContext.OwnershipGroups.AddRange(groups);
        MainContext.SaveChanges();

        // Create test vehicles
        var vehicles = CreateTestVehicles(groups);
        MainContext.Vehicles.AddRange(vehicles);
        MainContext.SaveChanges();

        // Create test bookings
        var bookings = CreateTestBookings(users, groups, vehicles);
        MainContext.Bookings.AddRange(bookings);
        MainContext.SaveChanges();

        // Create test expenses
        var expenses = CreateTestExpenses(users, groups, vehicles);
        MainContext.Expenses.AddRange(expenses);
        MainContext.SaveChanges();

        // Create test user analytics
        var userAnalytics = CreateTestUserAnalytics(users, groups);
        AnalyticsContext.UserAnalytics.AddRange(userAnalytics);
        AnalyticsContext.SaveChanges();

        // Create test analytics snapshots
        var snapshots = CreateTestAnalyticsSnapshots(groups, vehicles);
        AnalyticsContext.AnalyticsSnapshots.AddRange(snapshots);
        AnalyticsContext.SaveChanges();
    }

    private List<User> CreateTestUsers()
    {
        return new List<User>
        {
            new User
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Email = "user1@test.com",
                FirstName = "Alice",
                LastName = "Anderson",
                KycStatus = KycStatus.Approved,
                Role = UserRole.CoOwner,
                CreatedAt = DateTime.UtcNow.AddMonths(-6),
                UpdatedAt = DateTime.UtcNow
            },
            new User
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Email = "user2@test.com",
                FirstName = "Bob",
                LastName = "Brown",
                KycStatus = KycStatus.Approved,
                Role = UserRole.CoOwner,
                CreatedAt = DateTime.UtcNow.AddMonths(-6),
                UpdatedAt = DateTime.UtcNow
            },
            new User
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Email = "user3@test.com",
                FirstName = "Charlie",
                LastName = "Clark",
                KycStatus = KycStatus.Approved,
                Role = UserRole.CoOwner,
                CreatedAt = DateTime.UtcNow.AddMonths(-6),
                UpdatedAt = DateTime.UtcNow
            },
            new User
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                Email = "user4@test.com",
                FirstName = "Diana",
                LastName = "Davis",
                KycStatus = KycStatus.Approved,
                Role = UserRole.CoOwner,
                CreatedAt = DateTime.UtcNow.AddMonths(-3),
                UpdatedAt = DateTime.UtcNow
            },
            new User
            {
                Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                Email = "user5@test.com",
                FirstName = "Eve",
                LastName = "Edwards",
                KycStatus = KycStatus.Approved,
                Role = UserRole.CoOwner,
                CreatedAt = DateTime.UtcNow.AddMonths(-6),
                UpdatedAt = DateTime.UtcNow
            }
        };
    }

    private List<OwnershipGroup> CreateTestGroups(List<User> users)
    {
        return new List<OwnershipGroup>
        {
            new OwnershipGroup
            {
                Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                Name = "Balanced Test Group",
                Description = "Group with balanced usage",
                Status = GroupStatus.Active,
                CreatedBy = users[0].Id,
                CreatedAt = DateTime.UtcNow.AddMonths(-6),
                UpdatedAt = DateTime.UtcNow,
                Members = new List<GroupMember>
                {
                    new GroupMember
                    {
                        UserId = users[0].Id,
                        SharePercentage = 0.33m,
                        Status = MembershipStatus.Active,
                        CreatedAt = DateTime.UtcNow.AddMonths(-6)
                    },
                    new GroupMember
                    {
                        UserId = users[1].Id,
                        SharePercentage = 0.33m,
                        Status = MembershipStatus.Active,
                        CreatedAt = DateTime.UtcNow.AddMonths(-6)
                    },
                    new GroupMember
                    {
                        UserId = users[2].Id,
                        SharePercentage = 0.34m,
                        Status = MembershipStatus.Active,
                        CreatedAt = DateTime.UtcNow.AddMonths(-6)
                    }
                }
            },
            new OwnershipGroup
            {
                Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                Name = "Imbalanced Test Group",
                Description = "Group with imbalanced usage",
                Status = GroupStatus.Active,
                CreatedBy = users[0].Id,
                CreatedAt = DateTime.UtcNow.AddMonths(-6),
                UpdatedAt = DateTime.UtcNow,
                Members = new List<GroupMember>
                {
                    new GroupMember
                    {
                        UserId = users[0].Id,
                        SharePercentage = 0.25m,
                        Status = MembershipStatus.Active,
                        CreatedAt = DateTime.UtcNow.AddMonths(-6)
                    },
                    new GroupMember
                    {
                        UserId = users[1].Id,
                        SharePercentage = 0.25m,
                        Status = MembershipStatus.Active,
                        CreatedAt = DateTime.UtcNow.AddMonths(-6)
                    },
                    new GroupMember
                    {
                        UserId = users[2].Id,
                        SharePercentage = 0.50m,
                        Status = MembershipStatus.Active,
                        CreatedAt = DateTime.UtcNow.AddMonths(-6)
                    }
                }
            },
            new OwnershipGroup
            {
                Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                Name = "Single Vehicle Group",
                Description = "Test group with single member",
                Status = GroupStatus.Active,
                CreatedBy = users[4].Id,
                CreatedAt = DateTime.UtcNow.AddMonths(-6),
                UpdatedAt = DateTime.UtcNow,
                Members = new List<GroupMember>
                {
                    new GroupMember
                    {
                        UserId = users[4].Id,
                        SharePercentage = 1.0m,
                        Status = MembershipStatus.Active,
                        CreatedAt = DateTime.UtcNow.AddMonths(-6)
                    }
                }
            }
        };
    }

    private List<Vehicle> CreateTestVehicles(List<OwnershipGroup> groups)
    {
        return new List<Vehicle>
        {
            new Vehicle
            {
                Id = Guid.Parse("aaaa1111-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                GroupId = groups[0].Id,
                Vin = "TESTVIN001",
                PlateNumber = "TEST001",
                Make = "Tesla",
                Model = "Model 3",
                Year = 2023,
                Status = VehicleStatus.Available,
                CreatedAt = DateTime.UtcNow.AddMonths(-6),
                UpdatedAt = DateTime.UtcNow
            },
            new Vehicle
            {
                Id = Guid.Parse("aaaa2222-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                GroupId = groups[1].Id,
                Vin = "TESTVIN002",
                PlateNumber = "TEST002",
                Make = "Tesla",
                Model = "Model Y",
                Year = 2024,
                Status = VehicleStatus.Available,
                CreatedAt = DateTime.UtcNow.AddMonths(-6),
                UpdatedAt = DateTime.UtcNow
            },
            new Vehicle
            {
                Id = Guid.Parse("aaaa3333-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                GroupId = groups[2].Id,
                Vin = "TESTVIN003",
                PlateNumber = "TEST003",
                Make = "Tesla",
                Model = "Model S",
                Year = 2022,
                Status = VehicleStatus.Available,
                CreatedAt = DateTime.UtcNow.AddMonths(-6),
                UpdatedAt = DateTime.UtcNow
            }
        };
    }

    private List<Booking> CreateTestBookings(List<User> users, List<OwnershipGroup> groups, List<Vehicle> vehicles)
    {
        var bookings = new List<Booking>();
        var now = DateTime.UtcNow;

        // Balanced group bookings - users take turns evenly
        for (int i = 0; i < 30; i++)
        {
            var userId = i % 3 == 0 ? users[0].Id : i % 3 == 1 ? users[1].Id : users[2].Id;
            bookings.Add(new Booking
            {
                Id = Guid.NewGuid(),
                GroupId = groups[0].Id,
                VehicleId = vehicles[0].Id,
                UserId = userId,
                StartAt = now.AddDays(-30 + i).AddHours(8),
                EndAt = now.AddDays(-30 + i).AddHours(16),
                Status = BookingStatus.Completed,
                CreatedAt = now.AddDays(-30 + i),
                CheckIns = new List<CheckIn>
                {
                    new CheckIn
                    {
                        Id = Guid.NewGuid(),
                        BookingId = Guid.Empty, // Will be set after booking is added
                        Type = CheckInType.CheckIn,
                        Odometer = 10000 + (i * 50),
                        CreatedAt = now.AddDays(-30 + i).AddHours(8)
                    },
                    new CheckIn
                    {
                        Id = Guid.NewGuid(),
                        BookingId = Guid.Empty,
                        Type = CheckInType.CheckOut,
                        Odometer = 10000 + (i * 50) + 100,
                        CreatedAt = now.AddDays(-30 + i).AddHours(16)
                    }
                }
            });
        }

        // Imbalanced group bookings - user 2 dominates
        for (int i = 0; i < 20; i++)
        {
            var userId = i < 10 ? users[2].Id : (i < 15 ? users[0].Id : users[1].Id);
            bookings.Add(new Booking
            {
                Id = Guid.NewGuid(),
                GroupId = groups[1].Id,
                VehicleId = vehicles[1].Id,
                UserId = userId,
                StartAt = now.AddDays(-30 + i).AddHours(9),
                EndAt = now.AddDays(-30 + i).AddHours(17),
                Status = BookingStatus.Completed,
                CreatedAt = now.AddDays(-30 + i),
                CheckIns = new List<CheckIn>
                {
                    new CheckIn
                    {
                        Id = Guid.NewGuid(),
                        BookingId = Guid.Empty,
                        Type = CheckInType.CheckIn,
                        Odometer = 5000 + (i * 50),
                        CreatedAt = now.AddDays(-30 + i).AddHours(9)
                    },
                    new CheckIn
                    {
                        Id = Guid.NewGuid(),
                        BookingId = Guid.Empty,
                        Type = CheckInType.CheckOut,
                        Odometer = 5000 + (i * 50) + 150,
                        CreatedAt = now.AddDays(-30 + i).AddHours(17)
                    }
                }
            });
        }

        return bookings;
    }

    private List<Expense> CreateTestExpenses(List<User> users, List<OwnershipGroup> groups, List<Vehicle> vehicles)
    {
        var expenses = new List<Expense>();
        var now = DateTime.UtcNow;

        // Balance group expenses - well distributed
        expenses.Add(new Expense
        {
            Id = Guid.NewGuid(),
            GroupId = groups[0].Id,
            VehicleId = vehicles[0].Id,
            ExpenseType = ExpenseType.Maintenance,
            Amount = 200m,
            Description = "Regular maintenance at Tesla Service Center",
            DateIncurred = now.AddMonths(-3),
            CreatedBy = users[0].Id,
            IsRecurring = false
        });

        expenses.Add(new Expense
        {
            Id = Guid.NewGuid(),
            GroupId = groups[0].Id,
            VehicleId = vehicles[0].Id,
            ExpenseType = ExpenseType.Insurance,
            Amount = 1200m,
            Description = "Annual insurance renewal",
            DateIncurred = now.AddMonths(-5),
            CreatedBy = users[0].Id,
            IsRecurring = true
        });

        // Imbalanced group expenses - high costs
        expenses.Add(new Expense
        {
            Id = Guid.NewGuid(),
            GroupId = groups[1].Id,
            VehicleId = vehicles[1].Id,
            ExpenseType = ExpenseType.Repair,
            Amount = 500m,
            Description = "Brake repair at Dealer Service Center",
            DateIncurred = now.AddMonths(-2),
            CreatedBy = users[1].Id,
            IsRecurring = false
        });

        expenses.Add(new Expense
        {
            Id = Guid.NewGuid(),
            GroupId = groups[1].Id,
            VehicleId = vehicles[1].Id,
            ExpenseType = ExpenseType.Repair,
            Amount = 750m,
            Description = "Engine repair at Dealer Service Center",
            DateIncurred = now.AddMonths(-1),
            CreatedBy = users[1].Id,
            IsRecurring = false
        });

        expenses.Add(new Expense
        {
            Id = Guid.NewGuid(),
            GroupId = groups[1].Id,
            VehicleId = vehicles[1].Id,
            ExpenseType = ExpenseType.Cleaning,
            Amount = 75m,
            Description = "Professional cleaning",
            DateIncurred = now.AddDays(-10),
            CreatedBy = users[1].Id,
            IsRecurring = false
        });

        return expenses;
    }

    private List<UserAnalytics> CreateTestUserAnalytics(List<User> users, List<OwnershipGroup> groups)
    {
        var analytics = new List<UserAnalytics>();
        var now = DateTime.UtcNow;

        // Balanced group analytics
        analytics.Add(new UserAnalytics
        {
            Id = Guid.NewGuid(),
            UserId = users[0].Id,
            GroupId = groups[0].Id,
            PeriodStart = now.AddDays(-90),
            PeriodEnd = now,
            Period = AnalyticsPeriod.Monthly,
            OwnershipShare = 0.33m,
            UsageShare = 0.33m,
            TotalUsageHours = 240,
            TotalBookings = 10
        });

        analytics.Add(new UserAnalytics
        {
            Id = Guid.NewGuid(),
            UserId = users[1].Id,
            GroupId = groups[0].Id,
            PeriodStart = now.AddDays(-90),
            PeriodEnd = now,
            Period = AnalyticsPeriod.Monthly,
            OwnershipShare = 0.33m,
            UsageShare = 0.33m,
            TotalUsageHours = 240,
            TotalBookings = 10
        });

        analytics.Add(new UserAnalytics
        {
            Id = Guid.NewGuid(),
            UserId = users[2].Id,
            GroupId = groups[0].Id,
            PeriodStart = now.AddDays(-90),
            PeriodEnd = now,
            Period = AnalyticsPeriod.Monthly,
            OwnershipShare = 0.34m,
            UsageShare = 0.34m,
            TotalUsageHours = 240,
            TotalBookings = 10
        });

        // Imbalanced group analytics
        analytics.Add(new UserAnalytics
        {
            Id = Guid.NewGuid(),
            UserId = users[0].Id,
            GroupId = groups[1].Id,
            PeriodStart = now.AddDays(-90),
            PeriodEnd = now,
            Period = AnalyticsPeriod.Monthly,
            OwnershipShare = 0.25m,
            UsageShare = 0.25m,
            TotalUsageHours = 100,
            TotalBookings = 5
        });

        analytics.Add(new UserAnalytics
        {
            Id = Guid.NewGuid(),
            UserId = users[1].Id,
            GroupId = groups[1].Id,
            PeriodStart = now.AddDays(-90),
            PeriodEnd = now,
            Period = AnalyticsPeriod.Monthly,
            OwnershipShare = 0.25m,
            UsageShare = 0.25m,
            TotalUsageHours = 100,
            TotalBookings = 5
        });

        analytics.Add(new UserAnalytics
        {
            Id = Guid.NewGuid(),
            UserId = users[2].Id,
            GroupId = groups[1].Id,
            PeriodStart = now.AddDays(-90),
            PeriodEnd = now,
            Period = AnalyticsPeriod.Monthly,
            OwnershipShare = 0.50m,
            UsageShare = 0.50m,
            TotalUsageHours = 200,
            TotalBookings = 10
        });

        return analytics;
    }

    private List<AnalyticsSnapshot> CreateTestAnalyticsSnapshots(List<OwnershipGroup> groups, List<Vehicle> vehicles)
    {
        var snapshots = new List<AnalyticsSnapshot>();
        var now = DateTime.UtcNow;

        for (int i = 0; i < 6; i++)
        {
            var monthStart = new DateTime(now.Year, now.Month, 1).AddMonths(-i);
            var monthEnd = monthStart.AddMonths(1).AddTicks(-1);

            snapshots.Add(new AnalyticsSnapshot
            {
                Id = Guid.NewGuid(),
                GroupId = groups[0].Id,
                VehicleId = vehicles[0].Id,
                SnapshotDate = monthStart,
                Period = AnalyticsPeriod.Monthly,
                TotalBookings = 30 - (i * 2),
                TotalUsageHours = 720 - (i * 20),
                ActiveUsers = 3,
                TotalDistance = 3000 - (i * 100),
                TotalRevenue = 3000m - (i * 100),
                TotalExpenses = 1500m - (i * 50),
                NetProfit = 1500m - (i * 50),
                UtilizationRate = 0.8m,
                MaintenanceEfficiency = 0.85m,
                UserSatisfactionScore = 0.90m
            });
        }

        return snapshots;
    }

    public void Dispose()
    {
        AnalyticsContext.Database.EnsureDeleted();
        MainContext.Database.EnsureDeleted();
        AnalyticsContext.Dispose();
        MainContext.Dispose();
    }
}

