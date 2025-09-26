using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CoOwnershipVehicle.Domain.Entities;

public class GroupMember : BaseEntity
{
    public Guid GroupId { get; set; }
    
    public Guid UserId { get; set; }
    
    [Column(TypeName = "decimal(5,4)")]
    [Range(0.0001, 1.0000)]
    public decimal SharePercentage { get; set; }
    
    public GroupRole RoleInGroup { get; set; } = GroupRole.Member;
    
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public virtual OwnershipGroup Group { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}

public enum GroupRole
{
    Member = 0,
    Admin = 1
}
