using System.Globalization;
using System.Linq;
using CoOwnershipVehicle.Booking.Api.Configuration;
using CoOwnershipVehicle.Booking.Api.Contracts;
using CoOwnershipVehicle.Booking.Api.Repositories;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Shared.Contracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoOwnershipVehicle.Booking.Api.Services;

public class LateReturnFeeService : ILateReturnFeeService
{
    private readonly ILateReturnFeeRepository _lateReturnFeeRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IOptions<LateReturnFeeOptions> _options;
    private readonly ILogger<LateReturnFeeService> _logger;

    public LateReturnFeeService(
        ILateReturnFeeRepository lateReturnFeeRepository,
        IBookingRepository bookingRepository,
        IPublishEndpoint publishEndpoint,
        IOptions<LateReturnFeeOptions> options,
        ILogger<LateReturnFeeService> logger)
    {
        _lateReturnFeeRepository = lateReturnFeeRepository ?? throw new ArgumentNullException(nameof(lateReturnFeeRepository));
        _bookingRepository = bookingRepository ?? throw new ArgumentNullException(nameof(bookingRepository));
        _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<LateReturnFeeProcessingResult> EvaluateLateReturnAsync(
        CoOwnershipVehicle.Domain.Entities.Booking booking,
        CheckIn checkIn,
        double lateMinutes,
        CancellationToken cancellationToken = default)
    {
        if (booking == null)
        {
            throw new ArgumentNullException(nameof(booking));
        }

        if (checkIn == null)
        {
            throw new ArgumentNullException(nameof(checkIn));
        }

        var result = new LateReturnFeeProcessingResult
        {
            IsLateReturn = lateMinutes > 0
        };

        checkIn.IsLateReturn = result.IsLateReturn;
        checkIn.LateReturnMinutes = result.IsLateReturn ? Math.Round(lateMinutes, 2, MidpointRounding.AwayFromZero) : null;

        if (!result.IsLateReturn)
        {
            checkIn.LateFeeAmount = 0;
            return result;
        }

        var options = _options.Value ?? new LateReturnFeeOptions();

        var chargeableMinutes = Math.Max(0d, lateMinutes - options.GracePeriodMinutes);
        result.ChargeableMinutes = chargeableMinutes;

        if (chargeableMinutes <= 0)
        {
            _logger.LogInformation("Late return detected for booking {BookingId}, but within grace period of {Grace} minutes.",
                booking.Id, options.GracePeriodMinutes);
            checkIn.LateFeeAmount = 0;
            return result;
        }

        var selectedBand = SelectBand(options, chargeableMinutes);
        decimal feeAmount = 0m;
        string? calculationMethod = null;

        if (selectedBand != null)
        {
            feeAmount = CalculateFee(selectedBand, chargeableMinutes);
            calculationMethod = !string.IsNullOrWhiteSpace(selectedBand.Label)
                ? selectedBand.Label
                : $"Band({selectedBand.FromMinutes}-{(selectedBand.ToMinutes?.ToString(CultureInfo.InvariantCulture) ?? "open-ended")})";
        }
        else if (options.DefaultHourlyRate > 0)
        {
            feeAmount = CalculateDefault(options.DefaultHourlyRate, chargeableMinutes);
            calculationMethod = "DefaultRate";
        }

        if (feeAmount <= 0)
        {
            _logger.LogInformation("Late return detected for booking {BookingId}, but no fee calculated (chargeable minutes: {Minutes}).",
                booking.Id, chargeableMinutes);
            checkIn.LateFeeAmount = 0;
            return result;
        }

        feeAmount = Math.Min(feeAmount, options.MaxFeeAmount);
        feeAmount = Math.Round(feeAmount, 2, MidpointRounding.AwayFromZero);

        checkIn.LateFeeAmount = feeAmount;

        var now = DateTime.UtcNow;

        var fee = new LateReturnFee
        {
            Id = Guid.NewGuid(),
            BookingId = booking.Id,
            CheckInId = checkIn.Id,
            UserId = booking.UserId,
            VehicleId = booking.VehicleId,
            GroupId = booking.GroupId,
            LateDurationMinutes = (int)Math.Ceiling(chargeableMinutes),
            FeeAmount = feeAmount,
            OriginalFeeAmount = feeAmount,
            CalculationMethod = calculationMethod,
            Status = LateReturnFeeStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };

        fee.CheckIn = checkIn;
        fee.Booking = booking;
        checkIn.LateReturnFee = fee;

        await _lateReturnFeeRepository.AddAsync(fee, cancellationToken);
        await _lateReturnFeeRepository.SaveChangesAsync(cancellationToken);

        result.FeeCreated = true;
        result.FeeAmount = feeAmount;
        result.Fee = MapToDto(fee);

        _logger.LogInformation("Late return fee {FeeId} created for booking {BookingId} with amount {FeeAmount}.",
            fee.Id, booking.Id, feeAmount);

        await _publishEndpoint.Publish(new LateReturnFeeCreatedEvent
        {
            LateReturnFeeId = fee.Id,
            BookingId = booking.Id,
            CheckInId = checkIn.Id,
            UserId = booking.UserId,
            GroupId = booking.GroupId,
            VehicleId = booking.VehicleId,
            LateByMinutes = Math.Round(lateMinutes, 2, MidpointRounding.AwayFromZero),
            ChargeableMinutes = Math.Round(chargeableMinutes, 2, MidpointRounding.AwayFromZero),
            FeeAmount = feeAmount,
            Status = fee.Status,
            GracePeriodMinutes = options.GracePeriodMinutes,
            CalculationMethod = calculationMethod,
            DetectedAt = now
        }, cancellationToken);

        return result;
    }

    public async Task<LateReturnFeeDto?> GetByIdAsync(Guid feeId, CancellationToken cancellationToken = default)
    {
        var fee = await _lateReturnFeeRepository.GetByIdAsync(feeId, cancellationToken);
        return fee == null ? null : MapToDto(fee);
    }

    public async Task<LateReturnFeeDto?> GetByCheckInAsync(Guid checkInId, CancellationToken cancellationToken = default)
    {
        var fee = await _lateReturnFeeRepository.GetByCheckInAsync(checkInId, cancellationToken);
        return fee == null ? null : MapToDto(fee);
    }

    public async Task<LateReturnFeeDto> WaiveAsync(Guid feeId, Guid adminId, string? reason, CancellationToken cancellationToken = default)
    {
        var fee = await _lateReturnFeeRepository.GetForUpdateAsync(feeId, cancellationToken);

        if (fee == null)
        {
            throw new KeyNotFoundException("Late return fee not found.");
        }

        if (fee.Status == LateReturnFeeStatus.Waived)
        {
            _logger.LogInformation("Late return fee {FeeId} already waived.", feeId);
            return MapToDto(fee);
        }

        if (fee.OriginalFeeAmount == null || fee.OriginalFeeAmount == 0)
        {
            fee.OriginalFeeAmount = fee.FeeAmount;
        }

        fee.FeeAmount = 0;
        fee.Status = LateReturnFeeStatus.Waived;
        fee.WaivedBy = adminId;
        fee.WaivedReason = reason;
        fee.WaivedAt = DateTime.UtcNow;
        fee.UpdatedAt = DateTime.UtcNow;

        if (fee.CheckIn != null)
        {
            fee.CheckIn.LateFeeAmount = 0;
        }

        await _lateReturnFeeRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Late return fee {FeeId} waived by admin {AdminId}.", feeId, adminId);

        await _publishEndpoint.Publish(new LateReturnFeeStatusChangedEvent
        {
            LateReturnFeeId = fee.Id,
            BookingId = fee.BookingId,
            CheckInId = fee.CheckInId,
            UserId = fee.UserId,
            Status = fee.Status,
            ChangedBy = adminId,
            Reason = reason,
            FeeAmount = fee.FeeAmount,
            ExpenseId = fee.ExpenseId,
            InvoiceId = fee.InvoiceId,
            ChangedAt = fee.WaivedAt ?? DateTime.UtcNow
        }, cancellationToken);

        return MapToDto(fee);
    }

    public async Task<IReadOnlyList<LateReturnFeeDto>> GetUserHistoryAsync(Guid userId, int? take = null, CancellationToken cancellationToken = default)
    {
        var fees = await _lateReturnFeeRepository.GetByUserAsync(userId, take, cancellationToken);
        return fees.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<LateReturnFeeDto>> GetByBookingAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        var fees = await _lateReturnFeeRepository.GetByBookingAsync(bookingId, cancellationToken);
        return fees.Select(MapToDto).ToList();
    }

    private static LateReturnFeeDto MapToDto(LateReturnFee fee)
    {
        return new LateReturnFeeDto
        {
            Id = fee.Id,
            BookingId = fee.BookingId,
            CheckInId = fee.CheckInId,
            UserId = fee.UserId,
            VehicleId = fee.VehicleId,
            GroupId = fee.GroupId,
            LateDurationMinutes = fee.LateDurationMinutes,
            FeeAmount = fee.FeeAmount,
            OriginalFeeAmount = fee.OriginalFeeAmount,
            CalculationMethod = fee.CalculationMethod,
            Status = fee.Status,
            ExpenseId = fee.ExpenseId,
            InvoiceId = fee.InvoiceId,
            WaivedBy = fee.WaivedBy,
            WaivedReason = fee.WaivedReason,
            WaivedAt = fee.WaivedAt,
            CreatedAt = fee.CreatedAt,
            UpdatedAt = fee.UpdatedAt
        };
    }

    private static decimal CalculateFee(LateReturnFeeBand band, double chargeableMinutes)
    {
        var hours = (decimal)chargeableMinutes / 60m;
        var fee = hours * band.RatePerHour;

        if (band.FlatFee.HasValue && band.FlatFee.Value > 0)
        {
            fee += band.FlatFee.Value;
        }

        return fee;
    }

    private static decimal CalculateDefault(decimal defaultRate, double chargeableMinutes)
    {
        var hours = (decimal)chargeableMinutes / 60m;
        return hours * defaultRate;
    }

    private static LateReturnFeeBand? SelectBand(LateReturnFeeOptions options, double chargeableMinutes)
    {
        if (options.Bands == null || options.Bands.Count == 0)
        {
            return null;
        }

        return options.Bands
            .OrderBy(b => b.FromMinutes)
            .FirstOrDefault(b =>
                chargeableMinutes >= b.FromMinutes &&
                (!b.ToMinutes.HasValue || chargeableMinutes < b.ToMinutes.Value));
    }
}
