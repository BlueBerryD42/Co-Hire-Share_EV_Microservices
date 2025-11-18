using Microsoft.AspNetCore.Identity;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using StackExchange.Redis;

namespace CoOwnershipVehicle.Auth.Api.Services;

public interface IJwtTokenService
{
    Task<LoginResponseDto> GenerateTokenAsync(User user);
    Task<LoginResponseDto> RefreshTokenAsync(string refreshToken);
    Task RevokeTokenAsync(string refreshToken);
    Task<bool> ValidateTokenAsync(string token);
}

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;
    private readonly UserManager<User> _userManager;
    private readonly ILogger<JwtTokenService> _logger;
    private readonly IDatabase? _redisDatabase;
    private readonly string _keyPrefix;
    private readonly IUserServiceClient? _userServiceClient;
    
    // Fallback in-memory storage when Redis is not available
    private static readonly Dictionary<string, Guid> _inMemoryRefreshTokens = new();

    public JwtTokenService(
        IConfiguration configuration,
        UserManager<User> userManager,
        ILogger<JwtTokenService> logger,
        IDatabase? redisDatabase,
        IUserServiceClient? userServiceClient = null)
    {
        _configuration = configuration;
        _userManager = userManager;
        _logger = logger;
        _redisDatabase = redisDatabase;
        _keyPrefix = CoOwnershipVehicle.Shared.Configuration.EnvironmentHelper.GetRedisConfigParams(configuration).KeyPrefix;
        _userServiceClient = userServiceClient;
        
        if (_redisDatabase == null)
        {
            _logger.LogWarning("Redis database is not available. Refresh tokens will use in-memory storage.");
        }
        else
        {
            _logger.LogInformation("Redis database is available. Refresh tokens will be stored in Redis.");
        }
    }

    public async Task<LoginResponseDto> GenerateTokenAsync(User user)
    {
        var claims = await BuildClaimsAsync(user);
        var accessToken = GenerateAccessToken(claims);
        var refreshToken = GenerateRefreshToken();

        // Store refresh token (in production, use a secure store like Redis)
        await StoreRefreshTokenAsync(user.Id, refreshToken);

        // Extract user info from claims for the response DTO
        var firstName = claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.GivenName)?.Value ?? string.Empty;
        var lastName = claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Surname)?.Value ?? string.Empty;
        var roleClaim = claims.FirstOrDefault(c => c.Type == "role")?.Value ?? "CoOwner";
        var kycClaim = claims.FirstOrDefault(c => c.Type == "kyc_status")?.Value ?? "Pending";
        
        Enum.TryParse<UserRole>(roleClaim, out var userRole);
        Enum.TryParse<KycStatus>(kycClaim, out var kycStatus);

        return new LoginResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(GetTokenExpiryMinutes()),
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email!,
                FirstName = firstName,
                LastName = lastName,
                Phone = null, // Phone not included in role info endpoint
                KycStatus = kycStatus,
                Role = userRole,
                CreatedAt = DateTime.UtcNow // Not stored in Auth DB
            }
        };
    }

    public async Task<LoginResponseDto> RefreshTokenAsync(string refreshToken)
    {
        var userId = await ValidateRefreshTokenAsync(refreshToken);
        if (userId == null)
        {
            throw new UnauthorizedAccessException("Invalid refresh token");
        }

        var user = await _userManager.FindByIdAsync(userId.ToString()!);
        if (user == null)
        {
            throw new UnauthorizedAccessException("User not found");
        }

        // Revoke old token and generate new ones
        await RevokeTokenAsync(refreshToken);
        return await GenerateTokenAsync(user);
    }

    public async Task RevokeTokenAsync(string refreshToken)
    {
        if (_redisDatabase != null)
        {
            try
            {
                var key = $"{_keyPrefix}refresh_token:{refreshToken}";
                await _redisDatabase.KeyDeleteAsync(key);
                _logger.LogInformation("Refresh token revoked from Redis");
                // Also remove from in-memory cache if present
                _inMemoryRefreshTokens.Remove(refreshToken);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to revoke refresh token in Redis. Revoking from in-memory storage.");
                // Fall through to in-memory removal
            }
        }

        // Remove from in-memory storage
        _inMemoryRefreshTokens.Remove(refreshToken);
        _logger.LogInformation("Refresh token revoked from memory (Redis unavailable)");
    }

    public async Task<bool> ValidateTokenAsync(string token)
    {
        try
        {
            var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var key = System.Text.Encoding.UTF8.GetBytes(GetSecretKey());

            tokenHandler.ValidateToken(token, new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = GetIssuer(),
                ValidateAudience = true,
                ValidAudience = GetAudience(),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out Microsoft.IdentityModel.Tokens.SecurityToken validatedToken);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<List<System.Security.Claims.Claim>> BuildClaimsAsync(User user)
    {
        // First, try to get role from Auth DB (UserRoles table) - this is the source of truth for authentication
        Domain.Entities.UserRole userRole = Domain.Entities.UserRole.CoOwner;
        var identityRoles = await _userManager.GetRolesAsync(user);
        if (identityRoles != null && identityRoles.Count > 0)
        {
            // Get the first role (users typically have one primary role)
            var roleName = identityRoles.First();
            if (Enum.TryParse<Domain.Entities.UserRole>(roleName, out var parsedRole))
            {
                userRole = parsedRole;
                _logger.LogDebug("Using role from Auth DB for {UserId}: Role={Role}", user.Id, userRole);
            }
            else
            {
                _logger.LogWarning("Invalid role name '{RoleName}' in Auth DB for {UserId}, will try UserService", roleName, user.Id);
            }
        }
        else
        {
            _logger.LogWarning("No roles found in Auth DB for {UserId}, will try UserService", user.Id);
        }

        // Fetch user profile data from User service for JWT claims (for KYC status and name)
        UserRoleInfo? roleInfo = null;
        if (_userServiceClient != null)
        {
            try
            {
                roleInfo = await _userServiceClient.GetUserRoleInfoAsync(user.Id);
                if (roleInfo != null)
                {
                    _logger.LogDebug("Successfully fetched user role info for {UserId}: Role={Role}, KycStatus={KycStatus}", 
                        user.Id, roleInfo.Role, roleInfo.KycStatus);
                    
                    // Override role from Auth DB if UserService has a different role (Auth DB takes precedence)
                    // But if Auth DB had no role, use UserService role
                    if (identityRoles == null || identityRoles.Count == 0)
                    {
                        userRole = roleInfo.Role;
                        _logger.LogDebug("Using role from UserService for {UserId}: Role={Role} (no role in Auth DB)", user.Id, userRole);
                    }
                }
                else
                {
                    _logger.LogWarning("User role info not found for {UserId} in UserService", user.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch user role info for {UserId} from User service", user.Id);
            }
        }
        else
        {
            _logger.LogWarning("UserServiceClient not available for {UserId}", user.Id);
        }

        // Use fetched role info or defaults
        var kycStatus = roleInfo?.KycStatus ?? Domain.Entities.KycStatus.Pending;
        var firstName = roleInfo?.FirstName ?? string.Empty;
        var lastName = roleInfo?.LastName ?? string.Empty;

        var claims = new List<System.Security.Claims.Claim>
        {
            new(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(System.Security.Claims.ClaimTypes.Email, user.Email ?? string.Empty),
            new(System.Security.Claims.ClaimTypes.GivenName, firstName),
            new(System.Security.Claims.ClaimTypes.Surname, lastName),
            new("role", userRole.ToString()), // Role from Auth DB (UserRoles table) - source of truth
            new("kyc_status", kycStatus.ToString()), // KYC status from UserService
            new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Iat,
                new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds().ToString(),
                System.Security.Claims.ClaimValueTypes.Integer64)
        };

        // Add ASP.NET Identity roles (for [Authorize(Roles = "...")] attributes)
        // Use the roles already fetched above
        foreach (var role in identityRoles)
        {
            claims.Add(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, role));
        }

        return claims;
    }

    private string GenerateAccessToken(List<System.Security.Claims.Claim> claims)
    {
        var key = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
            System.Text.Encoding.UTF8.GetBytes(GetSecretKey()));
        var creds = new Microsoft.IdentityModel.Tokens.SigningCredentials(
            key, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);

        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: GetIssuer(),
            audience: GetAudience(),
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(GetTokenExpiryMinutes()),
            signingCredentials: creds);

        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
    }

    private string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    private async Task StoreRefreshTokenAsync(Guid userId, string refreshToken)
    {
        if (_redisDatabase != null)
        {
            try
            {
                var key = $"{_keyPrefix}refresh_token:{refreshToken}";
                var expiry = TimeSpan.FromDays(7); // 7 days expiry

                await _redisDatabase.StringSetAsync(key, userId.ToString(), expiry);
                _logger.LogInformation("Refresh token stored in Redis for user {UserId}", userId);
                return; // Successfully stored in Redis
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to store refresh token in Redis. Falling back to in-memory storage.");
                // Fall through to in-memory storage
            }
        }

        // Use in-memory storage as fallback
        _inMemoryRefreshTokens[refreshToken] = userId;
        _logger.LogInformation("Refresh token stored in memory for user {UserId} (Redis unavailable)", userId);
    }

    private async Task<Guid?> ValidateRefreshTokenAsync(string refreshToken)
    {
        if (_redisDatabase != null)
        {
            try
            {
                var key = $"{_keyPrefix}refresh_token:{refreshToken}";
                var userIdString = await _redisDatabase.StringGetAsync(key);

                if (userIdString.HasValue && Guid.TryParse(userIdString, out var userId))
                {
                    return userId;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to validate refresh token in Redis. Checking in-memory storage.");
                // Fall through to in-memory storage
            }
        }

        // Check in-memory storage as fallback
        if (_inMemoryRefreshTokens.TryGetValue(refreshToken, out var userIdFromMemory))
        {
            return userIdFromMemory;
        }

        return null; // Token not found or expired
    }

    private string GetSecretKey() => _configuration["JwtSettings:SecretKey"] ?? throw new InvalidOperationException("JWT secret key not configured");
    private string GetIssuer() => _configuration["JwtSettings:Issuer"] ?? throw new InvalidOperationException("JWT issuer not configured");
    private string GetAudience() => _configuration["JwtSettings:Audience"] ?? throw new InvalidOperationException("JWT audience not configured");
    private int GetTokenExpiryMinutes() => int.Parse(_configuration["JwtSettings:ExpiryMinutes"] ?? "60");
}
