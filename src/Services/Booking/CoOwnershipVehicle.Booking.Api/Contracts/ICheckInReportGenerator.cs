using System.Threading;
using System.Threading.Tasks;
using CoOwnershipVehicle.Shared.Contracts.DTOs;

namespace CoOwnershipVehicle.Booking.Api.Contracts;

public interface ICheckInReportGenerator
{
    Task<byte[]> GenerateAsync(BookingCheckInHistoryDto history, CancellationToken cancellationToken = default);
}
