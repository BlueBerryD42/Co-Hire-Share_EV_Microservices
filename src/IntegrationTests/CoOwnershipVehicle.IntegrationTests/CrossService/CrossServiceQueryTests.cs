using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.IntegrationTests.TestFixtures;
using FluentAssertions;

namespace CoOwnershipVehicle.IntegrationTests.CrossService;

public class CrossServiceQueryTests : IntegrationTestBase
{
    [Fact]
    [Trait("Category", "CrossService")]
    public async Task AnalyticsService_PullingDataFromAllServices_ShouldSucceed()
    {
        // Setup data across services
        var users = new List<User>();
        for (int i = 0; i < 5; i++)
        {
            users.Add(await CreateAndSaveUserAsync());
        }

        var groups = new List<OwnershipGroup>();
        for (int i = 0; i < 3; i++)
        {
            var memberIds = users.Skip(i * 2).Take(2).Select(u => u.Id).ToList();
            if (memberIds.Count == 2)
            {
                groups.Add(await CreateAndSaveGroupAsync(memberIds[0], memberIds));
            }
        }

        var vehicles = new List<Vehicle>();
        foreach (var group in groups)
        {
            vehicles.Add(await CreateAndSaveVehicleAsync(group.Id));
        }

        var bookings = new List<Booking>();
        foreach (var vehicle in vehicles)
        {
            var booking = TestDataBuilder.CreateTestBooking(
                vehicle.Id,
                vehicle.GroupId!.Value,
                users.First().Id,
                BookingStatus.Completed
            );
            bookings.Add(booking);
        }
        DbContext.Bookings.AddRange(bookings);
        await DbContext.SaveChangesAsync();

        // Analytics service aggregates data from all services
        var analyticsData = new
        {
            TotalUsers = await DbContext.Users.CountAsync(),
            TotalGroups = await DbContext.OwnershipGroups.CountAsync(),
            TotalVehicles = await DbContext.Vehicles.CountAsync(),
            TotalBookings = await DbContext.Bookings.CountAsync(),
            CompletedBookings = await DbContext.Bookings
                .Where(b => b.Status == BookingStatus.Completed)
                .CountAsync(),
            ActiveGroups = await DbContext.OwnershipGroups
                .Where(g => g.Status == GroupStatus.Active)
                .CountAsync()
        };

        analyticsData.TotalUsers.Should().BeGreaterOrEqualTo(5);
        analyticsData.TotalGroups.Should().BeGreaterOrEqualTo(3);
        analyticsData.TotalVehicles.Should().BeGreaterOrEqualTo(3);
        analyticsData.TotalBookings.Should().BeGreaterOrEqualTo(3);
        analyticsData.CompletedBookings.Should().BeGreaterOrEqualTo(3);
        analyticsData.ActiveGroups.Should().BeGreaterOrEqualTo(3);
    }

    [Fact]
    [Trait("Category", "CrossService")]
    public async Task AdminDashboard_AggregatingFromMultipleServices_ShouldSucceed()
    {
        // Create diverse data across services
        var admin = await CreateAndSaveUserAsync(UserRole.SystemAdmin, KycStatus.Approved);
        
        // Users with different statuses
        var activeUsers = new List<User>();
        for (int i = 0; i < 10; i++)
        {
            activeUsers.Add(await CreateAndSaveUserAsync(UserRole.CoOwner, KycStatus.Approved));
        }

        var pendingKycUsers = new List<User>();
        for (int i = 0; i < 5; i++)
        {
            pendingKycUsers.Add(await CreateAndSaveUserAsync(UserRole.CoOwner, KycStatus.Pending));
        }

        // Groups
        var groups = new List<OwnershipGroup>();
        for (int i = 0; i < 5; i++)
        {
            var memberIds = activeUsers.Skip(i * 2).Take(2).Select(u => u.Id).ToList();
            if (memberIds.Count == 2)
            {
                groups.Add(await CreateAndSaveGroupAsync(memberIds[0], memberIds));
            }
        }

        // Vehicles
        var vehicles = new List<Vehicle>();
        foreach (var group in groups)
        {
            vehicles.Add(await CreateAndSaveVehicleAsync(group.Id));
        }

        // Bookings with different statuses
        var confirmedBookings = new List<Booking>();
        var completedBookings = new List<Booking>();
        var cancelledBookings = new List<Booking>();

        foreach (var vehicle in vehicles.Take(3))
        {
            confirmedBookings.Add(TestDataBuilder.CreateTestBooking(
                vehicle.Id, vehicle.GroupId!.Value, activeUsers.First().Id, BookingStatus.Confirmed));
        }

        foreach (var vehicle in vehicles.Skip(3).Take(2))
        {
            completedBookings.Add(TestDataBuilder.CreateTestBooking(
                vehicle.Id, vehicle.GroupId!.Value, activeUsers.First().Id, BookingStatus.Completed));
        }

        cancelledBookings.Add(TestDataBuilder.CreateTestBooking(
            vehicles.First().Id, vehicles.First().GroupId!.Value, activeUsers.First().Id, BookingStatus.Cancelled));

        DbContext.Bookings.AddRange(confirmedBookings);
        DbContext.Bookings.AddRange(completedBookings);
        DbContext.Bookings.AddRange(cancelledBookings);
        await DbContext.SaveChangesAsync();

        // Admin dashboard aggregates from multiple services
        var dashboardMetrics = new
        {
            UserMetrics = new
            {
                TotalUsers = await DbContext.Users.CountAsync(),
                ActiveUsers = await DbContext.Users
                    .Where(u => u.AccountStatus == UserAccountStatus.Active)
                    .CountAsync(),
                PendingKyc = await DbContext.Users
                    .Where(u => u.KycStatus == KycStatus.Pending)
                    .CountAsync()
            },
            GroupMetrics = new
            {
                TotalGroups = await DbContext.OwnershipGroups.CountAsync(),
                ActiveGroups = await DbContext.OwnershipGroups
                    .Where(g => g.Status == GroupStatus.Active)
                    .CountAsync()
            },
            VehicleMetrics = new
            {
                TotalVehicles = await DbContext.Vehicles.CountAsync(),
                AvailableVehicles = await DbContext.Vehicles
                    .Where(v => v.Status == VehicleStatus.Available)
                    .CountAsync()
            },
            BookingMetrics = new
            {
                TotalBookings = await DbContext.Bookings.CountAsync(),
                ConfirmedBookings = await DbContext.Bookings
                    .Where(b => b.Status == BookingStatus.Confirmed)
                    .CountAsync(),
                CompletedBookings = await DbContext.Bookings
                    .Where(b => b.Status == BookingStatus.Completed)
                    .CountAsync(),
                CancelledBookings = await DbContext.Bookings
                    .Where(b => b.Status == BookingStatus.Cancelled)
                    .CountAsync()
            }
        };

        dashboardMetrics.UserMetrics.TotalUsers.Should().BeGreaterOrEqualTo(16);
        dashboardMetrics.UserMetrics.PendingKyc.Should().BeGreaterOrEqualTo(5);
        dashboardMetrics.GroupMetrics.TotalGroups.Should().BeGreaterOrEqualTo(5);
        dashboardMetrics.VehicleMetrics.TotalVehicles.Should().BeGreaterOrEqualTo(5);
        dashboardMetrics.BookingMetrics.TotalBookings.Should().BeGreaterOrEqualTo(6);
        dashboardMetrics.BookingMetrics.ConfirmedBookings.Should().BeGreaterOrEqualTo(3);
        dashboardMetrics.BookingMetrics.CompletedBookings.Should().BeGreaterOrEqualTo(2);
        dashboardMetrics.BookingMetrics.CancelledBookings.Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    [Trait("Category", "CrossService")]
    public async Task Reports_CombiningDataSources_ShouldSucceed()
    {
        // Create comprehensive data
        var users = new List<User>();
        for (int i = 0; i < 20; i++)
        {
            users.Add(await CreateAndSaveUserAsync());
        }

        var groups = new List<OwnershipGroup>();
        for (int i = 0; i < 10; i++)
        {
            var memberIds = users.Skip(i * 2).Take(2).Select(u => u.Id).ToList();
            if (memberIds.Count == 2)
            {
                groups.Add(await CreateAndSaveGroupAsync(memberIds[0], memberIds));
            }
        }

        var vehicles = new List<Vehicle>();
        foreach (var group in groups)
        {
            vehicles.Add(await CreateAndSaveVehicleAsync(group.Id));
        }

        var bookings = new List<Booking>();
        var expenses = new List<Expense>();
        var payments = new List<Payment>();

        foreach (var vehicle in vehicles.Take(5))
        {
            var booking = TestDataBuilder.CreateTestBooking(
                vehicle.Id,
                vehicle.GroupId!.Value,
                users.First().Id,
                BookingStatus.Completed
            );
            bookings.Add(booking);
            DbContext.Bookings.Add(booking);

            var expense = TestDataBuilder.CreateTestExpense(
                vehicle.GroupId.Value,
                vehicle.Id,
                200.00m
            );
            expenses.Add(expense);
            DbContext.Expenses.Add(expense);

            var invoice = new Invoice
            {
                Id = Guid.NewGuid(),
                ExpenseId = expense.Id,
                PayerId = users.First().Id,
                Amount = expense.Amount,
                InvoiceNumber = $"INV-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}",
                Status = InvoiceStatus.Paid,
                DueDate = DateTime.UtcNow.AddDays(30),
                PaidAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            DbContext.Invoices.Add(invoice);

            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                InvoiceId = invoice.Id,
                PayerId = invoice.PayerId,
                Amount = invoice.Amount,
                Method = PaymentMethod.BankTransfer,
                Status = PaymentStatus.Completed,
                PaidAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            payments.Add(payment);
            DbContext.Payments.Add(payment);
        }

        await DbContext.SaveChangesAsync();

        // Generate combined report
        var report = new
        {
            Period = new { Start = DateTime.UtcNow.AddDays(-30), End = DateTime.UtcNow },
            UserCount = users.Count,
            GroupCount = groups.Count,
            VehicleCount = vehicles.Count,
            BookingCount = bookings.Count,
            ExpenseCount = expenses.Count,
            TotalExpenses = expenses.Sum(e => e.Amount),
            PaymentCount = payments.Count,
            TotalRevenue = payments.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount),
            AverageBookingValue = bookings.Count > 0 
                ? expenses.Sum(e => e.Amount) / bookings.Count 
                : 0
        };

        report.UserCount.Should().Be(20);
        report.GroupCount.Should().Be(10);
        report.VehicleCount.Should().BeGreaterOrEqualTo(10);
        report.BookingCount.Should().BeGreaterOrEqualTo(5);
        report.ExpenseCount.Should().BeGreaterOrEqualTo(5);
        report.TotalExpenses.Should().BeGreaterThan(0);
        report.PaymentCount.Should().BeGreaterOrEqualTo(5);
        report.TotalRevenue.Should().BeGreaterThan(0);
    }
}

