using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Group.Api.DTOs;

public class DocumentUploadRequest
{
    [Required]
    public Guid GroupId { get; set; }

    [Required]
    public DocumentType DocumentType { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    public IFormFile File { get; set; } = null!;
}

public class DocumentUploadResponse
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public DocumentType Type { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public SignatureStatus SignatureStatus { get; set; }
    public string? Description { get; set; }
    public string SecureUrl { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
}

public class DocumentListResponse
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public DocumentType Type { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public SignatureStatus SignatureStatus { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public int SignatureCount { get; set; }
}

public class DocumentDetailResponse
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public DocumentType Type { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public SignatureStatus SignatureStatus { get; set; }
    public string? Description { get; set; }
    public string SecureUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? PageCount { get; set; }
    public string? Author { get; set; }
    public bool IsVirusScanned { get; set; }
    public List<DocumentSignatureResponse> Signatures { get; set; } = new();
}

public class DocumentSignatureResponse
{
    public Guid Id { get; set; }
    public Guid SignerId { get; set; }
    public string SignerName { get; set; } = string.Empty;
    public DateTime? SignedAt { get; set; }
    public int SignatureOrder { get; set; }
    public SignatureStatus Status { get; set; }
}

public class DocumentQueryParameters
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public DocumentType? DocumentType { get; set; }
    public SignatureStatus? SignatureStatus { get; set; }
    public string? SearchTerm { get; set; }
    public string? SortBy { get; set; } = "CreatedAt";
    public bool SortDescending { get; set; } = true;
}

public class PaginatedDocumentResponse
{
    public List<DocumentListItemResponse> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
}

public class DocumentListItemResponse
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public DocumentType Type { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public SignatureStatus SignatureStatus { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public int SignatureCount { get; set; }
    public int SignedCount { get; set; }
    public string UploaderName { get; set; } = string.Empty;
    public Guid UploaderId { get; set; }
    public int DownloadCount { get; set; }
}

public class DocumentDownloadResponse
{
    public Guid DocumentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public Stream FileStream { get; set; } = null!;
}

public class DownloadTrackingInfo
{
    public Guid DocumentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public int TotalDownloads { get; set; }
    public DateTime? LastDownloadedAt { get; set; }
    public string? LastDownloadedBy { get; set; }
    public List<DownloadHistoryItem> RecentDownloads { get; set; } = new();
}

public class DownloadHistoryItem
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public DateTime DownloadedAt { get; set; }
    public string IpAddress { get; set; } = string.Empty;
}

public class SendForSigningRequest
{
    [Required]
    [MinLength(1, ErrorMessage = "At least one signer is required")]
    public List<Guid> SignerIds { get; set; } = new();

    [Required]
    public SigningMode SigningMode { get; set; } = SigningMode.Parallel;

    public DateTime? DueDate { get; set; }

    [StringLength(1000)]
    public string? Message { get; set; }

    [Range(1, 365, ErrorMessage = "Token expiration must be between 1 and 365 days")]
    public int TokenExpirationDays { get; set; } = 7;
}

public class SendForSigningResponse
{
    public Guid DocumentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public SignatureStatus SignatureStatus { get; set; }
    public SigningMode SigningMode { get; set; }
    public DateTime? DueDate { get; set; }
    public int TotalSigners { get; set; }
    public List<SignerInfo> Signers { get; set; } = new();
    public DateTime SentAt { get; set; }
}

public class SignerInfo
{
    public Guid SignerId { get; set; }
    public string SignerName { get; set; } = string.Empty;
    public string SignerEmail { get; set; } = string.Empty;
    public int SignatureOrder { get; set; }
    public SignatureStatus Status { get; set; }
    public string SigningToken { get; set; } = string.Empty;
    public string SigningUrl { get; set; } = string.Empty;
    public DateTime TokenExpiresAt { get; set; }
    public bool NotificationSent { get; set; }
}

public class SignDocumentRequest
{
    [Required]
    [MinLength(1, ErrorMessage = "Signature data is required")]
    public string SignatureData { get; set; } = string.Empty;

    [Required]
    public string SigningToken { get; set; } = string.Empty;

    [StringLength(45)]
    public string? IpAddress { get; set; }

    [StringLength(500)]
    public string? DeviceInfo { get; set; }

    [StringLength(200)]
    public string? GpsCoordinates { get; set; }
}

public class SignDocumentResponse
{
    public Guid DocumentId { get; set; }
    public Guid SignatureId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public SignatureStatus DocumentStatus { get; set; }
    public DateTime SignedAt { get; set; }
    public string SignerName { get; set; } = string.Empty;
    public int TotalSigners { get; set; }
    public int SignedCount { get; set; }
    public double ProgressPercentage { get; set; }
    public string? NextSignerName { get; set; }
    public bool IsFullySigned { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class DocumentSignatureStatusResponse
{
    public Guid DocumentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public SignatureStatus Status { get; set; }
    public SigningMode SigningMode { get; set; }
    public int TotalSigners { get; set; }
    public int SignedCount { get; set; }
    public double ProgressPercentage { get; set; }
    public DateTime? DueDate { get; set; }
    public TimeSpan? TimeRemaining { get; set; }
    public string? NextSignerName { get; set; }
    public Guid? NextSignerId { get; set; }
    public List<SignatureDetailInfo> Signatures { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

public class SignatureDetailInfo
{
    public Guid Id { get; set; }
    public Guid SignerId { get; set; }
    public string SignerName { get; set; } = string.Empty;
    public string SignerEmail { get; set; } = string.Empty;
    public int SignatureOrder { get; set; }
    public SignatureStatus Status { get; set; }
    public DateTime? SignedAt { get; set; }
    public string? SignaturePreviewUrl { get; set; }
    public bool IsPending { get; set; }
    public bool IsCurrentSigner { get; set; }
}

public class SigningCertificateResponse
{
    public Guid DocumentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public byte[] CertificatePdf { get; set; } = Array.Empty<byte>();
    public string CertificateId { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string DocumentHash { get; set; } = string.Empty;
    public List<CertificateSignerInfo> Signers { get; set; } = new();
}

public class CertificateSignerInfo
{
    public string SignerName { get; set; } = string.Empty;
    public string SignerEmail { get; set; } = string.Empty;
    public DateTime SignedAt { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string DeviceInfo { get; set; } = string.Empty;
    public string SignatureImageUrl { get; set; } = string.Empty;
}

public class SignatureMetadata
{
    public string IpAddress { get; set; } = string.Empty;
    public string DeviceInfo { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public string? GpsCoordinates { get; set; }
    public DateTime SignedAt { get; set; }
}public class CertificateVerificationResult
{
    public string CertificateId { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public bool HashMatches { get; set; }
    public bool IsRevoked { get; set; }
    public bool IsExpired { get; set; }
    public string DocumentName { get; set; } = string.Empty;
    public Guid DocumentId { get; set; }
    public int TotalSigners { get; set; }
    public DateTime GeneratedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime VerifiedAt { get; set; }
    public string? RevocationReason { get; set; }
    public List<CertificateSignerInfo> Signers { get; set; } = new();
    public string VerificationUrl { get; set; } = string.Empty;
}
