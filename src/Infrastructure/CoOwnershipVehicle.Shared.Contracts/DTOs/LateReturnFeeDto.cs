using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Shared.Contracts.DTOs;

public class LateReturnFeeDto
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public Guid CheckInId { get; set; }
    public Guid UserId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid GroupId { get; set; }
    public int LateDurationMinutes { get; set; }
    public decimal FeeAmount { get; set; }
    public decimal? OriginalFeeAmount { get; set; }
    public string? CalculationMethod { get; set; }
    public LateReturnFeeStatus Status { get; set; }
    public Guid? ExpenseId { get; set; }
    public Guid? InvoiceId { get; set; }
    public Guid? WaivedBy { get; set; }
    public string? WaivedReason { get; set; }
    public DateTime? WaivedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
