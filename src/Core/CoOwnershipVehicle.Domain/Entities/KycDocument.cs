using System.ComponentModel.DataAnnotations;

namespace CoOwnershipVehicle.Domain.Entities;

public class KycDocument : BaseEntity
{
    public Guid UserId { get; set; }
    
    public KycDocumentType DocumentType { get; set; }
    
    [Required]
    [StringLength(200)]
    public string FileName { get; set; } = string.Empty;
    
    [Required]
    [StringLength(500)]
    public string StorageUrl { get; set; } = string.Empty;
    
    public KycDocumentStatus Status { get; set; } = KycDocumentStatus.Pending;
    
    [StringLength(1000)]
    public string? ReviewNotes { get; set; }
    
    public Guid? ReviewedBy { get; set; }
    
    public DateTime? ReviewedAt { get; set; }
    
    // Navigation properties
    public virtual User User { get; set; } = null!;
    public virtual User? Reviewer { get; set; }
}

public enum KycDocumentType
{
    NationalId = 0,
    Passport = 1,
    DriverLicense = 2,
    ProofOfAddress = 3,
    BankStatement = 4,
    Other = 5
}

public enum KycDocumentStatus
{
    Pending = 0,
    UnderReview = 1,
    Approved = 2,
    Rejected = 3,
    RequiresUpdate = 4
}
