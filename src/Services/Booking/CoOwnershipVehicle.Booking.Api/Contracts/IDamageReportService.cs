using CoOwnershipVehicle.Shared.Contracts.DTOs;

namespace CoOwnershipVehicle.Booking.Api.Contracts;

public interface IDamageReportService
{
    Task<DamageReportDto> ReportDamageAsync(Guid checkInId, Guid userId, CreateDamageReportDto request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DamageReportDto>> GetByCheckInAsync(Guid checkInId, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DamageReportDto>> GetByBookingAsync(Guid bookingId, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DamageReportDto>> GetByVehicleAsync(Guid vehicleId, Guid userId, CancellationToken cancellationToken = default);
    Task<DamageReportDto> UpdateStatusAsync(Guid reportId, Guid userId, UpdateDamageReportStatusDto request, CancellationToken cancellationToken = default);
}
