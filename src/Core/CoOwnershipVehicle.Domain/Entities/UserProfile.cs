using System.ComponentModel.DataAnnotations;

namespace CoOwnershipVehicle.Domain.Entities;

/// <summary>
/// UserProfile entity for User service database.
/// This is separate from User entity (used in Auth DB) to avoid confusion.
/// UserProfile stores profile data only, NO authentication data.
/// </summary>
public class UserProfile : BaseEntity
{
    
    [Required]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;
    
    [StringLength(256)]
    public string? NormalizedEmail { get; set; }
    
    [StringLength(256)]
    public string? UserName { get; set; }
    
    [StringLength(256)]
    public string? NormalizedUserName { get; set; }
    
    [Required]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;
    
    [StringLength(20)]
    public string? Phone { get; set; }
    
    [StringLength(500)]
    public string? Address { get; set; }
    
    [StringLength(100)]
    public string? City { get; set; }
    
    [StringLength(100)]
    public string? Country { get; set; }
    
    [StringLength(20)]
    public string? PostalCode { get; set; }
    
    public DateTime? DateOfBirth { get; set; }
    
    // Extended profile fields
    [StringLength(500)]
    public string? ProfilePhotoUrl { get; set; }
    
    [StringLength(200)]
    public string? Bio { get; set; }
    
    [StringLength(100)]
    public string? EmergencyContactName { get; set; }
    
    [StringLength(20)]
    public string? EmergencyContactPhone { get; set; }
    
    [StringLength(50)]
    public string? PreferredPaymentMethod { get; set; }
    
    [StringLength(2000)] // JSON string
    public string? NotificationPreferences { get; set; }
    
    [StringLength(10)]
    public string? LanguagePreference { get; set; }
    
    public KycStatus KycStatus { get; set; } = KycStatus.Pending;
    
    public UserRole Role { get; set; } = UserRole.CoOwner;
    
    // Identity fields for reference only (NOT used for authentication)
    // These are synced from Auth service but not used for authentication in User service
    public bool EmailConfirmed { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public bool LockoutEnabled { get; set; }
    public int AccessFailedCount { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
    public string? ConcurrencyStamp { get; set; }
    // Note: PhoneNumber and PhoneNumberConfirmed removed - use Phone field instead
    
    // Navigation properties
    public virtual ICollection<KycDocument> KycDocuments { get; set; } = new List<KycDocument>();
}

