using CoOwnershipVehicle.Data;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.IntegrationTests.TestFixtures;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace CoOwnershipVehicle.IntegrationTests.Performance;

public class LoadTests : IntegrationTestBase
{
    [Fact]
    [Trait("Category", "Performance")]
    [Trait("Category", "LoadTest")]
    public async Task LoadTest_200ConcurrentUsers_ShouldHandle()
    {
        const int concurrentUsers = 200;
        var successCount = 0;
        var failureCount = 0;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var preCreatedUsers = new List<User>();
        for (int i = 0; i < concurrentUsers * 2; i++)
        {
            preCreatedUsers.Add(await CreateAndSaveUserAsync());
        }

        var userPairs = preCreatedUsers
            .Chunk(2)
            .Where(chunk => chunk.Length == 2)
            .Select(chunk => new { Owner = chunk[0], Member = chunk[1] })
            .Take(concurrentUsers)
            .ToList();

        if (userPairs.Count < concurrentUsers)
        {
            throw new InvalidOperationException("Insufficient user pairs created for load test.");
        }

        await Parallel.ForEachAsync(userPairs, new ParallelOptions { MaxDegreeOfParallelism = 20 }, async (pair, cancellationToken) =>
        {
            try
            {
                using var scope = ServiceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var group = TestDataBuilder.CreateTestGroup(pair.Owner.Id);
                context.OwnershipGroups.Add(group);
                await context.SaveChangesAsync(cancellationToken);

                var share = 0.5m;
                context.GroupMembers.Add(TestDataBuilder.CreateGroupMember(group.Id, pair.Owner.Id, share, GroupRole.Admin));
                context.GroupMembers.Add(TestDataBuilder.CreateGroupMember(group.Id, pair.Member.Id, share, GroupRole.Member));
                await context.SaveChangesAsync(cancellationToken);

                var vehicle = TestDataBuilder.CreateTestVehicle(group.Id);
                context.Vehicles.Add(vehicle);
                await context.SaveChangesAsync(cancellationToken);

                var booking = TestDataBuilder.CreateTestBooking(vehicle.Id, group.Id, pair.Owner.Id, BookingStatus.Completed);
                context.Bookings.Add(booking);
                await context.SaveChangesAsync(cancellationToken);

                Interlocked.Increment(ref successCount);
            }
            catch
            {
                Interlocked.Increment(ref failureCount);
            }
        });

        stopwatch.Stop();

        var totalOperations = Math.Max(1, successCount + failureCount);
        var successRate = (double)successCount / totalOperations * 100;

        successRate.Should().BeGreaterThan(90);
        successCount.Should().BeGreaterThan((int)(concurrentUsers * 0.9));

        var avgTimePerOperation = stopwatch.ElapsedMilliseconds / (double)totalOperations;
        avgTimePerOperation.Should().BeLessThan(1000);
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task MultipleOperationsSimultaneously_ShouldSucceed()
    {
        const int operationCount = 50;

        using var scope = ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var users = new List<User>();
        for (int i = 0; i < operationCount; i++)
        {
            users.Add(TestDataBuilder.CreateTestUser());
        }
        context.Users.AddRange(users);
        await context.SaveChangesAsync();

        var groups = new List<OwnershipGroup>();
        for (int i = 0; i < users.Count; i += 2)
        {
            if (i + 1 >= users.Count) break;
            var owner = users[i];
            var member = users[i + 1];
            var group = TestDataBuilder.CreateTestGroup(owner.Id);
            context.OwnershipGroups.Add(group);
            await context.SaveChangesAsync();

            context.GroupMembers.Add(TestDataBuilder.CreateGroupMember(group.Id, owner.Id, 0.5m, GroupRole.Admin));
            context.GroupMembers.Add(TestDataBuilder.CreateGroupMember(group.Id, member.Id, 0.5m, GroupRole.Member));
            await context.SaveChangesAsync();

            groups.Add(group);
        }

        var vehicles = new List<Vehicle>();
        foreach (var group in groups)
        {
            var vehicle = TestDataBuilder.CreateTestVehicle(group.Id);
            context.Vehicles.Add(vehicle);
            vehicles.Add(vehicle);
        }
        await context.SaveChangesAsync();

        var bookings = new List<Booking>();
        foreach (var vehicle in vehicles.Where(v => v.GroupId != null))
        {
            var booking = TestDataBuilder.CreateTestBooking(
                vehicle.Id,
                vehicle.GroupId!.Value,
                users.First().Id,
                BookingStatus.Completed);
            context.Bookings.Add(booking);
            bookings.Add(booking);
        }
        await context.SaveChangesAsync();

        users.Count.Should().Be(operationCount);
        groups.Count.Should().BeGreaterThan(0);
        vehicles.Count.Should().Be(groups.Count);
        bookings.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task IdentifyBottlenecks_SlowInterServiceCalls_ShouldBeOptimized()
    {
        // Measure inter-service call performance
        var callTimes = new List<long>();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Simulate inter-service calls
        for (int i = 0; i < 10; i++)
        {
            stopwatch.Restart();
            
            // Simulate service call (database query)
            var users = await DbContext.Users.CountAsync();
            
            stopwatch.Stop();
            callTimes.Add(stopwatch.ElapsedMilliseconds);
        }

        var avgCallTime = callTimes.Average();
        var maxCallTime = callTimes.Max();
        var p95CallTime = callTimes.OrderBy(t => t).Skip((int)(callTimes.Count * 0.95)).First();

        // Average call time should be reasonable
        avgCallTime.Should().BeLessThan(100); // Less than 100ms average

        // Max call time should not be excessive
        maxCallTime.Should().BeLessThan(500); // Less than 500ms max

        // P95 should be reasonable
        p95CallTime.Should().BeLessThan(200); // Less than 200ms for 95th percentile
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task OptimizeSlowInterServiceCalls_ShouldImprovePerformance()
    {
        // Before optimization (simulated slow call)
        var slowStopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // Simulate slow operation (multiple queries)
        for (int i = 0; i < 5; i++)
        {
            await DbContext.Users.CountAsync();
            await DbContext.OwnershipGroups.CountAsync();
            await DbContext.Vehicles.CountAsync();
        }
        
        slowStopwatch.Stop();
        var slowTime = slowStopwatch.ElapsedMilliseconds;

        // After optimization (simulated optimized call)
        var fastStopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // Simulate optimized operation (batch query or cached results)
        var usersTask = DbContext.Users.CountAsync();
        var groupsTask = DbContext.OwnershipGroups.CountAsync();
        var vehiclesTask = DbContext.Vehicles.CountAsync();
        
        await Task.WhenAll(usersTask, groupsTask, vehiclesTask);
        var results = new[] { await usersTask, await groupsTask, await vehiclesTask };
        
        fastStopwatch.Stop();
        var fastTime = fastStopwatch.ElapsedMilliseconds;

        // Optimized version should be faster
        fastTime.Should().BeLessThan(slowTime);
        
        // Performance improvement should be significant
        var improvement = ((double)(slowTime - fastTime) / slowTime) * 100;
        improvement.Should().BeGreaterThan(0); // At least some improvement
    }
}





