namespace CoOwnershipVehicle.Group.Api.Services.Interfaces;

public interface ISigningTokenService
{
    /// <summary>
    /// Generates a secure signing token for a document signature
    /// </summary>
    string GenerateSigningToken(Guid documentId, Guid signerId, int expirationDays);

    /// <summary>
    /// Validates a signing token
    /// </summary>
    (bool IsValid, Guid DocumentId, Guid SignerId) ValidateSigningToken(string token);

    /// <summary>
    /// Generates a signing URL with the token
    /// </summary>
    string GenerateSigningUrl(string token, string baseUrl);
}
