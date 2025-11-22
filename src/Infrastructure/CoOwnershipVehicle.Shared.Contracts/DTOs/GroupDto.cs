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
    public string? RejectionReason { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
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

public class ApproveGroupDto
{
    public string? Notes { get; set; }
}

public class RejectGroupDto
{
    [Required]
    [StringLength(1000)]
    public string Reason { get; set; } = string.Empty;
}

public class PendingGroupDto : GroupDto
{
    public int MemberCount { get; set; }
    public int PendingKycCount { get; set; }
    public decimal TotalOwnershipPercentage { get; set; }
    public bool HasGroupAdmin { get; set; }
}

/// <summary>
/// Request DTO for browsing/searching groups in marketplace
/// </summary>
public class BrowseGroupsRequestDto
{
    public string? Search { get; set; }
    public string? Location { get; set; }
    public string? VehicleType { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public int? MinMembers { get; set; }
    public int? MaxMembers { get; set; }
    public string? Availability { get; set; } // "Any", "Open", "Full"
    public string? SortBy { get; set; } // "members", "utilization", "price", "name"
    public bool SortDescending { get; set; } = true;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// Response DTO for marketplace group listing
/// </summary>
public class MarketplaceGroupDto
{
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    
    // Vehicle info
    public Guid? VehicleId { get; set; }
    public string? VehiclePhoto { get; set; }
    public string? VehicleMake { get; set; }
    public string? VehicleModel { get; set; }
    public int? VehicleYear { get; set; }
    public string? VehiclePlateNumber { get; set; }
    public string? Location { get; set; }
    
    // Ownership info
    public decimal TotalOwnershipPercentage { get; set; }
    public decimal AvailableOwnershipPercentage { get; set; }
    public int TotalMembers { get; set; }
    public int CurrentMembers { get; set; }
    
    // Cost info
    public decimal? MonthlyEstimatedCost { get; set; }
    
    // Analytics info (optional, can be fetched separately)
    public decimal? UtilizationRate { get; set; }
    public decimal? ParticipationRate { get; set; }
    public int? TotalBookings { get; set; }
    public int? TotalVehicles { get; set; }
}

/// <summary>
/// Paginated response for marketplace groups
/// </summary>
public class BrowseGroupsResponseDto
{
    public List<MarketplaceGroupDto> Groups { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

