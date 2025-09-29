using Microsoft.EntityFrameworkCore;
using CoOwnershipVehicle.Data;
using CoOwnershipVehicle.Domain.Entities;
using System.Text.Json;
using System.Text.Json.Serialization;
using UserEntity = CoOwnershipVehicle.Domain.Entities.User;

namespace CoOwnershipVehicle.User.Api.Services;

public interface IUserSyncService
{
    Task<UserEntity?> GetUserFromAuthServiceAsync(Guid userId);
    Task<UserEntity> SyncUserAsync(Guid userId);
}

public class UserSyncService : IUserSyncService
{
    private readonly ApplicationDbContext _context;
    private readonly HttpClient _httpClient;
    private readonly ILogger<UserSyncService> _logger;
    private readonly IConfiguration _configuration;

    public UserSyncService(
        ApplicationDbContext context, 
        HttpClient httpClient, 
        ILogger<UserSyncService> logger,
        IConfiguration configuration)
    {
        _context = context;
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<UserEntity?> GetUserFromAuthServiceAsync(Guid userId)
    {
        try
        {
            // Call Auth service to get user data
            var authServiceUrl = _configuration["AuthServiceUrl"] ?? "https://localhost:61601";
            var requestUrl = $"{authServiceUrl}/api/Auth/user/{userId}";
            
            _logger.LogInformation("Attempting to fetch user {UserId} from Auth service at {Url}", userId, requestUrl);
            
            var response = await _httpClient.GetAsync(requestUrl);
            
            _logger.LogInformation("Auth service response: Status {StatusCode}", response.StatusCode);
            
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Auth service response body: {Json}", json);
                
                var authUser = JsonSerializer.Deserialize<AuthUserResponse>(json);
                
                if (authUser != null)
                {
                    _logger.LogInformation("Successfully deserialized user data for {UserId}", userId);
                    _logger.LogInformation("Deserialized AuthUserResponse: Id={Id}, Email='{Email}', FirstName='{FirstName}', LastName='{LastName}'", 
                        authUser.Id, authUser.Email, authUser.FirstName, authUser.LastName);
                    
                    var userEntity = new UserEntity
                    {
                        Id = authUser.Id,
                        Email = authUser.Email,
                        NormalizedEmail = authUser.Email.ToUpperInvariant(),
                        UserName = authUser.Email,
                        NormalizedUserName = authUser.Email.ToUpperInvariant(),
                        FirstName = authUser.FirstName,
                        LastName = authUser.LastName,
                        Phone = authUser.Phone ?? "",
                        PhoneNumber = authUser.Phone ?? "",
                        Role = (UserRole)authUser.Role,
                        KycStatus = (KycStatus)authUser.KycStatus,
                        CreatedAt = authUser.CreatedAt,
                        UpdatedAt = DateTime.UtcNow,
                        EmailConfirmed = true, // Since they're already in Auth service
                        PhoneNumberConfirmed = false,
                        TwoFactorEnabled = false,
                        LockoutEnabled = true,
                        AccessFailedCount = 0,
                        ConcurrencyStamp = Guid.NewGuid().ToString(),
                        SecurityStamp = Guid.NewGuid().ToString()
                    };
                    
                    _logger.LogInformation("Created UserEntity: Id={Id}, Email='{Email}', FirstName='{FirstName}', LastName='{LastName}'", 
                        userEntity.Id, userEntity.Email, userEntity.FirstName, userEntity.LastName);
                    
                    return userEntity;
                }
                else
                {
                    _logger.LogWarning("Failed to deserialize user data for {UserId}", userId);
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Auth service returned error {StatusCode}: {Content}", response.StatusCode, errorContent);
            }
            
            _logger.LogWarning("Could not fetch user {UserId} from Auth service", userId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching user {UserId} from Auth service", userId);
            return null;
        }
    }

    public async Task<UserEntity> SyncUserAsync(Guid userId)
    {
        var connectionString = _context.Database.GetConnectionString();
        _logger.LogInformation("Starting sync for user {UserId}. Connection: {ConnectionString}", 
            userId, connectionString);
            
        // Check if user exists in our local database
        var localUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        
        if (localUser == null)
        {
            // User doesn't exist locally, fetch from Auth service
            var authUser = await GetUserFromAuthServiceAsync(userId);
            
            if (authUser != null)
            {
                // Check if a user with the same email already exists (in case of data inconsistency)
                _logger.LogInformation("Checking for existing user with email: '{Email}'", authUser.Email);
                
                // Let's also check what users are currently in the database
                var dbConnectionString = _context.Database.GetConnectionString();
                _logger.LogInformation("UserSyncService is querying database: {ConnectionString}", dbConnectionString);
                
                var allUsers = await _context.Users.Select(u => new { u.Id, u.Email, u.FirstName, u.LastName }).ToListAsync();
                _logger.LogInformation("Current users in database: {UserCount} users", allUsers.Count);
                foreach (var user in allUsers)
                {
                    _logger.LogInformation("  User: ID={Id}, Email='{Email}', Name='{FirstName} {LastName}'", 
                        user.Id, user.Email, user.FirstName, user.LastName);
                }
                
                // Clean up any corrupted users with empty emails first
                var corruptedUsers = await _context.Users.Where(u => string.IsNullOrEmpty(u.Email)).ToListAsync();
                if (corruptedUsers.Any())
                {
                    _logger.LogWarning("Found {Count} corrupted users with empty emails, removing them", corruptedUsers.Count);
                    _context.Users.RemoveRange(corruptedUsers);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Removed {Count} corrupted users", corruptedUsers.Count);
                }
                
                // Now check for existing user with the same email (only if email is not empty)
                var existingUserWithEmail = !string.IsNullOrEmpty(authUser.Email) 
                    ? await _context.Users.FirstOrDefaultAsync(u => u.Email == authUser.Email)
                    : null;
                
                if (existingUserWithEmail != null)
                {
                    _logger.LogWarning("User with email '{Email}' already exists locally but with different ID. Old ID: {OldId}, New ID: {NewId}", 
                        authUser.Email, existingUserWithEmail.Id, userId);
                    _logger.LogInformation("Existing user details: Email='{ExistingEmail}', FirstName='{FirstName}', LastName='{LastName}'", 
                        existingUserWithEmail.Email, existingUserWithEmail.FirstName, existingUserWithEmail.LastName);
                    
                    // Entity Framework doesn't allow changing primary keys, so we need to:
                    // 1. Delete the old user
                    // 2. Add the new user with correct ID
                    _context.Users.Remove(existingUserWithEmail);
                    _logger.LogInformation("Removed old user {OldId} to make way for new user {NewId}", 
                        existingUserWithEmail.Id, userId);
                    
                    // Now add the new user with the correct ID
                    _context.Users.Add(authUser);
                    _logger.LogInformation("Added new user {NewId} with correct ID from Auth service", userId);
                    
                    try
                    {
                        var changes = await _context.SaveChangesAsync();
                        _logger.LogInformation("Successfully replaced user in database. Changes: {Changes}", changes);
                        
                        // Clear the change tracker to ensure fresh data for verification
                        _context.ChangeTracker.Clear();
                        
                        // Verify the user was actually saved using fresh data
                        var savedUser = await _context.Users
                            .AsNoTracking()
                            .FirstOrDefaultAsync(u => u.Id == userId);
                        
                        _logger.LogInformation("Replaced user {UserId} from Auth service. Verification: {UserExists}", 
                            userId, savedUser != null ? "SUCCESS" : "FAILED");
                            
                        if (savedUser != null)
                        {
                            _logger.LogInformation("Verified user details: Email={Email}, FirstName={FirstName}, LastName={LastName}", 
                                savedUser.Email, savedUser.FirstName, savedUser.LastName);
                        }
                        
                        return authUser;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to replace user {UserId} in database. Error: {ErrorMessage}", userId, ex.Message);
                        throw;
                    }
                }
                
                // Add to local database (only if no existing user with same email)
                _context.Users.Add(authUser);
                
                try
                {
                    var changes = await _context.SaveChangesAsync();
                    _logger.LogInformation("Successfully saved user {UserId} to database. Changes: {Changes}", userId, changes);
                    
                    // Clear the change tracker to ensure fresh data for verification
                    _context.ChangeTracker.Clear();
                    
                    // Verify the user was actually saved using fresh data
                    var savedUser = await _context.Users
                        .AsNoTracking()
                        .FirstOrDefaultAsync(u => u.Id == userId);
                    
                    _logger.LogInformation("Synced new user {UserId} from Auth service. Verification: {UserExists}", 
                        userId, savedUser != null ? "SUCCESS" : "FAILED");
                        
                    if (savedUser != null)
                    {
                        _logger.LogInformation("Verified user details: Email={Email}, FirstName={FirstName}, LastName={LastName}", 
                            savedUser.Email, savedUser.FirstName, savedUser.LastName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save user {UserId} to database. Error: {ErrorMessage}", userId, ex.Message);
                    throw;
                }
                
                return authUser;
            }
            else
            {
                throw new InvalidOperationException($"User {userId} not found in Auth service");
            }
        }
        else
        {
            // User exists locally, optionally sync latest data
            var authUser = await GetUserFromAuthServiceAsync(userId);
            
            if (authUser != null)
            {
                // Update local user with latest data from Auth service
                localUser.Email = authUser.Email;
                localUser.NormalizedEmail = authUser.Email?.ToUpperInvariant() ?? localUser.Email?.ToUpperInvariant() ?? string.Empty;
                localUser.FirstName = authUser.FirstName;
                localUser.LastName = authUser.LastName;
                localUser.Phone = authUser.Phone ?? localUser.Phone;
                localUser.PhoneNumber = authUser.Phone ?? localUser.PhoneNumber;
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
        public string Phone { get; set; } = string.Empty;
        
        [JsonPropertyName("role")]
        public int Role { get; set; }
        
        [JsonPropertyName("kycStatus")]
        public int KycStatus { get; set; }
        
        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; }
    }
}
