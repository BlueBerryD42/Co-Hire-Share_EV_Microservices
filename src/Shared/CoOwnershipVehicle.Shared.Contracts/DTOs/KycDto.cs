using System.ComponentModel.DataAnnotations;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Shared.Contracts.DTOs;

public class KycDocumentDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public KycDocumentType DocumentType { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StorageUrl { get; set; } = string.Empty;
    public KycDocumentStatus Status { get; set; }
    public string? ReviewNotes { get; set; }
    public Guid? ReviewedBy { get; set; }
    public string? ReviewerName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime UploadedAt { get; set; }
}

public class UploadKycDocumentDto
{
    [Required]
    public KycDocumentType DocumentType { get; set; }
    
    [Required]
    public string FileName { get; set; } = string.Empty;
    
    [Required]
    public string Base64Content { get; set; } = string.Empty; // For demo - in production use file upload
    
    public string? Notes { get; set; }
}

public class ReviewKycDocumentDto
{
    [Required]
    public KycDocumentStatus Status { get; set; }
    
    public string? ReviewNotes { get; set; }
}

public class UserProfileDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public KycStatus KycStatus { get; set; }
    public UserRole Role { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<KycDocumentDto> KycDocuments { get; set; } = new();
}

public class UpdateUserProfileDto
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
    public DateTime? DateOfBirth { get; set; }
}

public class ChangePasswordDto
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;
    
    [Required]
    [MinLength(8)]
    public string NewPassword { get; set; } = string.Empty;
    
    [Required]
    [Compare(nameof(NewPassword))]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ChangePasswordWithEmailDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;
    
    [Required]
    [MinLength(8)]
    public string NewPassword { get; set; } = string.Empty;
    
    [Required]
    [Compare(nameof(NewPassword))]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ForgotPasswordDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordDto
{
    [Required]
    public string UserId { get; set; } = string.Empty;
    
    [Required]
    public string Token { get; set; } = string.Empty;
    
    [Required]
    [MinLength(8)]
    public string NewPassword { get; set; } = string.Empty;
    
    [Required]
    [Compare(nameof(NewPassword))]
    public string ConfirmPassword { get; set; } = string.Empty;
}


