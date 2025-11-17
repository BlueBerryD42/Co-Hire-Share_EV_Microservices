using System.Net.Http.Headers;
using System.Text.Json;
using CoOwnershipVehicle.Group.Api.Services.Interfaces;
using CoOwnershipVehicle.Shared.Contracts.DTOs;

namespace CoOwnershipVehicle.Group.Api.Services;

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
            var userServiceUrl = _configuration["ServiceUrls:User"] ?? "https://localhost:61602";
            // Use the basic endpoint which allows any authenticated user to get basic user info
            var requestUrl = $"{userServiceUrl}/api/User/basic/{userId}";

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.GetAsync(requestUrl);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var userProfile = JsonSerializer.Deserialize<UserProfileDto>(json, options);
                
                if (userProfile != null)
                {
                    return new UserInfoDto
                    {
                        Id = userProfile.Id,
                        Email = userProfile.Email,
                        FirstName = userProfile.FirstName,
                        LastName = userProfile.LastName,
                        Phone = userProfile.Phone,
                        Role = userProfile.Role
                    };
                }
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("User {UserId} not found in User Service", userId);
                return null;
            }
            else
            {
                _logger.LogError("Failed to get user {UserId} from User Service. Status: {StatusCode}", 
                    userId, response.StatusCode);
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

