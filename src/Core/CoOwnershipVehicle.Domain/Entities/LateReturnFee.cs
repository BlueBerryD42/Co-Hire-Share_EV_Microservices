using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoOwnershipVehicle.Domain.Entities;

public class LateReturnFee : BaseEntity
{
    public Guid BookingId { get; set; }

    public Guid CheckInId { get; set; }

    public Guid UserId { get; set; }

    public Guid VehicleId { get; set; }

    public Guid GroupId { get; set; }

    public int LateDurationMinutes { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal FeeAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? OriginalFeeAmount { get; set; }

    [StringLength(200)]
    public string? CalculationMethod { get; set; }

    public LateReturnFeeStatus Status { get; set; } = LateReturnFeeStatus.Pending;

    public Guid? ExpenseId { get; set; }

    public Guid? InvoiceId { get; set; }

    public Guid? WaivedBy { get; set; }

    [StringLength(500)]
    public string? WaivedReason { get; set; }

    public DateTime? WaivedAt { get; set; }

    // Navigation properties
    public virtual Booking Booking { get; set; } = null!;

    public virtual CheckIn CheckIn { get; set; } = null!;

    public virtual User User { get; set; } = null!;

    public virtual Vehicle Vehicle { get; set; } = null!;
}

public enum LateReturnFeeStatus
{
    Pending = 0,
    Paid = 1,
    Waived = 2
}
