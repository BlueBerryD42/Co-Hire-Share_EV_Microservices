using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Shared.Contracts.DTOs;

namespace CoOwnershipVehicle.Booking.Api.Contracts;

public interface ILateReturnFeeService
{
    Task<LateReturnFeeProcessingResult> EvaluateLateReturnAsync(
        CoOwnershipVehicle.Domain.Entities.Booking booking,
        CheckIn checkIn,
        double lateMinutes,
        CancellationToken cancellationToken = default);

    Task<LateReturnFeeDto?> GetByIdAsync(Guid feeId, CancellationToken cancellationToken = default);

    Task<LateReturnFeeDto?> GetByCheckInAsync(Guid checkInId, CancellationToken cancellationToken = default);

    Task<LateReturnFeeDto> WaiveAsync(Guid feeId, Guid adminId, string? reason, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LateReturnFeeDto>> GetUserHistoryAsync(Guid userId, int? take = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LateReturnFeeDto>> GetByBookingAsync(Guid bookingId, CancellationToken cancellationToken = default);
}

public class LateReturnFeeProcessingResult
{
    public bool IsLateReturn { get; set; }
    public bool FeeCreated { get; set; }
    public double ChargeableMinutes { get; set; }
    public decimal FeeAmount { get; set; }
    public LateReturnFeeDto? Fee { get; set; }
}
