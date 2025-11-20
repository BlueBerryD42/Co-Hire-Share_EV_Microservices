using System.ComponentModel.DataAnnotations;

namespace CoOwnershipVehicle.Domain.Entities;

public class Vehicle : BaseEntity
{
    [Required]
    [StringLength(17)]
    public string Vin { get; set; } = string.Empty;
    
    [Required]
    [StringLength(20)]
    public string PlateNumber { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100)]
    public string Model { get; set; } = string.Empty;
    
    public int Year { get; set; }
    
    [StringLength(50)]
    public string? Color { get; set; }
    
    public VehicleStatus Status { get; set; } = VehicleStatus.PendingApproval;
    
    public DateTime? LastServiceDate { get; set; }
    
    public int Odometer { get; set; }
    
    public Guid? GroupId { get; set; }
    
    [StringLength(1000)]
    public string? RejectionReason { get; set; }
    
    public DateTime? SubmittedAt { get; set; }
    
    public Guid? ReviewedBy { get; set; }
    
    public DateTime? ReviewedAt { get; set; }
    
    // Navigation properties
    public virtual OwnershipGroup? Group { get; set; }
    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public virtual ICollection<Expense> Expenses { get; set; } = new List<Expense>();
    public virtual ICollection<CheckIn> CheckIns { get; set; } = new List<CheckIn>();
    public virtual ICollection<MaintenanceRecord> MaintenanceRecords { get; set; } = new List<MaintenanceRecord>();
    public virtual ICollection<RecurringBooking> RecurringBookings { get; set; } = new List<RecurringBooking>();
}

public enum VehicleStatus
{
    PendingApproval = 0,
    Available = 1,
    InUse = 2,
    Maintenance = 3,
    Unavailable = 4,
    Rejected = 5
}
