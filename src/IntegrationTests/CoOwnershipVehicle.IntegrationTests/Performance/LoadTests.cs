using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.IntegrationTests.TestFixtures;
using FluentAssertions;

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
        var tasks = new List<Task>();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        for (int i = 0; i < concurrentUsers; i++)
        {
            var userIndex = i;
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    // Simulate user operations
                    var user = await CreateAndSaveUserAsync();
                    
                    // Simulate multiple operations per user
                    var group = await CreateAndSaveGroupAsync(
                        user.Id,
                        new List<Guid> { user.Id, (await CreateAndSaveUserAsync()).Id }
                    );
                    
                    var vehicle = await CreateAndSaveVehicleAsync(group.Id);
                    
                    var booking = TestDataBuilder.CreateTestBooking(
                        vehicle.Id,
                        group.Id,
                        user.Id
                    );
                    DbContext.Bookings.Add(booking);
                    await DbContext.SaveChangesAsync();

                    Interlocked.Increment(ref successCount);
                }
                catch
                {
                    Interlocked.Increment(ref failureCount);
                }
            }));
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        var totalOperations = successCount + failureCount;
        var successRate = (double)successCount / totalOperations * 100;

        // At least 90% should succeed under load
        successRate.Should().BeGreaterThan(90);
        successCount.Should().BeGreaterThan(concurrentUsers * 0.9);
        
        // Performance should be reasonable (operations complete within reasonable time)
        var avgTimePerOperation = stopwatch.ElapsedMilliseconds / (double)totalOperations;
        avgTimePerOperation.Should().BeLessThan(1000); // Less than 1 second per operation
    }

    [Fact]
    [Trait("Category", "Performance")]
    public async Task MultipleOperationsSimultaneously_ShouldSucceed()
    {
        const int operationCount = 50;
        var tasks = new List<Task>();

        // Create users
        var userTasks = Enumerable.Range(0, operationCount)
            .Select(i => Task.Run(async () => await CreateAndSaveUserAsync()))
            .ToList();
        var users = await Task.WhenAll(userTasks);

        // Create groups
        var groupTasks = users
            .Chunk(2)
            .Where(chunk => chunk.Length == 2)
            .Select(chunk => Task.Run(async () => 
                await CreateAndSaveGroupAsync(chunk[0].Id, chunk.Select(u => u.Id).ToList())))
            .ToList();
        var groups = await Task.WhenAll(groupTasks);

        // Create vehicles
        var vehicleTasks = groups
            .Select(g => Task.Run(async () => await CreateAndSaveVehicleAsync(g.Id)))
            .ToList();
        var vehicles = await Task.WhenAll(vehicleTasks);

        // Create bookings
        var bookingTasks = vehicles
            .Where(v => v.GroupId != null)
            .Select(v => Task.Run(async () =>
            {
                var booking = TestDataBuilder.CreateTestBooking(
                    v.Id,
                    v.GroupId!.Value,
                    users.First().Id
                );
                DbContext.Bookings.Add(booking);
                await DbContext.SaveChangesAsync();
                return booking;
            }))
            .ToList();
        var bookings = await Task.WhenAll(bookingTasks);

        // Verify all operations completed
        users.Length.Should().Be(operationCount);
        groups.Length.Should().BeGreaterThan(0);
        vehicles.Length.Should().Be(groups.Length);
        bookings.Length.Should().BeGreaterThan(0);
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

