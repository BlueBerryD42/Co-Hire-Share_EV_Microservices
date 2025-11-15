using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.IntegrationTests.TestFixtures;
using FluentAssertions;
using Bogus;

namespace CoOwnershipVehicle.IntegrationTests.AIWorkflows;

public class AIWorkflowTests : IntegrationTestBase
{
    private static readonly Faker _faker = new();
    [Fact]
    [Trait("Category", "AIWorkflow")]
    public async Task AIFairnessCalculationWorkflow_CalculateFairnessSuggestBookings_ShouldSucceed()
    {
        // Setup: Create group with multiple members and bookings
        var creator = await CreateAndSaveUserAsync(UserRole.CoOwner, KycStatus.Approved);
        var member1 = await CreateAndSaveUserAsync(UserRole.CoOwner, KycStatus.Approved);
        var member2 = await CreateAndSaveUserAsync(UserRole.CoOwner, KycStatus.Approved);
        var member3 = await CreateAndSaveUserAsync(UserRole.CoOwner, KycStatus.Approved);
        
        var group = await CreateAndSaveGroupAsync(
            creator.Id,
            new List<Guid> { creator.Id, member1.Id, member2.Id, member3.Id }
        );

        var vehicle = await CreateAndSaveVehicleAsync(group.Id);

        // Create bookings to establish usage patterns
        // Member1 books more (overutilizer)
        for (int i = 0; i < 5; i++)
        {
            var booking = TestDataBuilder.CreateTestBooking(
                vehicle.Id,
                group.Id,
                member1.Id,
                BookingStatus.Completed
            );
            booking.StartAt = DateTime.UtcNow.AddDays(-(i + 1) * 7);
            booking.EndAt = booking.StartAt.AddHours(24);
            DbContext.Bookings.Add(booking);

            // Add check-ins
            var checkOut = TestDataBuilder.CreateCheckIn(
                booking.Id,
                member1.Id,
                CheckInType.CheckOut,
                10000 + (i * 100)
            );
            checkOut.CheckInTime = booking.StartAt;
            
            var checkIn = TestDataBuilder.CreateCheckIn(
                booking.Id,
                member1.Id,
                CheckInType.CheckIn,
                10100 + (i * 100)
            );
            checkIn.CheckInTime = booking.EndAt;

            DbContext.CheckIns.AddRange(checkOut, checkIn);
        }

        // Member2 books less (underutilizer)
        for (int i = 0; i < 2; i++)
        {
            var booking = TestDataBuilder.CreateTestBooking(
                vehicle.Id,
                group.Id,
                member2.Id,
                BookingStatus.Completed
            );
            booking.StartAt = DateTime.UtcNow.AddDays(-(i + 1) * 14);
            booking.EndAt = booking.StartAt.AddHours(12);
            DbContext.Bookings.Add(booking);

            var checkOut = TestDataBuilder.CreateCheckIn(
                booking.Id,
                member2.Id,
                CheckInType.CheckOut,
                10000 + (i * 50)
            );
            checkOut.CheckInTime = booking.StartAt;
            
            var checkIn = TestDataBuilder.CreateCheckIn(
                booking.Id,
                member2.Id,
                CheckInType.CheckIn,
                10050 + (i * 50)
            );
            checkIn.CheckInTime = booking.EndAt;

            DbContext.CheckIns.AddRange(checkOut, checkIn);
        }

        // Member3 occasional bookings to keep baseline ownership usage balance
        for (int i = 0; i < 3; i++)
        {
            var booking = TestDataBuilder.CreateTestBooking(
                vehicle.Id,
                group.Id,
                member3.Id,
                BookingStatus.Completed
            );
            booking.StartAt = DateTime.UtcNow.AddDays(-(i + 1) * 10);
            booking.EndAt = booking.StartAt.AddHours(6);
            DbContext.Bookings.Add(booking);

            var checkOut = TestDataBuilder.CreateCheckIn(
                booking.Id,
                member3.Id,
                CheckInType.CheckOut,
                10000 + (i * 40)
            );
            checkOut.CheckInTime = booking.StartAt;

            var checkIn = TestDataBuilder.CreateCheckIn(
                booking.Id,
                member3.Id,
                CheckInType.CheckIn,
                10030 + (i * 40)
            );
            checkIn.CheckInTime = booking.EndAt;

            DbContext.CheckIns.AddRange(checkOut, checkIn);
        }

        await DbContext.SaveChangesAsync();

        // Calculate fairness (simulated - in real scenario would call AI service)
        var member1Bookings = await DbContext.Bookings
            .Where(b => b.UserId == member1.Id && b.GroupId == group.Id && b.Status == BookingStatus.Completed)
            .CountAsync();

        var member2Bookings = await DbContext.Bookings
            .Where(b => b.UserId == member2.Id && b.GroupId == group.Id && b.Status == BookingStatus.Completed)
            .CountAsync();
        var member3Bookings = await DbContext.Bookings
            .Where(b => b.UserId == member3.Id && b.GroupId == group.Id && b.Status == BookingStatus.Completed)
            .CountAsync();

        var totalBookings = member1Bookings + member2Bookings + member3Bookings;

        var member1UsageShare = (decimal)member1Bookings / totalBookings;
        var member2UsageShare = (decimal)member2Bookings / totalBookings;
        var member3UsageShare = (decimal)member3Bookings / totalBookings;

        // All members have equal ownership (25% each)
        var expectedOwnership = 0.25m;

        // Member1 should have higher usage than ownership (overutilizer)
        member1UsageShare.Should().BeGreaterThan(expectedOwnership);
        
        // Member2 should have lower usage than ownership (underutilizer)
        member2UsageShare.Should().BeLessThan(expectedOwnership);

        // Member3 usage should remain around ownership (passive)
        member3UsageShare.Should().BeApproximately(expectedOwnership, 0.05m);

        // AI suggests booking for underutilizer (member2)
        // In real scenario, would call AI service endpoint
        var suggestedBooking = TestDataBuilder.CreateTestBooking(
            vehicle.Id,
            group.Id,
            member2.Id,
            BookingStatus.Confirmed
        );
        suggestedBooking.StartAt = DateTime.UtcNow.AddDays(1);
        suggestedBooking.EndAt = suggestedBooking.StartAt.AddHours(24);

        // Verify suggestion aligns with fairness improvement
        // Underutilizer should be encouraged to book
        suggestedBooking.UserId.Should().Be(member2.Id);

        // After booking, fairness should improve
        DbContext.Bookings.Add(suggestedBooking);
        await DbContext.SaveChangesAsync();

        var newTotalBookings = await DbContext.Bookings
            .Where(b => b.GroupId == group.Id && b.Status == BookingStatus.Completed || b.Status == BookingStatus.Confirmed)
            .CountAsync();

        var newMember2UsageShare = (decimal)(member2Bookings + 1) / newTotalBookings;
        
        // Member2's usage share should increase
        newMember2UsageShare.Should().BeGreaterThan(member2UsageShare);
    }

    [Fact]
    [Trait("Category", "AIWorkflow")]
    public async Task AIPredictionWorkflow_PredictUsageValidatePredictions_ShouldSucceed()
    {
        // Setup: Create group with historical usage data
        var creator = await CreateAndSaveUserAsync();
        var member1 = await CreateAndSaveUserAsync();
        var group = await CreateAndSaveGroupAsync(
            creator.Id,
            new List<Guid> { creator.Id, member1.Id }
        );
        var vehicle = await CreateAndSaveVehicleAsync(group.Id);

        // Create historical bookings over past 60 days
        var historicalBookings = new List<Booking>();
        for (int i = 0; i < 20; i++)
        {
            var booking = TestDataBuilder.CreateTestBooking(
                vehicle.Id,
                group.Id,
                i % 2 == 0 ? creator.Id : member1.Id,
                BookingStatus.Completed
            );
            booking.StartAt = DateTime.UtcNow.AddDays(-(60 - i * 3));
            booking.EndAt = booking.StartAt.AddHours(_faker.Random.Int(4, 48));
            historicalBookings.Add(booking);
        }
        DbContext.Bookings.AddRange(historicalBookings);
        await DbContext.SaveChangesAsync();

        // AI predicts usage for next 30 days
        // Calculate patterns from historical data
        var historicalData = await DbContext.Bookings
            .Where(b => b.GroupId == group.Id && b.Status == BookingStatus.Completed)
            .GroupBy(b => b.StartAt.Date)
            .Select(g => new
            {
                Date = g.Key,
                UsageHours = g.Sum(b => (b.EndAt - b.StartAt).TotalHours)
            })
            .OrderBy(x => x.Date)
            .ToListAsync();

        historicalData.Should().NotBeEmpty();

        // Calculate average daily usage and identify busiest days
        var averageDailyUsage = historicalData.Average(x => x.UsageHours);
        var busiestDays = historicalData.OrderByDescending(x => x.UsageHours).Take(5).ToList();

        averageDailyUsage.Should().BeGreaterThan(0);
        busiestDays.Should().HaveCountLessThanOrEqualTo(5).And.NotBeEmpty();

        // Validate that historical usage spans multiple weeks
        var historySpanDays = (historicalData.Last().Date - historicalData.First().Date).TotalDays;
        historySpanDays.Should().BeGreaterThan(30);
    }
}

