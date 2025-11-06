using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoOwnershipVehicle.Domain.Entities;

public class Expense : BaseEntity
{
    public Guid GroupId { get; set; }
    
    public Guid? VehicleId { get; set; }
    
    public ExpenseType ExpenseType { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }
    
    [Required]
    [StringLength(200)]
    public string Description { get; set; } = string.Empty;
    
    public DateTime DateIncurred { get; set; }
    
    public Guid CreatedBy { get; set; }
    
    [StringLength(500)]
    public string? Notes { get; set; }
    
    public bool IsRecurring { get; set; } = false;
    
    // Navigation properties
    public virtual OwnershipGroup Group { get; set; } = null!;
    public virtual Vehicle? Vehicle { get; set; }
    public virtual User Creator { get; set; } = null!;
    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
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
