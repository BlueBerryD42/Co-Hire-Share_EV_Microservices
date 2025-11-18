using CoOwnershipVehicle.Booking.Api.Configuration;
using CoOwnershipVehicle.Booking.Api.Contracts;
using CoOwnershipVehicle.Booking.Api.Repositories;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoOwnershipVehicle.Booking.Api.Services;

public class CheckInService : ICheckInService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly ICheckInRepository _checkInRepository;
    private readonly ILogger<CheckInService> _logger;
    private readonly TripPricingOptions _pricing;

    public CheckInService(
        IBookingRepository bookingRepository,
        ICheckInRepository checkInRepository,
        ILogger<CheckInService> logger,
        IOptions<TripPricingOptions> pricingOptions)
    {
        _bookingRepository = bookingRepository ?? throw new ArgumentNullException(nameof(bookingRepository));
        _checkInRepository = checkInRepository ?? throw new ArgumentNullException(nameof(checkInRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pricing = pricingOptions?.Value ?? throw new ArgumentNullException(nameof(pricingOptions));
    }

    public async Task<CheckInDto> StartTripAsync(StartTripDto request, Guid userId, CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var booking = await _bookingRepository.GetBookingWithVehicleAndUserAsync(request.BookingId, cancellationToken)
                      ?? throw new ArgumentException("Booking not found", nameof(request.BookingId));

        if (booking.UserId != userId)
        {
            throw new UnauthorizedAccessException("You cannot start a trip for this booking.");
        }

        if (DateTime.UtcNow > booking.EndAt)
        {
            _logger.LogWarning("User {UserId} attempted to start a trip for booking {BookingId} after it ended at {EndAt}.", userId, booking.Id, booking.EndAt);
            throw new InvalidOperationException("This booking has already ended. Please create a new booking to start a trip.");
        }

        if (await HasPendingCheckOutAsync(booking.Id, cancellationToken))
        {
            _logger.LogWarning("User {UserId} attempted to start a trip for booking {BookingId} while another trip was still in progress.", userId, booking.Id);
            throw new InvalidOperationException("Trip already in progress. Please complete the checkout before starting again.");
        }

        var checkIn = new CheckIn
        {
            Id = Guid.NewGuid(),
            BookingId = booking.Id,
            UserId = userId,
            VehicleId = booking.VehicleId,
            Type = CheckInType.CheckOut,
            Odometer = request.OdometerReading,
            Notes = request.Notes,
            SignatureReference = request.SignatureReference,
            CheckInTime = request.ClientTimestamp ?? DateTime.UtcNow,
            Photos = MapPhotos(request.Photos)
        };

        booking.VehicleStatus = VehicleStatus.InUse;
        booking.UpdatedAt = DateTime.UtcNow;

        await _checkInRepository.AddAsync(checkIn, cancellationToken);
        await _checkInRepository.SaveChangesAsync(cancellationToken);
        await _bookingRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Trip started for booking {BookingId} by user {UserId}", booking.Id, userId);
        return MapToDto(checkIn);
    }

    public async Task<CheckInDto> EndTripAsync(EndTripDto request, Guid userId, CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var booking = await _bookingRepository.GetBookingWithVehicleAndUserAsync(request.BookingId, cancellationToken)
                      ?? throw new ArgumentException("Booking not found", nameof(request.BookingId));

        if (booking.UserId != userId)
        {
            throw new UnauthorizedAccessException("You cannot complete this trip.");
        }

        var startEntry = await _checkInRepository.GetLatestAsync(booking.Id, CheckInType.CheckOut, cancellationToken);
        if (startEntry == null)
        {
            throw new InvalidOperationException("Trip has not been started yet.");
        }

        var checkIn = new CheckIn
        {
            Id = Guid.NewGuid(),
            BookingId = booking.Id,
            UserId = userId,
            VehicleId = booking.VehicleId,
            Type = CheckInType.CheckIn,
            Odometer = request.OdometerReading,
            Notes = request.Notes,
            SignatureReference = request.SignatureReference,
            CheckInTime = request.ClientTimestamp ?? DateTime.UtcNow,
            Photos = MapPhotos(request.Photos)
        };

        await _checkInRepository.AddAsync(checkIn, cancellationToken);
        await _checkInRepository.SaveChangesAsync(cancellationToken);

        var distance = Math.Max(0, request.OdometerReading - startEntry.Odometer);
        booking.DistanceKm = distance;
        booking.TripFeeAmount = CalculateFee(distance);
        booking.VehicleStatus = VehicleStatus.Available;
        booking.UpdatedAt = DateTime.UtcNow;

        await _bookingRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Trip ended for booking {BookingId}. Distance {Distance} km.", booking.Id, distance);
        return MapToDto(checkIn);
    }

    public async Task<IReadOnlyList<CheckInDto>> GetBookingHistoryAsync(Guid bookingId, Guid userId, CancellationToken cancellationToken = default)
    {
        var booking = await _bookingRepository.GetBookingWithVehicleAndUserAsync(bookingId, cancellationToken)
                      ?? throw new ArgumentException("Booking not found", nameof(bookingId));

        if (booking.UserId != userId)
        {
            throw new UnauthorizedAccessException("You cannot view this booking.");
        }

        var entries = await _checkInRepository.GetByBookingAsync(bookingId, cancellationToken);
        return entries.Select(MapToDto).ToList();
    }

    private async Task<bool> HasPendingCheckOutAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        var latestCheckOut = await _checkInRepository.GetLatestAsync(bookingId, CheckInType.CheckOut, cancellationToken);
        if (latestCheckOut == null)
        {
            return false;
        }

        var latestCheckIn = await _checkInRepository.GetLatestAsync(bookingId, CheckInType.CheckIn, cancellationToken);
        if (latestCheckIn == null)
        {
            return true;
        }

        return latestCheckOut.CheckInTime >= latestCheckIn.CheckInTime;
    }

    private decimal CalculateFee(decimal distance)
    {
        var cost = Math.Round(distance * _pricing.CostPerKm, 2, MidpointRounding.AwayFromZero);
        if (_pricing.MinimumFee.HasValue && cost < _pricing.MinimumFee.Value)
        {
            return _pricing.MinimumFee.Value;
        }
        return cost;
    }

    private static List<CheckInPhoto> MapPhotos(IEnumerable<CheckInPhotoInputDto>? photos)
    {
        if (photos == null)
        {
            return new List<CheckInPhoto>();
        }

        return photos
            .Where(p => !string.IsNullOrWhiteSpace(p.PhotoUrl))
            .Select(p => new CheckInPhoto
            {
                Id = Guid.NewGuid(),
                PhotoUrl = p.PhotoUrl,
                Type = p.Type,
                Description = p.Description
            })
            .ToList();
    }

    private static CheckInDto MapToDto(CheckIn entity)
    {
        return new CheckInDto
        {
            Id = entity.Id,
            BookingId = entity.BookingId,
            UserId = entity.UserId,
            VehicleId = entity.VehicleId,
            Type = entity.Type,
            Odometer = entity.Odometer,
            Notes = entity.Notes,
            SignatureReference = entity.SignatureReference,
            CheckInTime = entity.CheckInTime,
            Photos = entity.Photos.Select(p => new CheckInPhotoDto
            {
                Id = p.Id,
                CheckInId = entity.Id,
                PhotoUrl = p.PhotoUrl,
                Description = p.Description,
                Type = p.Type
            }).ToList(),
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
