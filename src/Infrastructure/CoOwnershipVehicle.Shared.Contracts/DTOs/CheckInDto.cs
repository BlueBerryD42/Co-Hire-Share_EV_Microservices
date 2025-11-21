using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Shared.Contracts.DTOs;

public class CheckInDto
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public Guid UserId { get; set; }
    public Guid? VehicleId { get; set; }
    public string UserFirstName { get; set; } = string.Empty;
    public string UserLastName { get; set; } = string.Empty;
    public CheckInType Type { get; set; }
    public int Odometer { get; set; }
    public DateTime CheckInTime { get; set; }
    public bool IsLateReturn { get; set; }
    public double? LateReturnMinutes { get; set; }
    public decimal? LateFeeAmount { get; set; }
    public LateReturnFeeDto? LateReturnFee { get; set; }
    public string? Notes { get; set; }
    public string? SignatureReference { get; set; }
    public SignatureMetadataDto? SignatureMetadata { get; set; }
    public List<CheckInPhotoDto> Photos { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CheckInPhotoDto
{
    public Guid Id { get; set; }
    public Guid CheckInId { get; set; }
    public string PhotoUrl { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public PhotoType Type { get; set; }
    public string? Description { get; set; }
    public string? ContentType { get; set; }
    public DateTime? CapturedAt { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool IsDeleted { get; set; }
}

public class CheckInPhotoInputDto
{
    [Required]
    [MaxLength(10485760)] // allow up to ~10MB base64 payloads instead of the previous 4000 char cap
    public string PhotoUrl { get; set; } = string.Empty;

    [Required]
    public PhotoType Type { get; set; }

    [StringLength(200)]
    public string? Description { get; set; }
}

public class CreateCheckInDto
{
    [Required]
    public Guid BookingId { get; set; }

    public Guid? UserId { get; set; }

    [Required]
    public CheckInType Type { get; set; }

    [Range(0, int.MaxValue)]
    public int Odometer { get; set; }

    public DateTime? CheckInTime { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    [StringLength(500)]
    public string? SignatureReference { get; set; }

    public List<CheckInPhotoInputDto> Photos { get; set; } = new();
}

public class UpdateCheckInDto
{
    [Range(0, int.MaxValue)]
    public int Odometer { get; set; }

    public DateTime? CheckInTime { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    [StringLength(500)]
    public string? SignatureReference { get; set; }

    public CheckInType Type { get; set; }

    public List<CheckInPhotoInputDto> Photos { get; set; } = new();
}

public class StartTripDto
{
    [Required]
    public Guid BookingId { get; set; }

    [Range(0, int.MaxValue)]
    public int OdometerReading { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    [StringLength(500)]
    public string? SignatureReference { get; set; }

    public DateTime? ClientTimestamp { get; set; }

    public List<CheckInPhotoInputDto> Photos { get; set; } = new();
}

public class EndTripDto
{
    [Required]
    public Guid BookingId { get; set; }

    [Range(0, int.MaxValue)]
    public int OdometerReading { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    [StringLength(500)]
    public string? SignatureReference { get; set; }

    public DateTime? ClientTimestamp { get; set; }

    public List<CheckInPhotoInputDto> Photos { get; set; } = new();
}

public class TripCompletionDto
{
    public CheckInDto CheckIn { get; set; } = new();
    public int TripDistance { get; set; }
    public double TripDurationMinutes { get; set; }
    public bool IsLateReturn { get; set; }
    public double LateByMinutes { get; set; }
    public LateReturnFeeDto? LateFee { get; set; }
    public DateTime CheckOutTime { get; set; }
}

public class SignatureCaptureRequestDto
{
    [Required]
    public string SignatureData { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Format { get; set; }

    [StringLength(200)]
    public string? DeviceInfo { get; set; }

    [StringLength(100)]
    public string? DeviceId { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public bool OverwriteExisting { get; set; } = true;
}

public class SignatureMetadataDto
{
    public DateTime? CapturedAt { get; set; }
    public string? Device { get; set; }
    public string? DeviceId { get; set; }
    public string? IpAddress { get; set; }
    public string? Hash { get; set; }
    public bool? MatchesPrevious { get; set; }
    public string? CertificateUrl { get; set; }
    public Dictionary<string, string>? AdditionalMetadata { get; set; }
}

public class SignatureCaptureResponseDto
{
    public Guid CheckInId { get; set; }
    public Guid BookingId { get; set; }
    public Guid UserId { get; set; }
    public string SignatureUrl { get; set; } = string.Empty;
    public SignatureMetadataDto Metadata { get; set; } = new();
    public string? CertificateUrl { get; set; }
    public bool OverwroteExisting { get; set; }
}
