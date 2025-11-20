using System.ComponentModel.DataAnnotations;

namespace CoOwnershipVehicle.Domain.Entities;

public class OwnershipGroup : BaseEntity
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(1000)]
    public string? Description { get; set; }
    
    public GroupStatus Status { get; set; } = GroupStatus.PendingApproval;
    
    public Guid CreatedBy { get; set; }
    
    [StringLength(1000)]
    public string? RejectionReason { get; set; }
    
    public DateTime? SubmittedAt { get; set; }
    
    public Guid? ReviewedBy { get; set; }
    
    public DateTime? ReviewedAt { get; set; }
    
    // Navigation properties
    public virtual User Creator { get; set; } = null!;
    public virtual ICollection<GroupMember> Members { get; set; } = new List<GroupMember>();
    public virtual ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public virtual ICollection<Expense> Expenses { get; set; } = new List<Expense>();
    public virtual ICollection<Document> Documents { get; set; } = new List<Document>();
    public virtual ICollection<Proposal> Proposals { get; set; } = new List<Proposal>();
    public virtual ICollection<LedgerEntry> LedgerEntries { get; set; } = new List<LedgerEntry>();
    public virtual ICollection<RecurringBooking> RecurringBookings { get; set; } = new List<RecurringBooking>();
    public virtual GroupFund? Fund { get; set; }
    public virtual ICollection<FundTransaction> FundTransactions { get; set; } = new List<FundTransaction>();
}

public enum GroupStatus
{
    PendingApproval = 0,
    Active = 1,
    Inactive = 2,
    Dissolved = 3,
    Rejected = 4
}
