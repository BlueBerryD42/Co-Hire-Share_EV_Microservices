namespace CoOwnershipVehicle.Booking.Api.DTOs;

public record VehicleQrCodeResult(Guid VehicleId, byte[] ImageBytes, string DataUrl, string Payload, DateTime ExpiresAt);
