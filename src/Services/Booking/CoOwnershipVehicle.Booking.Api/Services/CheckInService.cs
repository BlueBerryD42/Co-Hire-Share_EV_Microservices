using System.IO;
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
            Photos = entity.Photos?.Where(photo => !photo.IsDeleted).Select(MapPhotoToDto).ToList() ?? new List<CheckInPhotoDto>(),
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
