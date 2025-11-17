using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Group.Api.Services.Interfaces;

public interface IUserServiceClient
{
    Task<UserInfoDto?> GetUserAsync(Guid userId, string accessToken);
    Task<Dictionary<Guid, UserInfoDto>> GetUsersAsync(List<Guid> userIds, string accessToken);
}

public class UserInfoDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public UserRole? Role { get; set; }
}

