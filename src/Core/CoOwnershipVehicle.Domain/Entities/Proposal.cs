using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoOwnershipVehicle.Domain.Entities;

public class Proposal : BaseEntity
{
    public Guid GroupId { get; set; }
    
    public Guid CreatedBy { get; set; }
    
    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;
    
    [Required]
    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;
    
    public ProposalType Type { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal? Amount { get; set; }
    
    public ProposalStatus Status { get; set; } = ProposalStatus.Active;
    
    public DateTime VotingStartDate { get; set; }
    
    public DateTime VotingEndDate { get; set; }
    
    [Column(TypeName = "decimal(5,4)")]
    public decimal RequiredMajority { get; set; } = 0.5m; // 50% by default
    
    // Navigation properties
    public virtual OwnershipGroup Group { get; set; } = null!;
    public virtual User Creator { get; set; } = null!;
    public virtual ICollection<Vote> Votes { get; set; } = new List<Vote>();
}

public class Vote : BaseEntity
{
    public Guid ProposalId { get; set; }
    
    public Guid VoterId { get; set; }
    
    [Column(TypeName = "decimal(5,4)")]
    public decimal Weight { get; set; } // Based on ownership share
    
    public VoteChoice Choice { get; set; }
    
    [StringLength(500)]
    public string? Comment { get; set; }
    
    public DateTime VotedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual Proposal Proposal { get; set; } = null!;
    public virtual User Voter { get; set; } = null!;
}

public enum ProposalType
{
    VehicleUpgrade = 0,
    VehicleSale = 1,
    MaintenanceBudget = 2,
    PolicyChange = 3,
    MembershipChange = 4,
    Other = 5
}

public enum ProposalStatus
{
    Active = 0,
    Passed = 1,
    Rejected = 2,
    Expired = 3,
    Cancelled = 4
}

public enum VoteChoice
{
    Yes = 0,
    No = 1,
    Abstain = 2
}
