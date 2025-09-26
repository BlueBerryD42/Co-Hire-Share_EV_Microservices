using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace CoOwnershipVehicle.Domain.Entities;

public class User : IdentityUser<Guid>
{
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
    
    public KycStatus KycStatus { get; set; } = KycStatus.Pending;
    
    public UserRole Role { get; set; } = UserRole.CoOwner;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual ICollection<GroupMember> GroupMemberships { get; set; } = new List<GroupMember>();
    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public virtual ICollection<Expense> ExpensesCreated { get; set; } = new List<Expense>();
    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public virtual ICollection<CheckIn> CheckIns { get; set; } = new List<CheckIn>();
    public virtual ICollection<Vote> Votes { get; set; } = new List<Vote>();
    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    public virtual ICollection<KycDocument> KycDocuments { get; set; } = new List<KycDocument>();
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
