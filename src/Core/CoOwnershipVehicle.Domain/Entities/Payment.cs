using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoOwnershipVehicle.Domain.Entities;

public class Payment : BaseEntity
{
    public Guid? InvoiceId { get; set; } // Nullable to support fund deposits without invoices
    
    public Guid PayerId { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }
    
    public PaymentMethod Method { get; set; }
    
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    
    [StringLength(100)]
    public string? TransactionReference { get; set; }
    
    public DateTime? PaidAt { get; set; }
    
    [StringLength(500)]
    public string? Notes { get; set; }
    
    // Navigation properties
    public virtual Invoice? Invoice { get; set; }
    public virtual User Payer { get; set; } = null!;
}

public enum PaymentMethod
{
    Cash = 0,
    BankTransfer = 1,
    CreditCard = 2,
    DebitCard = 3,
    EWallet = 4,
    Other = 5
}

public enum PaymentStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3,
    Refunded = 4,
    Cancelled = 5
}
