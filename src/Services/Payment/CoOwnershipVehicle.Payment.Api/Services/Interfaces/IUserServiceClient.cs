namespace CoOwnershipVehicle.Payment.Api.Services.Interfaces;

/// <summary>
/// HTTP client interface for communicating with User Service
/// Used to fetch user information since Payment service doesn't store User entities
/// </summary>
public interface IUserServiceClient
{
    /// <summary>
    /// Get a single user by ID
    /// </summary>
    Task<UserInfoDto?> GetUserAsync(Guid userId, string accessToken);

    /// <summary>
    /// Get multiple users by their IDs
    /// </summary>
    Task<Dictionary<Guid, UserInfoDto>> GetUsersAsync(List<Guid> userIds, string accessToken);
}

/// <summary>
/// DTO for user information returned from User Service
/// </summary>
public class UserInfoDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Phone { get; set; }
}


