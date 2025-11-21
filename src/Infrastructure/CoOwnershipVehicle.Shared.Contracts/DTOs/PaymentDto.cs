using System.ComponentModel.DataAnnotations;
using CoOwnershipVehicle.Domain.Entities;

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
    public Guid? InvoiceId { get; set; } // Nullable to support fund deposits without invoices
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

public class CreateFundDepositPaymentDto
{
    [Required]
    public Guid GroupId { get; set; }
    
    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }
    
    [Required]
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;
    
    [StringLength(200)]
    public string? Reference { get; set; }
}

public class FundDepositPaymentResponse
{
    public string PaymentUrl { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public Guid PaymentId { get; set; }
    public Guid GroupId { get; set; }
}

public class CompleteFundDepositDto
{
    [Required]
    public Guid GroupId { get; set; }
    
    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }
    
    [Required]
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;
    
    [Required]
    public string PaymentReference { get; set; } = string.Empty;
    
    [Required]
    public Guid InitiatedBy { get; set; }
    
    [StringLength(200)]
    public string? Reference { get; set; }
}

