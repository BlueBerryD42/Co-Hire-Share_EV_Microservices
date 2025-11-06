using System.ComponentModel.DataAnnotations;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Shared.Contracts.DTOs;

public class VehicleQrCodeResponseDto
{
    public Guid VehicleId { get; set; }
    public string Format { get; set; } = "dataUrl";
    public string Payload { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

public class QrCodeValidationRequestDto
{
    [Required]
    public string QrCodeData { get; set; } = string.Empty;

    [Required]
    public string Action { get; set; } = string.Empty;

    public string? DeviceInfo { get; set; }

    public string? DeviceId { get; set; }

    public string? Locale { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }
}

public class QrCodeValidationResponseDto
{
    public bool IsValid { get; set; }
    public string Message { get; set; } = string.Empty;
    public BookingDto? Booking { get; set; }
    public string Action { get; set; } = string.Empty;
    public DateTime ValidatedAt { get; set; }
    public DateTime? CheckoutWindowOpensAt { get; set; }
    public DateTime? CheckoutWindowClosesAt { get; set; }
    public DateTime? CheckinWindowClosesAt { get; set; }
}
