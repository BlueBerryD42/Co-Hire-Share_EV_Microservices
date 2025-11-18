using Microsoft.EntityFrameworkCore;
using CoOwnershipVehicle.User.Api.Data;
using CoOwnershipVehicle.Domain.Entities;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CoOwnershipVehicle.User.Api.Services;

public interface IUserSyncService
{
    Task<UserProfile?> GetUserFromAuthServiceAsync(Guid userId);
    Task<UserProfile> SyncUserAsync(Guid userId);
}

/// <summary>
/// Service for syncing user data from Auth service via HTTP.
/// Uses HTTP for synchronous queries (consistent with other services like Vehicle, Admin, Analytics).
/// UserRegisteredEvent handles initial creation, but HTTP is used for sync operations.
/// </summary>
public class UserSyncService : IUserSyncService
{
    private readonly UserDbContext _context;
    private readonly HttpClient _httpClient;
    private readonly ILogger<UserSyncService> _logger;
    private readonly IConfiguration _configuration;

    public UserSyncService(
        UserDbContext context, 
        HttpClient httpClient, 
        ILogger<UserSyncService> logger,
        IConfiguration configuration)
    {
        _context = context;
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<UserProfile?> GetUserFromAuthServiceAsync(Guid userId)
    {
        try
        {
            // Call Auth service to get user data (HTTP pattern, consistent with other services)
            var authServiceUrl = _configuration["ServiceUrls:Auth"] ?? _configuration["AuthServiceUrl"] ?? "http://localhost:61601";
            var requestUrl = $"{authServiceUrl}/api/Auth/user/{userId}";
            
            _logger.LogInformation("Fetching user {UserId} from Auth service at {Url}", userId, requestUrl);
            
            var response = await _httpClient.GetAsync(requestUrl);
            
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var authUser = JsonSerializer.Deserialize<AuthUserResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (authUser != null)
                {
                    var userProfile = new UserProfile
                    {
                        Id = authUser.Id,
                        Email = authUser.Email,
                        NormalizedEmail = authUser.Email.ToUpperInvariant(),
                        UserName = authUser.Email,
                        NormalizedUserName = authUser.Email.ToUpperInvariant(),
                        FirstName = authUser.FirstName,
                        LastName = authUser.LastName,
                        Phone = authUser.Phone, // Phone field (profile data)
                        Role = (UserRole)authUser.Role,
                        KycStatus = (KycStatus)authUser.KycStatus,
                        CreatedAt = authUser.CreatedAt,
                        UpdatedAt = DateTime.UtcNow,
                        EmailConfirmed = true,
                        TwoFactorEnabled = false,
                        LockoutEnabled = true,
                        AccessFailedCount = 0,
                        ConcurrencyStamp = Guid.NewGuid().ToString()
                        // PasswordHash, SecurityStamp, PhoneNumber, PhoneNumberConfirmed are NOT stored in User DB
                    };
                    
                    return userProfile;
                }
            }
            else
            {
                _logger.LogWarning("Auth service returned {StatusCode} for user {UserId}", response.StatusCode, userId);
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching user {UserId} from Auth service", userId);
            return null;
        }
    }

    public async Task<UserProfile> SyncUserAsync(Guid userId)
    {
        _logger.LogInformation("Syncing user {UserId}", userId);
            
        // Check if user exists in local database first
        var localUser = await _context.UserProfiles.FirstOrDefaultAsync(u => u.Id == userId);
        
        if (localUser == null)
        {
            // User doesn't exist locally, fetch from Auth service via HTTP
            var authUser = await GetUserFromAuthServiceAsync(userId);
            
            if (authUser != null)
            {
                // Check for existing user with same email (data inconsistency)
                var existingUserWithEmail = !string.IsNullOrEmpty(authUser.Email) 
                    ? await _context.UserProfiles.FirstOrDefaultAsync(u => u.Email == authUser.Email)
                    : null;
                
                if (existingUserWithEmail != null && existingUserWithEmail.Id != userId)
                {
                    _logger.LogWarning("User with email '{Email}' exists with different ID. Removing old user.", authUser.Email);
                    _context.UserProfiles.Remove(existingUserWithEmail);
                }
                
                // Add new user profile
                _context.UserProfiles.Add(authUser);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Synced user {UserId} from Auth service", userId);
                return authUser;
            }
            else
            {
                throw new InvalidOperationException($"User {userId} not found in Auth service");
            }
        }
        else
        {
            // User exists locally, optionally sync latest data from Auth service
            var authUser = await GetUserFromAuthServiceAsync(userId);
            
            if (authUser != null)
            {
                // Update local user profile with latest data
                localUser.Email = authUser.Email;
                localUser.NormalizedEmail = authUser.Email?.ToUpperInvariant() ?? localUser.Email?.ToUpperInvariant() ?? string.Empty;
                localUser.FirstName = authUser.FirstName;
                localUser.LastName = authUser.LastName;
                localUser.Phone = authUser.Phone ?? localUser.Phone;
                localUser.Role = authUser.Role;
                localUser.KycStatus = authUser.KycStatus;
                localUser.UpdatedAt = DateTime.UtcNow;
                
                await _context.SaveChangesAsync();
                _logger.LogInformation("Updated local user {UserId} with data from Auth service", userId);
            }
            
            return localUser;
        }
    }

    private class AuthUserResponse
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }
        
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;
        
        [JsonPropertyName("firstName")]
        public string FirstName { get; set; } = string.Empty;
        
        [JsonPropertyName("lastName")]
        public string LastName { get; set; } = string.Empty;
        
        [JsonPropertyName("phone")]
        public string? Phone { get; set; }
        
        [JsonPropertyName("role")]
        public int Role { get; set; }
        
        [JsonPropertyName("kycStatus")]
        public int KycStatus { get; set; }
        
        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }
    }
}
