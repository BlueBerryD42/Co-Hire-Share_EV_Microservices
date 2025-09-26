using System.ComponentModel.DataAnnotations;

namespace CoOwnershipVehicle.Shared.Contracts.DTOs;

public class UserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public KycStatus KycStatus { get; set; }
    public UserRole Role { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateUserDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    public string Password { get; set; } = string.Empty;
    
    [Required]
    public string FirstName { get; set; } = string.Empty;
    
    [Required]
    public string LastName { get; set; } = string.Empty;
    
    public string? Phone { get; set; }
}

public class LoginDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    public string Password { get; set; } = string.Empty;
}

public class LoginResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserDto User { get; set; } = null!;
}

public class RefreshTokenDto
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}

public enum KycStatus
{
    Pending = 0,
    InReview = 1,
    Approved = 2,
    Rejected = 3
}

public enum UserRole
{
    SystemAdmin = 0,
    Staff = 1,
    GroupAdmin = 2,
    CoOwner = 3
}
