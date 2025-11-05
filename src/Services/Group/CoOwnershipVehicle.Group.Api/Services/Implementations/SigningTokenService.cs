using CoOwnershipVehicle.Group.Api.Services.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace CoOwnershipVehicle.Group.Api.Services.Implementations;

public class SigningTokenService : ISigningTokenService
{
    private readonly ILogger<SigningTokenService> _logger;
    private const string TokenPrefix = "SIGN";

    public SigningTokenService(ILogger<SigningTokenService> logger)
    {
        _logger = logger;
    }

    public string GenerateSigningToken(Guid documentId, Guid signerId, int expirationDays)
    {
        try
        {
            // Create a unique token combining document ID, signer ID, and timestamp
            var expiresAt = DateTime.UtcNow.AddDays(expirationDays);
            var expirationTimestamp = new DateTimeOffset(expiresAt).ToUnixTimeSeconds();

            // Combine data: prefix|documentId|signerId|expirationTimestamp|randomBytes
            var randomBytes = GenerateRandomBytes(16);
            var tokenData = $"{TokenPrefix}|{documentId:N}|{signerId:N}|{expirationTimestamp}|{randomBytes}";

            // Encode to Base64URL (URL-safe)
            var tokenBytes = Encoding.UTF8.GetBytes(tokenData);
            var base64Token = Convert.ToBase64String(tokenBytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');

            _logger.LogInformation("Generated signing token for document {DocumentId}, signer {SignerId}",
                documentId, signerId);

            return base64Token;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating signing token");
            throw;
        }
    }

    public (bool IsValid, Guid DocumentId, Guid SignerId) ValidateSigningToken(string token)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return (false, Guid.Empty, Guid.Empty);
            }

            // Convert from Base64URL back to standard Base64
            var base64 = token
                .Replace('-', '+')
                .Replace('_', '/');

            // Add padding if needed
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }

            // Decode
            var tokenBytes = Convert.FromBase64String(base64);
            var tokenData = Encoding.UTF8.GetString(tokenBytes);

            // Parse: prefix|documentId|signerId|expirationTimestamp|randomBytes
            var parts = tokenData.Split('|');
            if (parts.Length != 5 || parts[0] != TokenPrefix)
            {
                _logger.LogWarning("Invalid token format");
                return (false, Guid.Empty, Guid.Empty);
            }

            // Extract data (GUIDs are stored in N format without hyphens)
            if (!Guid.TryParseExact(parts[1], "N", out var documentId))
            {
                _logger.LogWarning("Invalid document ID in token");
                return (false, Guid.Empty, Guid.Empty);
            }

            if (!Guid.TryParseExact(parts[2], "N", out var signerId))
            {
                _logger.LogWarning("Invalid signer ID in token");
                return (false, Guid.Empty, Guid.Empty);
            }

            if (!long.TryParse(parts[3], out var expirationTimestamp))
            {
                _logger.LogWarning("Invalid expiration timestamp in token");
                return (false, Guid.Empty, Guid.Empty);
            }

            // Check if expired
            var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expirationTimestamp).UtcDateTime;
            if (DateTime.UtcNow > expiresAt)
            {
                _logger.LogWarning("Token expired for document {DocumentId}, signer {SignerId}",
                    documentId, signerId);
                return (false, documentId, signerId);
            }

            _logger.LogInformation("Token validated successfully for document {DocumentId}, signer {SignerId}",
                documentId, signerId);

            return (true, documentId, signerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating signing token");
            return (false, Guid.Empty, Guid.Empty);
        }
    }

    public string GenerateSigningUrl(string token, string baseUrl)
    {
        // Remove trailing slash from baseUrl
        baseUrl = baseUrl.TrimEnd('/');

        // Generate the signing URL
        var signingUrl = $"{baseUrl}/sign/{token}";

        return signingUrl;
    }

    private string GenerateRandomBytes(int length)
    {
        var randomBytes = new byte[length];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(randomBytes);
        }
        return Convert.ToBase64String(randomBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
