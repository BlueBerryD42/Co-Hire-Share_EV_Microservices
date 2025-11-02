using System;
using System.Collections.Generic;

namespace CoOwnershipVehicle.Vehicle.Api.DTOs
{
    /// <summary>
    /// Response from Payment Service for vehicle expenses
    /// </summary>
    public class VehicleExpensesResponse
    {
        public Guid VehicleId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<ExpenseDto> Expenses { get; set; } = new();
        public decimal TotalAmount { get; set; }
    }

    /// <summary>
    /// Individual expense from Payment Service
    /// </summary>
    public class ExpenseDto
    {
        public Guid Id { get; set; }
        public Guid VehicleId { get; set; }
        public string ExpenseType { get; set; } = string.Empty; // Maintenance, Insurance, etc.
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime ExpenseDate { get; set; }
        public string PaymentStatus { get; set; } = string.Empty; // Paid, Pending, etc.
        public Guid? PaidBy { get; set; } // User ID
        public string Category { get; set; } = string.Empty;
    }

    /// <summary>
    /// Budget information from Payment Service (if available)
    /// </summary>
    public class VehicleBudgetResponse
    {
        public Guid VehicleId { get; set; }
        public decimal MonthlyBudget { get; set; }
        public bool HasBudget { get; set; }
    }
}
