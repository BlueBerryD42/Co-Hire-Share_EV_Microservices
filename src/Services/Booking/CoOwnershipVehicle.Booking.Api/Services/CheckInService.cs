using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CoOwnershipVehicle.Booking.Api.Data;
using CoOwnershipVehicle.Booking.Api.Storage;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Shared.Contracts.Events;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace CoOwnershipVehicle.Booking.Api.Services;

public record PhotoUploadItem(IFormFile File, PhotoType Type, string? Description);
public record SignatureCaptureContext(string? IpAddress, string? UserAgent, DateTime CapturedAt);

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
}

public class CheckInService : ICheckInService
{
    private readonly BookingDbContext _context;
    private readonly ILogger<CheckInService> _logger;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IFileStorageService _fileStorageService;
    private readonly IVirusScanner _virusScanner;
    private const int MaxTripOdometerDelta = 2000;
    private const int MaxPhotoCount = 10;
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/heic",
        "image/heif",
        "image/jpg",
        "image/pjpeg"
    };
    private static readonly HashSet<string> AllowedDocumentContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/msword", // .doc
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document" // .docx
    };
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".heic",
        ".heif"
    };
    private static readonly HashSet<string> AllowedDocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx"
    };
    private const int MaxSignatureBytes = 2 * 1024 * 1024;
    private const int MaxSignaturePathLength = 200_000;
    private static readonly Regex DataUrlRegex = new(@"^data:(?<mime>[\w+\-\.\/]+);base64,(?<data>[A-Za-z0-9+/=\s]+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SvgPathRegex = new(@"^[MmLlHhVvCcSsQqTtAaZz0-9\.\s,\-]+$", RegexOptions.Compiled);
    private static readonly Regex MultipleWhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    public CheckInService(
        BookingDbContext context,
        ILogger<CheckInService> logger,
        IPublishEndpoint publishEndpoint,
        IFileStorageService fileStorageService,
        IVirusScanner virusScanner)
    {
        _context = context;
        _logger = logger;
        _publishEndpoint = publishEndpoint;
        _fileStorageService = fileStorageService;
        _virusScanner = virusScanner;
    }

    public async Task<CheckInDto> StartTripAsync(StartTripDto request, Guid userId, CancellationToken cancellationToken = default)
    {
        var booking = await _context.Bookings
            .Include(b => b.Vehicle)
            .Include(b => b.Group)
                .ThenInclude(g => g.Members)
            .Include(b => b.User)
            .Include(b => b.CheckIns).ThenInclude(ci => ci.Photos)
            .FirstOrDefaultAsync(b => b.Id == request.BookingId, cancellationToken);

        if (booking == null)
        {
            throw new KeyNotFoundException("Booking not found");
        }

        if (booking.UserId != userId)
        {
            throw new UnauthorizedAccessException("You do not own this booking");
        }

        if (booking.Status == BookingStatus.Cancelled)
        {
            throw new InvalidOperationException("Cannot start trip for a cancelled booking");
        }

        if (booking.Status == BookingStatus.Completed)
        {
            throw new InvalidOperationException("Trip already completed for this booking");
        }

        if (booking.Status == BookingStatus.InProgress)
        {
            throw new InvalidOperationException("Trip already in progress for this booking");
        }

        if (booking.Status != BookingStatus.Confirmed)
        {
            throw new InvalidOperationException("Booking must be confirmed before starting the trip");
        }

        var now = DateTime.UtcNow;
        var earliest = booking.StartAt.AddMinutes(-30);
        var latest = booking.StartAt.AddMinutes(30);

        if (now < earliest)
        {
            throw new ArgumentException("Check-out is only allowed within 30 minutes prior to the booking start time");
        }

        if (now > latest)
        {
            throw new InvalidOperationException("The check-out window for this booking has expired");
        }

        if (booking.CheckIns.Any(ci => ci.Type == CheckInType.CheckOut))
        {
            throw new InvalidOperationException("This booking already has a check-out recorded");
        }

        var vehicle = booking.Vehicle ?? throw new InvalidOperationException("Vehicle information is missing for this booking");

        var checkIn = new CheckIn
        {
            BookingId = booking.Id,
            VehicleId = booking.VehicleId,
            UserId = userId,
            Type = CheckInType.CheckOut,
            Odometer = request.OdometerReading,
            Notes = request.Notes,
            SignatureReference = request.SignatureReference,
            CheckInTime = now
        };

        var photoInputs = request.Photos ?? new List<CheckInPhotoInputDto>();

        if (photoInputs.Any())
        {
            checkIn.Photos = photoInputs
                .Select(photo => new CheckInPhoto
                {
                    CheckInId = checkIn.Id,
                    PhotoUrl = photo.PhotoUrl,
                    ThumbnailUrl = null,
                    StoragePath = null,
                    ThumbnailPath = null,
                    ContentType = null,
                    Type = photo.Type,
                    Description = photo.Description,
                    CapturedAt = null,
                    Latitude = null,
                    Longitude = null,
                    IsDeleted = false
                })
                .ToList();
        }

        booking.CheckIns.Add(checkIn);
        booking.Status = BookingStatus.InProgress;

        var previousVehicleStatus = vehicle.Status;
        vehicle.Status = VehicleStatus.InUse;
        if (request.OdometerReading > vehicle.Odometer)
        {
            vehicle.Odometer = request.OdometerReading;
        }

        _context.CheckIns.Add(checkIn);

        await _context.SaveChangesAsync(cancellationToken);

        var photoUrls = checkIn.Photos.Where(p => !p.IsDeleted).Select(p => p.PhotoUrl).ToList();

        await _publishEndpoint.Publish(new TripStartedEvent
        {
            BookingId = booking.Id,
            VehicleId = booking.VehicleId,
            GroupId = booking.GroupId,
            UserId = userId,
            CheckInId = checkIn.Id,
            CheckOutTime = checkIn.CheckInTime,
            Odometer = checkIn.Odometer,
            Notes = checkIn.Notes,
            SignatureReference = checkIn.SignatureReference,
            PhotoUrls = photoUrls
        }, cancellationToken);

        await _publishEndpoint.Publish(new VehicleCheckedOutEvent
        {
            BookingId = booking.Id,
            VehicleId = booking.VehicleId,
            UserId = userId,
            Odometer = checkIn.Odometer,
            CheckOutTime = checkIn.CheckInTime,
            SignatureReference = checkIn.SignatureReference
        }, cancellationToken);

        if (previousVehicleStatus != vehicle.Status)
        {
            await _publishEndpoint.Publish(new VehicleStatusChangedEvent
            {
                VehicleId = vehicle.Id,
                GroupId = booking.GroupId,
                OldStatus = previousVehicleStatus,
                NewStatus = vehicle.Status,
                ChangedBy = userId,
                ChangedAt = now,
                Reason = "Vehicle checked out"
            }, cancellationToken);
        }

        if (booking.Group?.Members?.Any() == true)
        {
            var notificationUserIds = booking.Group.Members
                .Select(m => m.UserId)
                .Where(id => id != userId)
                .Distinct()
                .ToList();

            if (notificationUserIds.Any())
            {
                await _publishEndpoint.Publish(new BulkNotificationEvent
                {
                    UserIds = notificationUserIds,
                    GroupId = booking.GroupId,
                    Title = $"Trip started for {vehicle.Model}",
                    Message = $"{booking.User.FirstName} {booking.User.LastName} started the trip with vehicle {vehicle.PlateNumber} at {checkIn.CheckInTime:HH:mm}.",
                    Type = "TripStarted",
                    Priority = "Normal",
                    ActionUrl = $"/bookings/{booking.Id}",
                    ActionText = "View booking"
                }, cancellationToken);
            }
        }

        _logger.LogInformation("Trip started for booking {BookingId} with vehicle {VehicleId}", booking.Id, vehicle.Id);

        return await GetByIdRequiredAsync(checkIn.Id, cancellationToken);
    }

    public async Task<TripCompletionDto> EndTripAsync(EndTripDto request, Guid userId, CancellationToken cancellationToken = default)
    {
        var booking = await _context.Bookings
            .Include(b => b.Vehicle)
            .Include(b => b.Group)
                .ThenInclude(g => g.Members)
            .Include(b => b.User)
            .Include(b => b.CheckIns).ThenInclude(ci => ci.Photos)
            .FirstOrDefaultAsync(b => b.Id == request.BookingId, cancellationToken);

        if (booking == null)
        {
            throw new KeyNotFoundException("Booking not found");
        }

        if (booking.UserId != userId)
        {
            throw new UnauthorizedAccessException("You do not own this booking");
        }

        var checkOut = booking.CheckIns
            .Where(ci => ci.Type == CheckInType.CheckOut)
            .OrderByDescending(ci => ci.CheckInTime)
            .FirstOrDefault();

        if (checkOut == null)
        {
            throw new InvalidOperationException("Trip has not been checked out yet");
        }

        if (booking.CheckIns.Any(ci => ci.Type == CheckInType.CheckIn))
        {
            throw new InvalidOperationException("Trip has already been checked in");
        }

        if (booking.Status != BookingStatus.InProgress)
        {
            throw new InvalidOperationException("Trip is not currently in progress");
        }

        if (request.OdometerReading < checkOut.Odometer)
        {
            throw new InvalidOperationException("Odometer reading cannot be less than the check-out reading");
        }

        var distance = request.OdometerReading - checkOut.Odometer;
        if (distance > MaxTripOdometerDelta)
        {
            throw new InvalidOperationException($"Reported distance of {distance} exceeds allowed limit of {MaxTripOdometerDelta}");
        }

        var now = DateTime.UtcNow;

        var checkIn = new CheckIn
        {
            BookingId = booking.Id,
            VehicleId = booking.VehicleId,
            UserId = userId,
            Type = CheckInType.CheckIn,
            Odometer = request.OdometerReading,
            Notes = request.Notes,
            SignatureReference = request.SignatureReference,
            CheckInTime = now
        };

        var photoInputs = request.Photos ?? new List<CheckInPhotoInputDto>();
        if (photoInputs.Any())
        {
            checkIn.Photos = photoInputs
                .Select(photo => new CheckInPhoto
                {
                    CheckInId = checkIn.Id,
                    PhotoUrl = photo.PhotoUrl,
                    ThumbnailUrl = null,
                    StoragePath = null,
                    ThumbnailPath = null,
                    ContentType = null,
                    Type = photo.Type,
                    Description = photo.Description,
                    CapturedAt = null,
                    Latitude = null,
                    Longitude = null,
                    IsDeleted = false
                })
                .ToList();
        }

        booking.CheckIns.Add(checkIn);
        booking.Status = BookingStatus.Completed;

        var vehicle = booking.Vehicle ?? throw new InvalidOperationException("Vehicle information is missing for this booking");
        var previousVehicleStatus = vehicle.Status;
        vehicle.Status = VehicleStatus.Available;
        if (request.OdometerReading > vehicle.Odometer)
        {
            vehicle.Odometer = request.OdometerReading;
        }

        _context.CheckIns.Add(checkIn);

        await _context.SaveChangesAsync(cancellationToken);

        var duration = checkIn.CheckInTime - checkOut.CheckInTime;
        if (duration < TimeSpan.Zero)
        {
            duration = TimeSpan.Zero;
        }

        var isLateReturn = checkIn.CheckInTime > booking.EndAt;
        var lateByMinutes = isLateReturn ? (checkIn.CheckInTime - booking.EndAt).TotalMinutes : 0d;
        var roundedDurationMinutes = Math.Round(duration.TotalMinutes, 2);

        var photoUrls = checkIn.Photos.Where(p => !p.IsDeleted).Select(p => p.PhotoUrl).ToList();

        await _publishEndpoint.Publish(new TripEndedEvent
        {
            BookingId = booking.Id,
            VehicleId = booking.VehicleId,
            GroupId = booking.GroupId,
            UserId = userId,
            CheckInId = checkIn.Id,
            CheckOutId = checkOut.Id,
            CheckInTime = checkIn.CheckInTime,
            CheckOutTime = checkOut.CheckInTime,
            TripDistance = distance,
            TripDurationMinutes = roundedDurationMinutes,
            IsLateReturn = isLateReturn,
            LateByMinutes = Math.Round(lateByMinutes, 2),
            StartOdometer = checkOut.Odometer,
            EndOdometer = checkIn.Odometer,
            Notes = checkIn.Notes,
            SignatureReference = checkIn.SignatureReference,
            PhotoUrls = photoUrls
        }, cancellationToken);

        await _publishEndpoint.Publish(new VehicleCheckedInEvent
        {
            BookingId = booking.Id,
            VehicleId = booking.VehicleId,
            UserId = userId,
            Odometer = checkIn.Odometer,
            CheckInTime = checkIn.CheckInTime,
            SignatureReference = checkIn.SignatureReference,
            PhotoUrls = photoUrls
        }, cancellationToken);

        if (previousVehicleStatus != vehicle.Status)
        {
            await _publishEndpoint.Publish(new VehicleStatusChangedEvent
            {
                VehicleId = vehicle.Id,
                GroupId = booking.GroupId,
                OldStatus = previousVehicleStatus,
                NewStatus = vehicle.Status,
                ChangedBy = userId,
                ChangedAt = now,
                Reason = "Vehicle returned"
            }, cancellationToken);
        }

        if (booking.Group?.Members?.Any() == true)
        {
            var notificationUserIds = booking.Group.Members
                .Select(m => m.UserId)
                .Where(id => id != userId)
                .Distinct()
                .ToList();

            if (notificationUserIds.Any())
            {
                var durationHours = duration.TotalHours;
                var durationText = durationHours >= 1
                    ? $"{durationHours:F1} hours"
                    : $"{duration.TotalMinutes:F0} minutes";

                var message = $"{booking.User.FirstName} {booking.User.LastName} completed the trip with vehicle {vehicle.PlateNumber}. " +
                              $"Distance: {distance} km. Duration: {durationText}. " +
                              (isLateReturn ? $"Returned {lateByMinutes:F0} minutes late." : "Returned on time.");

                await _publishEndpoint.Publish(new BulkNotificationEvent
                {
                    UserIds = notificationUserIds,
                    GroupId = booking.GroupId,
                    Title = $"Trip completed for {vehicle.Model}",
                    Message = message,
                    Type = "TripCompleted",
                    Priority = isLateReturn ? "High" : "Normal",
                    ActionUrl = $"/bookings/{booking.Id}",
                    ActionText = "View trip summary"
                }, cancellationToken);
            }
        }

        _logger.LogInformation("Trip ended for booking {BookingId}. Distance: {Distance} km, Duration: {Duration} minutes", booking.Id, distance, roundedDurationMinutes);

        var checkInDto = await GetByIdRequiredAsync(checkIn.Id, cancellationToken);

        return new TripCompletionDto
        {
            CheckIn = checkInDto,
            TripDistance = distance,
            TripDurationMinutes = roundedDurationMinutes,
            IsLateReturn = isLateReturn,
            LateByMinutes = Math.Round(lateByMinutes, 2),
            CheckOutTime = checkOut.CheckInTime
        };
    }

    public async Task<IReadOnlyList<CheckInPhotoDto>> UploadPhotosAsync(Guid checkInId, Guid userId, IEnumerable<PhotoUploadItem> uploads, CancellationToken cancellationToken = default)
    {
        var uploadList = uploads?.ToList() ?? new List<PhotoUploadItem>();
        if (uploadList.Count == 0)
        {
            throw new PhotoUploadException("No files provided for upload.");
        }

        var checkIn = await _context.CheckIns
            .Include(ci => ci.Photos)
            .FirstOrDefaultAsync(ci => ci.Id == checkInId, cancellationToken);

        if (checkIn == null)
        {
            throw new KeyNotFoundException("Check-in not found");
        }

        if (checkIn.UserId != userId)
        {
            throw new UnauthorizedAccessException("User is not allowed to modify this check-in");
        }

        checkIn.Photos ??= new List<CheckInPhoto>();

        var activeCount = checkIn.Photos.Count(p => !p.IsDeleted);
        if (activeCount + uploadList.Count > MaxPhotoCount)
        {
            throw new PhotoUploadException($"Uploading these files would exceed the limit of {MaxPhotoCount} photos per check-in.");
        }

        var createdPhotos = new List<CheckInPhoto>();

        foreach (var upload in uploadList)
        {
            ValidateUploadItem(upload);

            if (upload.File.Length > MaxFileSizeBytes)
            {
                throw new PhotoUploadException($"File '{upload.File.FileName}' exceeds the maximum size of 10MB.", StatusCodes.Status413PayloadTooLarge);
            }

            await using var memoryStream = new MemoryStream();
            await upload.File.CopyToAsync(memoryStream, cancellationToken);

            if (memoryStream.Length > MaxFileSizeBytes)
            {
                throw new PhotoUploadException($"File '{upload.File.FileName}' exceeds the maximum size of 10MB.", StatusCodes.Status413PayloadTooLarge);
            }

            memoryStream.Seek(0, SeekOrigin.Begin);
            await _virusScanner.ScanAsync(memoryStream, upload.File.FileName, cancellationToken);

            memoryStream.Seek(0, SeekOrigin.Begin);

            var contentType = upload.File.ContentType ?? "application/octet-stream";
            var isImage = AllowedContentTypes.Contains(contentType) || AllowedExtensions.Contains(Path.GetExtension(upload.File.FileName));

            FileStorageResult storageResult;
            if (isImage)
            {
                storageResult = await _fileStorageService.SaveImageAsync(memoryStream, upload.File.FileName, contentType, cancellationToken);
            }
            else
            {
                storageResult = await _fileStorageService.SaveFileAsync(memoryStream, upload.File.FileName, contentType, cancellationToken);
            }

            var photo = new CheckInPhoto
            {
                CheckInId = checkIn.Id,
                PhotoUrl = storageResult.FileUrl,
                ThumbnailUrl = storageResult.ThumbnailUrl,
                StoragePath = storageResult.StoragePath,
                ThumbnailPath = storageResult.ThumbnailPath,
                ContentType = storageResult.ContentType,
                Type = upload.Type,
                Description = upload.Description,
                CapturedAt = storageResult.CapturedAt,
                Latitude = storageResult.Latitude,
                Longitude = storageResult.Longitude,
                IsDeleted = false
            };

            checkIn.Photos.Add(photo);
            createdPhotos.Add(photo);
        }

        _context.CheckInPhotos.AddRange(createdPhotos);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Uploaded {Count} photos for check-in {CheckInId}", createdPhotos.Count, checkInId);

        return createdPhotos.Select(MapPhotoToDto).ToList();
    }

    public async Task<SignatureCaptureResponseDto> CaptureSignatureAsync(Guid checkInId, Guid userId, SignatureCaptureRequestDto request, SignatureCaptureContext context, CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.SignatureData))
        {
            throw new ArgumentException("Signature data is required.", nameof(request.SignatureData));
        }

        var checkIn = await _context.CheckIns
            .Include(ci => ci.Booking)
                .ThenInclude(b => b.Group)
            .FirstOrDefaultAsync(ci => ci.Id == checkInId, cancellationToken);

        if (checkIn == null)
        {
            throw new KeyNotFoundException("Check-in not found");
        }

        if (checkIn.UserId != userId && !await UserHasAccessAsync(userId, checkIn.Booking.GroupId, cancellationToken))
        {
            throw new UnauthorizedAccessException("User is not allowed to sign this check-in");
        }

        if (!request.OverwriteExisting && !string.IsNullOrWhiteSpace(checkIn.SignatureReference))
        {
            throw new InvalidOperationException("A signature has already been captured for this check-in.");
        }

        var payload = PrepareSignaturePayload(request);
        if (payload.Content.Length == 0)
        {
            throw new ArgumentException("Signature data cannot be empty.", nameof(request.SignatureData));
        }

        if (payload.Content.Length > MaxSignatureBytes)
        {
            throw new ArgumentException($"Signature data exceeds the maximum size of {MaxSignatureBytes / (1024 * 1024)}MB.", nameof(request.SignatureData));
        }

        var fileName = $"checkin-{checkIn.Id:N}-signature-{context.CapturedAt:yyyyMMddHHmmssfff}{payload.FileExtension}";

        await using (var scanStream = new MemoryStream(payload.Content, writable: false))
        {
            await _virusScanner.ScanAsync(scanStream, fileName, cancellationToken);
        }

        FileStorageResult storageResult;
        await using (var storageStream = new MemoryStream(payload.Content, writable: false))
        {
            storageResult = await _fileStorageService.SaveFileAsync(storageStream, fileName, payload.ContentType, cancellationToken);
        }

        var signatureHash = Convert.ToHexString(SHA256.HashData(payload.Content));
        bool? matchesPrevious = null;
        if (!string.IsNullOrWhiteSpace(checkIn.SignatureHash))
        {
            matchesPrevious = string.Equals(checkIn.SignatureHash, signatureHash, StringComparison.OrdinalIgnoreCase);
        }

        var metadataDictionary = BuildSignatureMetadataDictionary(request, context, payload.ContentType, payload.Content.Length);
        var metadataForPersistence = metadataDictionary.Count > 0
            ? new Dictionary<string, string>(metadataDictionary)
            : new Dictionary<string, string>();
        var metadataJson = metadataForPersistence.Count > 0
            ? JsonSerializer.Serialize(metadataForPersistence, new JsonSerializerOptions { WriteIndented = false })
            : null;

        var certificateDocument = new SignatureCertificateDocument(
            checkIn.Id,
            checkIn.BookingId,
            userId,
            context.CapturedAt,
            storageResult.FileUrl,
            signatureHash,
            request.DeviceInfo ?? context.UserAgent,
            request.DeviceId,
            context.IpAddress,
            matchesPrevious,
            request.Notes,
            metadataForPersistence);

        var certificateFileName = $"checkin-{checkIn.Id:N}-signature-cert-{context.CapturedAt:yyyyMMddHHmmssfff}.json";
        FileStorageResult certificateResult;
        await using (var certificateStream = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(certificateDocument, new JsonSerializerOptions { WriteIndented = false })), writable: false))
        {
            certificateResult = await _fileStorageService.SaveFileAsync(certificateStream, certificateFileName, "application/json", cancellationToken);
        }

        var hadExistingSignature = !string.IsNullOrWhiteSpace(checkIn.SignatureReference);

        checkIn.SignatureReference = storageResult.FileUrl;
        checkIn.SignatureDevice = request.DeviceInfo ?? context.UserAgent;
        checkIn.SignatureDeviceId = request.DeviceId;
        checkIn.SignatureIpAddress = context.IpAddress;
        checkIn.SignatureCapturedAt = context.CapturedAt;
        checkIn.SignatureHash = signatureHash;
        checkIn.SignatureMatchesPrevious = matchesPrevious;
        checkIn.SignatureCertificateUrl = certificateResult.FileUrl;
        checkIn.SignatureMetadataJson = metadataJson;

        await _context.SaveChangesAsync(cancellationToken);

        var responseMetadata = new SignatureMetadataDto
        {
            CapturedAt = checkIn.SignatureCapturedAt,
            Device = checkIn.SignatureDevice,
            DeviceId = checkIn.SignatureDeviceId,
            IpAddress = checkIn.SignatureIpAddress,
            Hash = checkIn.SignatureHash,
            MatchesPrevious = checkIn.SignatureMatchesPrevious,
            CertificateUrl = checkIn.SignatureCertificateUrl,
            AdditionalMetadata = metadataForPersistence.Count > 0 ? new Dictionary<string, string>(metadataForPersistence) : null
        };

        var response = new SignatureCaptureResponseDto
        {
            CheckInId = checkIn.Id,
            BookingId = checkIn.BookingId,
            UserId = checkIn.UserId,
            SignatureUrl = storageResult.FileUrl,
            CertificateUrl = certificateResult.FileUrl,
            Metadata = responseMetadata,
            OverwroteExisting = hadExistingSignature
        };

        await _publishEndpoint.Publish(new SignatureCapturedEvent
        {
            CheckInId = checkIn.Id,
            BookingId = checkIn.BookingId,
            UserId = checkIn.UserId,
            SignatureUrl = storageResult.FileUrl,
            SignatureHash = signatureHash,
            CapturedAt = context.CapturedAt,
            Device = checkIn.SignatureDevice,
            DeviceId = checkIn.SignatureDeviceId,
            IpAddress = checkIn.SignatureIpAddress,
            MatchesPrevious = matchesPrevious,
            CertificateUrl = certificateResult.FileUrl,
            Metadata = metadataForPersistence.Count > 0 ? new Dictionary<string, string>(metadataForPersistence) : new Dictionary<string, string>()
        }, cancellationToken);

        _logger.LogInformation("Captured signature for check-in {CheckInId}", checkIn.Id);

        return response;
    }

    public async Task DeletePhotoAsync(Guid checkInId, Guid photoId, Guid userId, CancellationToken cancellationToken = default)
    {
        var photo = await _context.CheckInPhotos
            .Include(p => p.CheckIn)
            .FirstOrDefaultAsync(p => p.Id == photoId && p.CheckInId == checkInId, cancellationToken);

        if (photo == null)
        {
            throw new KeyNotFoundException("Photo not found");
        }

        if (photo.CheckIn.UserId != userId)
        {
            throw new UnauthorizedAccessException("User is not allowed to delete this photo");
        }

        if (photo.IsDeleted)
        {
            return;
        }

        photo.IsDeleted = true;
        await _context.SaveChangesAsync(cancellationToken);

        await _fileStorageService.DeleteAsync(photo.StoragePath ?? string.Empty, photo.ThumbnailPath, cancellationToken);

        _logger.LogInformation("Soft deleted photo {PhotoId} for check-in {CheckInId}", photoId, checkInId);
    }

    public async Task<CheckInDto> CreateAsync(CreateCheckInDto request, Guid userId, CancellationToken cancellationToken = default)
    {
        var booking = await _context.Bookings
            .Include(b => b.Group)
            .Include(b => b.User)
            .FirstOrDefaultAsync(b => b.Id == request.BookingId, cancellationToken);

        if (booking == null)
        {
            throw new KeyNotFoundException("Booking not found for check-in");
        }

        var actorUserId = request.UserId ?? userId;

        if (!await UserHasAccessAsync(actorUserId, booking.GroupId, cancellationToken))
        {
            throw new UnauthorizedAccessException("User does not have access to this booking");
        }

        var checkIn = new CheckIn
        {
            BookingId = booking.Id,
            UserId = actorUserId,
            VehicleId = booking.VehicleId,
            Type = request.Type,
            Odometer = request.Odometer,
            Notes = request.Notes,
            SignatureReference = request.SignatureReference,
            CheckInTime = request.CheckInTime ?? DateTime.UtcNow
        };

        var photoInputs = request.Photos ?? new List<CheckInPhotoInputDto>();

        if (photoInputs.Any())
        {
            checkIn.Photos = photoInputs
                .Select(photo => new CheckInPhoto
                {
                    CheckInId = checkIn.Id,
                    PhotoUrl = photo.PhotoUrl,
                    ThumbnailUrl = null,
                    StoragePath = null,
                    ThumbnailPath = null,
                    ContentType = null,
                    Type = photo.Type,
                    Description = photo.Description,
                    CapturedAt = null,
                    Latitude = null,
                    Longitude = null,
                    IsDeleted = false
                })
                .ToList();
        }

        _context.CheckIns.Add(checkIn);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created check-in {CheckInId} for booking {BookingId}", checkIn.Id, booking.Id);

        return await GetByIdRequiredAsync(checkIn.Id, cancellationToken);
    }

    public async Task<CheckInDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.CheckIns
            .AsNoTracking()
            .Include(ci => ci.User)
            .Include(ci => ci.Photos.Where(p => !p.IsDeleted))
            .FirstOrDefaultAsync(ci => ci.Id == id, cancellationToken);

        return entity == null ? null : MapToDto(entity);
    }

    public async Task<IReadOnlyList<CheckInDto>> GetByBookingAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        var checkIns = await _context.CheckIns
            .AsNoTracking()
            .Include(ci => ci.User)
            .Include(ci => ci.Photos.Where(p => !p.IsDeleted))
            .Where(ci => ci.BookingId == bookingId)
            .OrderBy(ci => ci.CheckInTime)
            .ToListAsync(cancellationToken);

        return checkIns.Select(MapToDto).ToList();
    }

    public async Task<CheckInDto> UpdateAsync(Guid id, UpdateCheckInDto request, Guid userId, CancellationToken cancellationToken = default)
    {
        var checkIn = await _context.CheckIns
            .Include(ci => ci.Photos.Where(p => !p.IsDeleted))
            .Include(ci => ci.Booking)
            .FirstOrDefaultAsync(ci => ci.Id == id, cancellationToken);

        if (checkIn == null)
        {
            throw new KeyNotFoundException("Check-in not found");
        }

        if (checkIn.UserId != userId && !await UserHasAccessAsync(userId, checkIn.Booking.GroupId, cancellationToken))
        {
            throw new UnauthorizedAccessException("User is not allowed to modify this check-in");
        }

        checkIn.Odometer = request.Odometer;
        checkIn.Type = request.Type;
        checkIn.Notes = request.Notes;
        checkIn.SignatureReference = request.SignatureReference;
        checkIn.CheckInTime = request.CheckInTime ?? checkIn.CheckInTime;

        var photoInputs = request.Photos ?? new List<CheckInPhotoInputDto>();

        if (photoInputs.Any())
        {
            _context.CheckInPhotos.RemoveRange(checkIn.Photos);
            checkIn.Photos = photoInputs
                .Select(photo => new CheckInPhoto
                {
                    CheckInId = checkIn.Id,
                    PhotoUrl = photo.PhotoUrl,
                    Type = photo.Type,
                    Description = photo.Description
                })
                .ToList();
        }
        else if (checkIn.Photos.Any())
        {
            _context.CheckInPhotos.RemoveRange(checkIn.Photos);
            checkIn.Photos.Clear();
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated check-in {CheckInId}", checkIn.Id);

        return await GetByIdRequiredAsync(checkIn.Id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var checkIn = await _context.CheckIns
            .Include(ci => ci.Booking)
            .FirstOrDefaultAsync(ci => ci.Id == id, cancellationToken);

        if (checkIn == null)
        {
            return;
        }

        if (checkIn.UserId != userId && !await UserHasAccessAsync(userId, checkIn.Booking.GroupId, cancellationToken))
        {
            throw new UnauthorizedAccessException("User is not allowed to delete this check-in");
        }

        _context.CheckIns.Remove(checkIn);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted check-in {CheckInId}", checkIn.Id);
    }

    private async Task<bool> UserHasAccessAsync(Guid userId, Guid groupId, CancellationToken cancellationToken)
    {
        return await _context.GroupMembers
            .AsNoTracking()
            .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId, cancellationToken);
    }

    private async Task<CheckInDto> GetByIdRequiredAsync(Guid id, CancellationToken cancellationToken)
    {
        var dto = await GetByIdAsync(id, cancellationToken);
        if (dto == null)
        {
            throw new InvalidOperationException("Failed to load check-in after persistence");
        }

        return dto;
    }

    private static SignaturePayload PrepareSignaturePayload(SignatureCaptureRequestDto request)
    {
        var raw = request.SignatureData.Trim();

        if (raw.StartsWith("<svg", StringComparison.OrdinalIgnoreCase))
        {
            var svgMarkup = EnsureSvgHasNamespace(raw);
            var svgBytes = Encoding.UTF8.GetBytes(svgMarkup);
            var svgContentType = NormalizeContentType(request.Format ?? "image/svg+xml");
            return new SignaturePayload(svgBytes, svgContentType, GetExtensionForContentType(svgContentType));
        }

        var dataUrlMatch = DataUrlRegex.Match(raw);
        if (dataUrlMatch.Success)
        {
            if (!TryDecodeBase64(dataUrlMatch.Groups["data"].Value, out var decodedFromDataUrl))
            {
                throw new ArgumentException("Signature data URL payload is not valid base64 data.", nameof(request.SignatureData));
            }

            var mime = dataUrlMatch.Groups["mime"].Value;
            var contentTypeFromDataUrl = NormalizeContentType(request.Format ?? mime);
            return new SignaturePayload(decodedFromDataUrl, contentTypeFromDataUrl, GetExtensionForContentType(contentTypeFromDataUrl));
        }

        if (TryDecodeBase64(raw, out var decodedBytes))
        {
            var base64ContentType = NormalizeContentType(request.Format ?? "image/png");
            return new SignaturePayload(decodedBytes, base64ContentType, GetExtensionForContentType(base64ContentType));
        }

        var sanitizedPath = SanitizeSvgPath(raw);
        var svgFromPath = BuildSvgFromPath(sanitizedPath);
        var pathContentType = NormalizeContentType(request.Format ?? "image/svg+xml");
        return new SignaturePayload(Encoding.UTF8.GetBytes(svgFromPath), pathContentType, GetExtensionForContentType(pathContentType));
    }

    private static Dictionary<string, string> BuildSignatureMetadataDictionary(SignatureCaptureRequestDto request, SignatureCaptureContext context, string contentType, int byteLength)
    {
        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["contentType"] = contentType,
            ["byteLength"] = byteLength.ToString(CultureInfo.InvariantCulture),
            ["capturedAt"] = context.CapturedAt.ToString("O", CultureInfo.InvariantCulture),
            ["overwriteExisting"] = request.OverwriteExisting.ToString(CultureInfo.InvariantCulture)
        };

        if (!string.IsNullOrWhiteSpace(request.Format))
        {
            metadata["format"] = request.Format.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.DeviceInfo))
        {
            metadata["deviceInfo"] = request.DeviceInfo.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.DeviceId))
        {
            metadata["deviceId"] = request.DeviceId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(context.UserAgent))
        {
            metadata["userAgent"] = context.UserAgent;
        }

        if (!string.IsNullOrWhiteSpace(context.IpAddress))
        {
            metadata["ipAddress"] = context.IpAddress;
        }

        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            metadata["notes"] = request.Notes.Trim();
        }

        return metadata;
    }

    private static string EnsureSvgHasNamespace(string svgMarkup)
    {
        var trimmed = svgMarkup.Trim();
        if (trimmed.IndexOf("xmlns", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return trimmed;
        }

        var tagEndIndex = trimmed.IndexOf('>');
        if (tagEndIndex < 0)
        {
            throw new ArgumentException("SVG markup is invalid.");
        }

        var builder = new StringBuilder(trimmed.Length + 50);
        builder.Append(trimmed.AsSpan(0, tagEndIndex));
        builder.Append(" xmlns=\"http://www.w3.org/2000/svg\"");
        builder.Append(trimmed.AsSpan(tagEndIndex));
        return builder.ToString();
    }

    private static string SanitizeSvgPath(string pathData)
    {
        var trimmed = pathData.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("Signature path data cannot be empty.");
        }

        if (trimmed.Length > MaxSignaturePathLength)
        {
            throw new ArgumentException("Signature path data exceeds the maximum supported length.");
        }

        var normalizedWhitespace = MultipleWhitespaceRegex.Replace(trimmed, " ");
        if (!SvgPathRegex.IsMatch(normalizedWhitespace))
        {
            throw new ArgumentException("Signature path data contains unsupported characters.");
        }

        return normalizedWhitespace;
    }

    private static string BuildSvgFromPath(string pathData)
    {
        var safePath = pathData.Replace("\"", string.Empty, StringComparison.Ordinal).Replace("'", string.Empty, StringComparison.Ordinal);
        return $"<svg xmlns=\"http://www.w3.org/2000/svg\" version=\"1.1\" viewBox=\"0 0 1000 400\" preserveAspectRatio=\"xMidYMid meet\"><path d=\"{safePath}\" fill=\"none\" stroke=\"#000000\" stroke-width=\"4\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/></svg>";
    }

    private static string NormalizeContentType(string format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return "image/png";
        }

        var trimmed = format.Trim();
        if (trimmed.Equals("png", StringComparison.OrdinalIgnoreCase))
        {
            return "image/png";
        }

        if (trimmed.Equals("jpg", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("jpeg", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("image/jpg", StringComparison.OrdinalIgnoreCase))
        {
            return "image/jpeg";
        }

        if (trimmed.Equals("svg", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("svg+xml", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("image/svg", StringComparison.OrdinalIgnoreCase))
        {
            return "image/svg+xml";
        }

        if (!trimmed.Contains('/', StringComparison.Ordinal))
        {
            return $"image/{trimmed.ToLowerInvariant()}";
        }

        return trimmed.ToLowerInvariant();
    }

    private static string GetExtensionForContentType(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/jpg" => ".jpg",
            "image/svg+xml" => ".svg",
            "application/json" => ".json",
            _ => ".bin"
        };
    }

    private static bool TryDecodeBase64(string value, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = NormalizeBase64(value);
        if (normalized.Length < 8 || normalized.Length % 4 != 0)
        {
            return false;
        }

        try
        {
            bytes = Convert.FromBase64String(normalized);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string NormalizeBase64(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (!char.IsWhiteSpace(ch))
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }

    private record SignaturePayload(byte[] Content, string ContentType, string FileExtension);

    private record SignatureCertificateDocument(
        Guid CheckInId,
        Guid BookingId,
        Guid SignedByUserId,
        DateTime CapturedAt,
        string SignatureUrl,
        string SignatureHash,
        string? Device,
        string? DeviceId,
        string? IpAddress,
        bool? MatchesPrevious,
        string? Notes,
        Dictionary<string, string> Metadata);

    private static void ValidateUploadItem(PhotoUploadItem upload)
    {
        if (upload.File == null)
        {
            throw new PhotoUploadException("Upload item is missing a file.");
        }

        if (upload.File.Length == 0)
        {
            throw new PhotoUploadException($"File '{upload.File.FileName}' is empty.");
        }

        var contentType = upload.File.ContentType ?? string.Empty;
        var extension = Path.GetExtension(upload.File.FileName ?? string.Empty);
        extension = string.IsNullOrWhiteSpace(extension) ? string.Empty : extension.ToLowerInvariant();

        bool isAllowedImage = AllowedContentTypes.Contains(contentType) || AllowedExtensions.Contains(extension);
        bool isAllowedDocument = AllowedDocumentContentTypes.Contains(contentType) || AllowedDocumentExtensions.Contains(extension);

        if (!isAllowedImage && !isAllowedDocument)
        {
            throw new PhotoUploadException($"File '{upload.File.FileName}' is not a supported image or document type.");
        }
    }

    private static CheckInPhotoDto MapPhotoToDto(CheckInPhoto photo)
    {
        return new CheckInPhotoDto
        {
            Id = photo.Id,
            CheckInId = photo.CheckInId,
            PhotoUrl = photo.PhotoUrl,
            ThumbnailUrl = photo.ThumbnailUrl,
            Type = photo.Type,
            Description = photo.Description,
            ContentType = photo.ContentType,
            CapturedAt = photo.CapturedAt,
            Latitude = photo.Latitude,
            Longitude = photo.Longitude,
            IsDeleted = photo.IsDeleted
        };
    }

    private static SignatureMetadataDto? MapSignatureMetadata(CheckIn entity)
    {
        if (entity.SignatureCapturedAt == null &&
            string.IsNullOrWhiteSpace(entity.SignatureDevice) &&
            string.IsNullOrWhiteSpace(entity.SignatureHash) &&
            string.IsNullOrWhiteSpace(entity.SignatureCertificateUrl) &&
            string.IsNullOrWhiteSpace(entity.SignatureMetadataJson))
        {
            return null;
        }

        Dictionary<string, string>? additional = null;
        if (!string.IsNullOrWhiteSpace(entity.SignatureMetadataJson))
        {
            try
            {
                additional = JsonSerializer.Deserialize<Dictionary<string, string>>(entity.SignatureMetadataJson);
            }
            catch (JsonException)
            {
                additional = null;
            }
        }

        return new SignatureMetadataDto
        {
            CapturedAt = entity.SignatureCapturedAt,
            Device = entity.SignatureDevice,
            DeviceId = entity.SignatureDeviceId,
            IpAddress = entity.SignatureIpAddress,
            Hash = entity.SignatureHash,
            MatchesPrevious = entity.SignatureMatchesPrevious,
            CertificateUrl = entity.SignatureCertificateUrl,
            AdditionalMetadata = additional
        };
    }

    private static CheckInDto MapToDto(CheckIn entity)
    {
        return new CheckInDto
        {
            Id = entity.Id,
            BookingId = entity.BookingId,
            UserId = entity.UserId,
            VehicleId = entity.VehicleId,
            UserFirstName = entity.User?.FirstName ?? string.Empty,
            UserLastName = entity.User?.LastName ?? string.Empty,
            Type = entity.Type,
            Odometer = entity.Odometer,
            CheckInTime = entity.CheckInTime,
            Notes = entity.Notes,
            SignatureReference = entity.SignatureReference,
            SignatureMetadata = MapSignatureMetadata(entity),
            Photos = entity.Photos?.Where(photo => !photo.IsDeleted).Select(MapPhotoToDto).ToList() ?? new List<CheckInPhotoDto>(),
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
