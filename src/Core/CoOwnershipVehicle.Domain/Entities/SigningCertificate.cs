using System.ComponentModel.DataAnnotations;

namespace CoOwnershipVehicle.Domain.Entities;

/// <summary>
/// Represents a signing certificate generated for a fully signed document
/// </summary>
public class SigningCertificate
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid DocumentId { get; set; }

    [Required]
    [StringLength(100)]
    public string CertificateId { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string DocumentHash { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    public int TotalSigners { get; set; }

    public DateTime GeneratedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// JSON array of signer information
    /// </summary>
    [StringLength(4000)]
    public string? SignersJson { get; set; }

    public bool IsRevoked { get; set; }

    public DateTime? RevokedAt { get; set; }

    [StringLength(500)]
    public string? RevocationReason { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    // Navigation properties
    public virtual Document? Document { get; set; }
}
