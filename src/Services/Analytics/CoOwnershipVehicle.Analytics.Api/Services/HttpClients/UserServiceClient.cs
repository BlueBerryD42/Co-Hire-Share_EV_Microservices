using System.Net.Http.Headers;
using System.Text.Json;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using Microsoft.AspNetCore.Http;

namespace CoOwnershipVehicle.Analytics.Api.Services.HttpClients;

public class UserServiceClient : IUserServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<UserServiceClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public UserServiceClient(
        HttpClient httpClient,
        IHttpContextAccessor httpContextAccessor,
        ILogger<UserServiceClient> logger)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    private void SetAuthorizationHeader()
    {
        var token = _httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString();
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(token);
        }
    }

    public async Task<List<UserProfileDto>> GetUsersAsync()
    {
        try
        {
            SetAuthorizationHeader();
            // Note: This assumes there's an admin endpoint to get all users
            // If not available, we may need to use a different approach
            var response = await _httpClient.GetAsync("api/User/users");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                // Try to deserialize as list directly first
                var users = JsonSerializer.Deserialize<List<UserProfileDto>>(content, _jsonOptions);
                if (users != null)
                {
                    return users;
                }
                // If that fails, try UserListResponseDto structure
                var result = JsonSerializer.Deserialize<UserListResponseDto>(content, _jsonOptions);
                if (result?.Users != null)
                {
                    // Convert UserSummaryDto to UserProfileDto
                    return result.Users.Select(u => new UserProfileDto
                    {
                        Id = u.Id,
                        Email = u.Email,
                        FirstName = u.FirstName,
                        LastName = u.LastName,
                        Role = u.Role,
                        KycStatus = u.KycStatus,
                        CreatedAt = u.CreatedAt
                    }).ToList();
                }
                return new List<UserProfileDto>();
            }
            else
            {
                _logger.LogWarning("Failed to get users. Status: {StatusCode}", response.StatusCode);
                return new List<UserProfileDto>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling User service to get users");
            return new List<UserProfileDto>();
        }
    }

    public async Task<int> GetUserCountAsync()
    {
        var users = await GetUsersAsync();
        return users.Count;
    }
}

