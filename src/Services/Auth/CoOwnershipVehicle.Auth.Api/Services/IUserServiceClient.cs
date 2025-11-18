using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Auth.Api.Services;

/// <summary>
/// Client interface for communicating with User service to get user profile data for JWT token generation
/// </summary>
public interface IUserServiceClient
{
    Task<UserRoleInfo?> GetUserRoleInfoAsync(Guid userId);
}

public class UserRoleInfo
{
    public Guid UserId { get; set; }
    public UserRole Role { get; set; }
    public KycStatus KycStatus { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}

public class UserServiceClient : IUserServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<UserServiceClient> _logger;

    public UserServiceClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<UserServiceClient> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<UserRoleInfo?> GetUserRoleInfoAsync(Guid userId)
    {
        try
        {
            // Use BaseAddress from HttpClient configuration (configured in Program.cs)
            var requestUrl = $"/api/user/internal/role/{userId}";

            // Get service key for internal service-to-service communication
            var serviceKey = _configuration["ServiceKeys:Internal"]
                ?? Environment.GetEnvironmentVariable("SERVICE_KEY_INTERNAL")
                ?? "internal-service-key-change-in-production-2024";  // Match .env default

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("X-Service-Key", serviceKey);

            _logger.LogInformation("Fetching user role info for {UserId} from User service. BaseAddress: {BaseAddress}, RequestUrl: {RequestUrl}, ServiceKey: {ServiceKey}",
                userId, _httpClient.BaseAddress?.ToString() ?? "NULL", requestUrl, serviceKey.Substring(0, Math.Min(10, serviceKey.Length)) + "...");

            var response = await _httpClient.GetAsync(requestUrl);
            
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("User {UserId} not found in User service", userId);
                    return null;
                }
                
                _logger.LogWarning("Failed to fetch user role info for {UserId}. Status: {StatusCode}", 
                    userId, response.StatusCode);
                return null;
            }
            
            var json = await response.Content.ReadAsStringAsync();
            var roleInfo = System.Text.Json.JsonSerializer.Deserialize<UserRoleInfoResponse>(json, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            if (roleInfo == null)
            {
                _logger.LogWarning("Failed to deserialize user role info for {UserId}", userId);
                return null;
            }
            
            // Parse enum values
            if (!Enum.TryParse<UserRole>(roleInfo.Role, out var userRole))
            {
                _logger.LogWarning("Invalid role value '{Role}' for user {UserId}, defaulting to CoOwner", 
                    roleInfo.Role, userId);
                userRole = UserRole.CoOwner;
            }
            
            if (!Enum.TryParse<KycStatus>(roleInfo.KycStatus, out var kycStatus))
            {
                _logger.LogWarning("Invalid KYC status value '{KycStatus}' for user {UserId}, defaulting to Pending", 
                    roleInfo.KycStatus, userId);
                kycStatus = KycStatus.Pending;
            }
            
            return new UserRoleInfo
            {
                UserId = roleInfo.UserId,
                Role = userRole,
                KycStatus = kycStatus,
                FirstName = roleInfo.FirstName ?? string.Empty,
                LastName = roleInfo.LastName ?? string.Empty
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching user role info for {UserId} from User service", userId);
            return null; // Return null on error - JWT generation will use defaults
        }
    }
    
    private class UserRoleInfoResponse
    {
        public Guid UserId { get; set; }
        public string Role { get; set; } = string.Empty;
        public string KycStatus { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
    }
}

