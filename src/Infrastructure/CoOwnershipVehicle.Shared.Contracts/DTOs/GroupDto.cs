using System.ComponentModel.DataAnnotations;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Shared.Contracts.DTOs;

public class GroupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public GroupStatus Status { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<GroupMemberDto> Members { get; set; } = new();
    public List<VehicleDto> Vehicles { get; set; } = new();
}

public class CreateGroupDto
{
    [Required]
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<CreateGroupMemberDto> Members { get; set; } = new();
}

public class UpdateGroupSharesDto
{
    [Required]
    public List<UpdateGroupMemberShareDto> Members { get; set; } = new();
}

public class GroupMemberDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserFirstName { get; set; } = string.Empty;
    public string UserLastName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public decimal SharePercentage { get; set; }
    public GroupRole RoleInGroup { get; set; }
    public DateTime JoinedAt { get; set; }
}

public class CreateGroupMemberDto
{
    [Required]
    public Guid UserId { get; set; }
    
    [Range(0.0001, 1.0000)]
    public decimal SharePercentage { get; set; }
    
    public GroupRole RoleInGroup { get; set; } = GroupRole.Member;
}

public class UpdateGroupMemberShareDto
{
    [Required]
    public Guid UserId { get; set; }
    
    [Range(0.0001, 1.0000)]
    public decimal SharePercentage { get; set; }
}

