using Microsoft.AspNetCore.Http;

namespace CoOwnershipVehicle.Booking.Api.Storage;

public record FileStorageResult(
    string FileUrl,
    string? ThumbnailUrl,
    string StoragePath,
    string? ThumbnailPath,
    string ContentType,
    DateTime? CapturedAt,
    double? Latitude,
    double? Longitude);

public interface IFileStorageService
{
    Task<FileStorageResult> SaveFileAsync(Stream stream, string fileName, string contentType, CancellationToken cancellationToken = default);
    Task<FileStorageResult> SaveImageAsync(Stream stream, string fileName, string contentType, CancellationToken cancellationToken = default);
    Task DeleteAsync(string storagePath, string? thumbnailPath, CancellationToken cancellationToken = default);
}
