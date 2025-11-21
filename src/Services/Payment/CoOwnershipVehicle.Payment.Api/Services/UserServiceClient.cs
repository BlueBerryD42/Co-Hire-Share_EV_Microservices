using System.Net.Http.Headers;
using System.Text.Json;
using CoOwnershipVehicle.Payment.Api.Services.Interfaces;

namespace CoOwnershipVehicle.Payment.Api.Services;

/// <summary>
/// HTTP client implementation for communicating with User Service
/// Fetches user data via HTTP since Payment service uses microservices architecture
/// </summary>
public class UserServiceClient : IUserServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UserServiceClient> _logger;
    private readonly IConfiguration _configuration;

    public UserServiceClient(
        HttpClient httpClient,
        ILogger<UserServiceClient> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<UserInfoDto?> GetUserAsync(Guid userId, string accessToken)
    {
        try
        {
            var requestUrl = $"/api/User/basic/{userId}";

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            _logger.LogInformation("Fetching user {UserId} from User service", userId);
            var response = await _httpClient.GetAsync(requestUrl);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var userData = JsonSerializer.Deserialize<JsonElement>(json, options);
                
                if (userData.ValueKind == JsonValueKind.Object)
                {
                    // Try both camelCase and PascalCase property names for compatibility
                    var idProp = userData.TryGetProperty("id", out var idLower) ? idLower : 
                                 (userData.TryGetProperty("Id", out var idUpper) ? idUpper : default);
                    var emailProp = userData.TryGetProperty("email", out var emailLower) ? emailLower : 
                                    (userData.TryGetProperty("Email", out var emailUpper) ? emailUpper : default);
                    var firstNameProp = userData.TryGetProperty("firstName", out var firstNameLower) ? firstNameLower : 
                                        (userData.TryGetProperty("FirstName", out var firstNameUpper) ? firstNameUpper : default);
                    var lastNameProp = userData.TryGetProperty("lastName", out var lastNameLower) ? lastNameLower : 
                                       (userData.TryGetProperty("LastName", out var lastNameUpper) ? lastNameUpper : default);
                    var phoneProp = userData.TryGetProperty("phone", out var phoneLower) ? phoneLower : 
                                    (userData.TryGetProperty("Phone", out var phoneUpper) ? phoneUpper : default);
                    
                    var userInfo = new UserInfoDto
                    {
                        Id = idProp.ValueKind != JsonValueKind.Null && Guid.TryParse(idProp.GetString(), out var id) ? id : userId,
                        Email = emailProp.ValueKind != JsonValueKind.Null ? emailProp.GetString() ?? string.Empty : string.Empty,
                        FirstName = firstNameProp.ValueKind != JsonValueKind.Null ? firstNameProp.GetString() ?? string.Empty : string.Empty,
                        LastName = lastNameProp.ValueKind != JsonValueKind.Null ? lastNameProp.GetString() ?? string.Empty : string.Empty,
                        Phone = phoneProp.ValueKind != JsonValueKind.Null ? phoneProp.GetString() : null
                    };
                    
                    _logger.LogInformation("Successfully fetched user {UserId}: {FirstName} {LastName}", 
                        userId, userInfo.FirstName, userInfo.LastName);
                    return userInfo;
                }
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("User {UserId} not found in User Service (404)", userId);
                return null;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to get user {UserId} from User Service. Status: {StatusCode}, Response: {Response}", 
                    userId, response.StatusCode, errorContent);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling User Service for user {UserId}", userId);
            return null;
        }
    }

    public async Task<Dictionary<Guid, UserInfoDto>> GetUsersAsync(List<Guid> userIds, string accessToken)
    {
        var result = new Dictionary<Guid, UserInfoDto>();
        
        // Fetch users in parallel
        var tasks = userIds.Select(userId => GetUserAsync(userId, accessToken));
        var users = await Task.WhenAll(tasks);

        foreach (var user in users)
        {
            if (user != null)
            {
                result[user.Id] = user;
            }
        }

        return result;
    }
}


