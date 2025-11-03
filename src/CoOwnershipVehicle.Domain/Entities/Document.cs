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

    public long FileSize { get; set; }

    [StringLength(100)]
    public string ContentType { get; set; } = string.Empty;

    [StringLength(64)]
    public string? FileHash { get; set; }

    public int? PageCount { get; set; }

    [StringLength(200)]
    public string? Author { get; set; }

    public bool IsVirusScanned { get; set; }

    public bool? VirusScanPassed { get; set; }

    public Guid? UploadedBy { get; set; }

    // Navigation properties
    public virtual OwnershipGroup Group { get; set; } = null!;
    public virtual ICollection<DocumentSignature> Signatures { get; set; } = new List<DocumentSignature>();
    public virtual ICollection<DocumentDownload> Downloads { get; set; } = new List<DocumentDownload>();
}

public class DocumentSignature : BaseEntity
{
    public Guid DocumentId { get; set; }

    public Guid SignerId { get; set; }

    public DateTime? SignedAt { get; set; }

    [StringLength(500)]
    public string? SignatureReference { get; set; }

    public int SignatureOrder { get; set; }

    [StringLength(2000)]
    public string? SignatureMetadata { get; set; }

    public SignatureStatus Status { get; set; } = SignatureStatus.Draft;

    [StringLength(500)]
    public string? SigningToken { get; set; }

    public DateTime? TokenExpiresAt { get; set; }

    public DateTime? DueDate { get; set; }

    [StringLength(1000)]
    public string? Message { get; set; }

    public SigningMode SigningMode { get; set; } = SigningMode.Parallel;

    public bool IsNotificationSent { get; set; } = false;

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

public enum SigningMode
{
    Parallel = 0,
    Sequential = 1
}

public class DocumentDownload : BaseEntity
{
    public Guid DocumentId { get; set; }

    public Guid UserId { get; set; }

    [StringLength(45)]
    public string IpAddress { get; set; } = string.Empty;

    [StringLength(500)]
    public string? UserAgent { get; set; }

    public DateTime DownloadedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Document Document { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}
