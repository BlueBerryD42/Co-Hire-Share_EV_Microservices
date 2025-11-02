using Microsoft.AspNetCore.Http;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Booking.Api.DTOs;

public record PhotoUploadItem(IFormFile File, PhotoType Type, string? Description);

public record SignatureCaptureContext(string? IpAddress, string? UserAgent, DateTime CapturedAt);
