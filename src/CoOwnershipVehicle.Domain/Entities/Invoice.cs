using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoOwnershipVehicle.Domain.Entities;

public class Invoice : BaseEntity
{
    public Guid ExpenseId { get; set; }
    
    public Guid PayerId { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }
    
    [Required]
    [StringLength(50)]
    public string InvoiceNumber { get; set; } = string.Empty;
    
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Pending;
    
    public DateTime DueDate { get; set; }
    
    public DateTime? PaidAt { get; set; }
    
    // Navigation properties
    public virtual Expense Expense { get; set; } = null!;
    public virtual User Payer { get; set; } = null!;
    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

public enum InvoiceStatus
{
    Pending = 0,
    Sent = 1,
    Paid = 2,
    Overdue = 3,
    Cancelled = 4
}
