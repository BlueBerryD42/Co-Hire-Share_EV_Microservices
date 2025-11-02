using CoOwnershipVehicle.Data;
using CoOwnershipVehicle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoOwnershipVehicle.IntegrationTests.TestFixtures;

public abstract class IntegrationTestBase : IDisposable
{
    protected ApplicationDbContext DbContext { get; private set; }
    protected ServiceProvider ServiceProvider { get; private set; }
    protected HttpClient HttpClient { get; private set; }
    protected Dictionary<string, ServiceClientHelper> ServiceClients { get; private set; }

    protected IntegrationTestBase()
    {
        // Setup in-memory database
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));
        
        ServiceProvider = services.BuildServiceProvider();
        DbContext = ServiceProvider.GetRequiredService<ApplicationDbContext>();
        DbContext.Database.EnsureCreated();

        // Setup HTTP clients for services
        HttpClient = new HttpClient();
        ServiceClients = new Dictionary<string, ServiceClientHelper>
        {
            { "apiGateway", new ServiceClientHelper(new HttpClient(), "https://localhost:7000") },
            { "auth", new ServiceClientHelper(new HttpClient(), "https://localhost:5002") },
            { "user", new ServiceClientHelper(new HttpClient(), "https://localhost:5008") },
            { "booking", new ServiceClientHelper(new HttpClient(), "https://localhost:5003") },
            { "vehicle", new ServiceClientHelper(new HttpClient(), "https://localhost:5009") },
            { "payment", new ServiceClientHelper(new HttpClient(), "https://localhost:5007") },
            { "group", new ServiceClientHelper(new HttpClient(), "https://localhost:5004") },
            { "analytics", new ServiceClientHelper(new HttpClient(), "https://localhost:5001") },
            { "admin", new ServiceClientHelper(new HttpClient(), "https://localhost:61610") }
        };
    }

    protected async Task<User> CreateAndSaveUserAsync(UserRole role = UserRole.CoOwner, KycStatus kycStatus = KycStatus.Pending)
    {
        var user = TestDataBuilder.CreateTestUser(role, kycStatus);
        DbContext.Users.Add(user);
        await DbContext.SaveChangesAsync();
        return user;
    }

    protected async Task<OwnershipGroup> CreateAndSaveGroupAsync(Guid createdBy, List<Guid> memberIds)
    {
        var group = TestDataBuilder.CreateTestGroup(createdBy);
        DbContext.OwnershipGroups.Add(group);
        await DbContext.SaveChangesAsync();

        var totalMembers = memberIds.Count;
        var sharePerMember = 1.0m / totalMembers;
        
        foreach (var memberId in memberIds)
        {
            var member = TestDataBuilder.CreateGroupMember(
                group.Id, 
                memberId, 
                sharePerMember,
                memberId == createdBy ? GroupRole.Owner : GroupRole.Member
            );
            DbContext.GroupMembers.Add(member);
        }

        await DbContext.SaveChangesAsync();
        return group;
    }

    protected async Task<Vehicle> CreateAndSaveVehicleAsync(Guid? groupId = null)
    {
        var vehicle = TestDataBuilder.CreateTestVehicle(groupId);
        DbContext.Vehicles.Add(vehicle);
        await DbContext.SaveChangesAsync();
        return vehicle;
    }

    protected async Task<string> AuthenticateUserAsync(Guid userId, string email, string password = "TestPassword123!")
    {
        // In a real scenario, this would call the Auth service
        // For integration tests, we might mock or use test authentication
        var loginRequest = new
        {
            Email = email,
            Password = password
        };

        var response = await ServiceClients["auth"].PostRawAsync("/api/auth/login", loginRequest);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            var json = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(content);
            return json?["token"]?.ToString() ?? string.Empty;
        }

        // Fallback: Generate a test token (in real tests, use proper JWT generation)
        return $"test-token-{userId}";
    }

    public void Dispose()
    {
        DbContext?.Database.EnsureDeleted();
        DbContext?.Dispose();
        HttpClient?.Dispose();
        ServiceProvider?.Dispose();
        
        foreach (var client in ServiceClients.Values)
        {
            // Clients will be disposed by HttpClient
        }
    }
}

