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
    
    // Fallback in-memory storage when Redis is not available
    private static readonly Dictionary<string, Guid> _inMemoryRefreshTokens = new();

    public JwtTokenService(
        IConfiguration configuration,
        UserManager<User> userManager,
        ILogger<JwtTokenService> logger,
        IDatabase? redisDatabase)
    {
        _configuration = configuration;
        _userManager = userManager;
        _logger = logger;
        _redisDatabase = redisDatabase;
        _keyPrefix = CoOwnershipVehicle.Shared.Configuration.EnvironmentHelper.GetRedisConfigParams(configuration).KeyPrefix;
        
        if (_redisDatabase == null)
        {
            _logger.LogWarning("Redis database is not available. Refresh tokens will use in-memory storage.");
        }
    }

    public async Task<LoginResponseDto> GenerateTokenAsync(User user)
    {
        var claims = await BuildClaimsAsync(user);
        var accessToken = GenerateAccessToken(claims);
        var refreshToken = GenerateRefreshToken();

        // Store refresh token (in production, use a secure store like Redis)
        await StoreRefreshTokenAsync(user.Id, refreshToken);

        return new LoginResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(GetTokenExpiryMinutes()),
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email!,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Phone = user.Phone,
                KycStatus = user.KycStatus,
                Role = user.Role,
                CreatedAt = user.CreatedAt
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
            var key = $"{_keyPrefix}refresh_token:{refreshToken}";
            await _redisDatabase.KeyDeleteAsync(key);
            _logger.LogInformation("Refresh token revoked from Redis");
        }
        else
        {
            _inMemoryRefreshTokens.Remove(refreshToken);
            _logger.LogInformation("Refresh token revoked from memory (Redis unavailable)");
        }
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
        var claims = new List<System.Security.Claims.Claim>
        {
            new(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(System.Security.Claims.ClaimTypes.Email, user.Email!),
            new(System.Security.Claims.ClaimTypes.GivenName, user.FirstName),
            new(System.Security.Claims.ClaimTypes.Surname, user.LastName),
            new("role", user.Role.ToString()),
            new("kyc_status", user.KycStatus.ToString()),
            new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Iat, 
                new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds().ToString(), 
                System.Security.Claims.ClaimValueTypes.Integer64)
        };

        var roles = await _userManager.GetRolesAsync(user);
        foreach (var role in roles)
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
            var key = $"{_keyPrefix}refresh_token:{refreshToken}";
            var expiry = TimeSpan.FromDays(7); // 7 days expiry
            
            await _redisDatabase.StringSetAsync(key, userId.ToString(), expiry);
            _logger.LogInformation("Refresh token stored in Redis for user {UserId}", userId);
        }
        else
        {
            _inMemoryRefreshTokens[refreshToken] = userId;
            _logger.LogInformation("Refresh token stored in memory for user {UserId} (Redis unavailable)", userId);
        }
    }

    private async Task<Guid?> ValidateRefreshTokenAsync(string refreshToken)
    {
        if (_redisDatabase != null)
        {
            var key = $"{_keyPrefix}refresh_token:{refreshToken}";
            var userIdString = await _redisDatabase.StringGetAsync(key);
            
            if (userIdString.HasValue && Guid.TryParse(userIdString, out var userId))
            {
                return userId;
            }
        }
        else
        {
            if (_inMemoryRefreshTokens.TryGetValue(refreshToken, out var userId))
            {
                return userId;
            }
        }
        
        return null; // Token not found or expired
    }

    private string GetSecretKey() => _configuration["JwtSettings:SecretKey"] ?? throw new InvalidOperationException("JWT secret key not configured");
    private string GetIssuer() => _configuration["JwtSettings:Issuer"] ?? throw new InvalidOperationException("JWT issuer not configured");
    private string GetAudience() => _configuration["JwtSettings:Audience"] ?? throw new InvalidOperationException("JWT audience not configured");
    private int GetTokenExpiryMinutes() => int.Parse(_configuration["JwtSettings:ExpiryMinutes"] ?? "60");
}
