using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoOwnershipVehicle.Domain.Entities;

public class LedgerEntry : BaseEntity
{
    public Guid GroupId { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }
    
    public LedgerEntryType Type { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal BalanceAfter { get; set; }
    
    [Required]
    [StringLength(200)]
    public string Description { get; set; } = string.Empty;
    
    [StringLength(100)]
    public string? Reference { get; set; }
    
    public Guid? RelatedEntityId { get; set; }
    
    public string? RelatedEntityType { get; set; }
    
    // Navigation properties
    public virtual OwnershipGroup Group { get; set; } = null!;
}

public enum LedgerEntryType
{
    Deposit = 0,
    Withdrawal = 1,
    ExpensePayment = 2,
    RefundReceived = 3,
    InterestEarned = 4,
    Fee = 5,
    Adjustment = 6
}
