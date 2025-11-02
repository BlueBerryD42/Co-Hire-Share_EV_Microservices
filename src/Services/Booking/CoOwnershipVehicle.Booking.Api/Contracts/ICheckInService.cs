using CoOwnershipVehicle.Booking.Api.DTOs;
using CoOwnershipVehicle.Shared.Contracts.DTOs;

namespace CoOwnershipVehicle.Booking.Api.Contracts;

public interface ICheckInService
{
    Task<CheckInDto> CreateAsync(CreateCheckInDto request, Guid userId, CancellationToken cancellationToken = default);
    Task<CheckInDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CheckInDto>> GetByBookingAsync(Guid bookingId, CancellationToken cancellationToken = default);
    Task<CheckInDto> UpdateAsync(Guid id, UpdateCheckInDto request, Guid userId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<CheckInDto> StartTripAsync(StartTripDto request, Guid userId, CancellationToken cancellationToken = default);
    Task<TripCompletionDto> EndTripAsync(EndTripDto request, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CheckInPhotoDto>> UploadPhotosAsync(Guid checkInId, Guid userId, IEnumerable<PhotoUploadItem> uploads, CancellationToken cancellationToken = default);
    Task DeletePhotoAsync(Guid checkInId, Guid photoId, Guid userId, CancellationToken cancellationToken = default);
    Task<SignatureCaptureResponseDto> CaptureSignatureAsync(Guid checkInId, Guid userId, SignatureCaptureRequestDto request, SignatureCaptureContext context, CancellationToken cancellationToken = default);
    Task<BookingCheckInHistoryDto> GetBookingHistoryAsync(Guid bookingId, Guid userId, CancellationToken cancellationToken = default);
    Task<CheckInComparisonDto> GetComparisonAsync(Guid checkInId, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CheckInRecordDetailDto>> FilterHistoryAsync(CheckInHistoryFilterDto filter, Guid userId, CancellationToken cancellationToken = default);
    Task<byte[]> ExportBookingHistoryPdfAsync(Guid bookingId, Guid userId, CancellationToken cancellationToken = default);
}
