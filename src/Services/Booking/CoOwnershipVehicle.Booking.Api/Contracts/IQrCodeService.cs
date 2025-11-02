using CoOwnershipVehicle.Booking.Api.DTOs;
using CoOwnershipVehicle.Shared.Contracts.DTOs;

namespace CoOwnershipVehicle.Booking.Api.Contracts;

public interface IQrCodeService
{
    Task<VehicleQrCodeResult> GetVehicleQrCodeAsync(Guid vehicleId, Guid userId, CancellationToken cancellationToken = default);
    Task<QrCodeValidationResponseDto> ValidateAsync(QrCodeValidationRequestDto request, Guid userId, CancellationToken cancellationToken = default);
}
