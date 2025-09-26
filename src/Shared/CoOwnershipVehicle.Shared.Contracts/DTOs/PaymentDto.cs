using System.ComponentModel.DataAnnotations;

namespace CoOwnershipVehicle.Shared.Contracts.DTOs;

public class ExpenseDto
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public Guid? VehicleId { get; set; }
    public string? VehicleModel { get; set; }
    public ExpenseType ExpenseType { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime DateIncurred { get; set; }
    public Guid CreatedBy { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool IsRecurring { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateExpenseDto
{
    [Required]
    public Guid GroupId { get; set; }
    
    public Guid? VehicleId { get; set; }
    
    [Required]
    public ExpenseType ExpenseType { get; set; }
    
    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }
    
    [Required]
    public string Description { get; set; } = string.Empty;
    
    [Required]
    public DateTime DateIncurred { get; set; }
    
    public string? Notes { get; set; }
    public bool IsRecurring { get; set; }
}

public class InvoiceDto
{
    public Guid Id { get; set; }
    public Guid ExpenseId { get; set; }
    public ExpenseDto Expense { get; set; } = null!;
    public Guid PayerId { get; set; }
    public string PayerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public InvoiceStatus Status { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PaymentDto
{
    public Guid Id { get; set; }
    public Guid InvoiceId { get; set; }
    public Guid PayerId { get; set; }
    public string PayerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; }
    public string? TransactionReference { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreatePaymentDto
{
    [Required]
    public Guid InvoiceId { get; set; }
    
    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }
    
    [Required]
    public PaymentMethod Method { get; set; }
    
    public string? Notes { get; set; }
}

public class CostSplitDto
{
    public Guid ExpenseId { get; set; }
    public decimal TotalAmount { get; set; }
    public List<GroupMemberCostDto> MemberCosts { get; set; } = new();
}

public class GroupMemberCostDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public decimal SharePercentage { get; set; }
    public decimal AmountOwed { get; set; }
}

public enum ExpenseType
{
    Fuel = 0,
    Maintenance = 1,
    Insurance = 2,
    Registration = 3,
    Cleaning = 4,
    Repair = 5,
    Upgrade = 6,
    Parking = 7,
    Toll = 8,
    Other = 9
}

public enum InvoiceStatus
{
    Pending = 0,
    Sent = 1,
    Paid = 2,
    Overdue = 3,
    Cancelled = 4
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
