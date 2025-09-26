using System.ComponentModel.DataAnnotations;

namespace CoOwnershipVehicle.Domain.Entities;

public class Document : BaseEntity
{
    public Guid GroupId { get; set; }
    
    public DocumentType Type { get; set; }
    
    [Required]
    [StringLength(500)]
    public string StorageKey { get; set; } = string.Empty;
    
    [Required]
    [StringLength(200)]
    public string FileName { get; set; } = string.Empty;
    
    public SignatureStatus SignatureStatus { get; set; } = SignatureStatus.Draft;
    
    [StringLength(1000)]
    public string? Description { get; set; }
    
    // Navigation properties
    public virtual OwnershipGroup Group { get; set; } = null!;
    public virtual ICollection<DocumentSignature> Signatures { get; set; } = new List<DocumentSignature>();
}

public class DocumentSignature : BaseEntity
{
    public Guid DocumentId { get; set; }
    
    public Guid SignerId { get; set; }
    
    public DateTime SignedAt { get; set; }
    
    [StringLength(500)]
    public string SignatureReference { get; set; } = string.Empty;
    
    // Navigation properties
    public virtual Document Document { get; set; } = null!;
    public virtual User Signer { get; set; } = null!;
}

public enum DocumentType
{
    OwnershipAgreement = 0,
    MaintenanceContract = 1,
    InsurancePolicy = 2,
    CheckInReport = 3,
    CheckOutReport = 4,
    Other = 5
}

public enum SignatureStatus
{
    Draft = 0,
    SentForSigning = 1,
    PartiallySigned = 2,
    FullySigned = 3,
    Expired = 4,
    Cancelled = 5
}
