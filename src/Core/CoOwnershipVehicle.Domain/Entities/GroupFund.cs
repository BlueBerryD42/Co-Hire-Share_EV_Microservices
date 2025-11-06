using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoOwnershipVehicle.Domain.Entities;

public class GroupFund : BaseEntity
{
    public Guid GroupId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalBalance { get; set; } = 0m;

    [Column(TypeName = "decimal(18,2)")]
    public decimal ReserveBalance { get; set; } = 0m;

    [NotMapped]
    public decimal AvailableBalance => TotalBalance - ReserveBalance;

    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual OwnershipGroup Group { get; set; } = null!;
    public virtual ICollection<FundTransaction> Transactions { get; set; } = new List<FundTransaction>();
}

public class FundTransaction : BaseEntity
{
    public Guid GroupId { get; set; }

    public Guid InitiatedBy { get; set; }

    public FundTransactionType Type { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal BalanceBefore { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal BalanceAfter { get; set; }

    [Required]
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    public FundTransactionStatus Status { get; set; } = FundTransactionStatus.Pending;

    public Guid? ApprovedBy { get; set; }

    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

    [StringLength(200)]
    public string? Reference { get; set; }

    // Navigation properties
    public virtual OwnershipGroup Group { get; set; } = null!;
    public virtual User Initiator { get; set; } = null!;
    public virtual User? Approver { get; set; }
}

public enum FundTransactionType
{
    Deposit = 0,
    Withdrawal = 1,
    Allocation = 2, // Move to reserve
    Release = 3, // Release from reserve
    ExpensePayment = 4
}

public enum FundTransactionStatus
{
    Pending = 0,
    Approved = 1,
    Completed = 2,
    Rejected = 3
}

