using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using CoOwnershipVehicle.Group.Api.Data;
using CoOwnershipVehicle.Group.Api.Services.Interfaces;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Shared.Contracts.Events;
using CoOwnershipVehicle.Domain.Entities;
using MassTransit;

namespace CoOwnershipVehicle.Group.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GroupController : ControllerBase
{
    private readonly GroupDbContext _context;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<GroupController> _logger;
    private readonly IUserServiceClient _userServiceClient;

    public GroupController(
        GroupDbContext context,
        IPublishEndpoint publishEndpoint,
        ILogger<GroupController> logger,
        IUserServiceClient userServiceClient)
    {
        _context = context;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
        _userServiceClient = userServiceClient;
    }

    private string GetAccessToken()
    {
        var authHeader = HttpContext.Request.Headers["Authorization"].ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            return string.Empty;
        }
        return authHeader.Substring("Bearer ".Length).Trim();
    }

    /// <summary>
    /// Get all groups for current user
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetUserGroups()
    {
        try
        {
            var userId = GetCurrentUserId();
            _logger.LogInformation("GetUserGroups called for UserId: {UserId}", userId); // Added log
            
            var groups = await _context.OwnershipGroups
                .Include(g => g.Members)
                .Include(g => g.Vehicles)
                .Where(g => g.Members.Any(m => m.UserId == userId))
                .ToListAsync();

            // Fetch user data via HTTP
            var accessToken = GetAccessToken();
            var allMemberUserIds = groups.SelectMany(g => g.Members.Select(m => m.UserId)).Distinct().ToList();
            var users = await _userServiceClient.GetUsersAsync(allMemberUserIds, accessToken);

            var groupDtos = groups.Select(g => new GroupDto
            {
                Id = g.Id,
                Name = g.Name,
                Description = g.Description,
                Status = (GroupStatus)g.Status,
                CreatedBy = g.CreatedBy,
                CreatedAt = g.CreatedAt,
                Members = g.Members.Select(m =>
                {
                    var user = users.GetValueOrDefault(m.UserId);
                    return new GroupMemberDto
                    {
                        Id = m.Id,
                        UserId = m.UserId,
                        UserFirstName = user?.FirstName ?? "Unknown",
                        UserLastName = user?.LastName ?? "",
                        UserEmail = user?.Email ?? "",
                        SharePercentage = m.SharePercentage,
                        RoleInGroup = (GroupRole)m.RoleInGroup,
                        JoinedAt = m.JoinedAt
                    };
                }).ToList(),
                Vehicles = g.Vehicles.Select(v => new VehicleDto
                {
                    Id = v.Id,
                    Vin = v.Vin,
                    PlateNumber = v.PlateNumber,
                    Model = v.Model,
                    Year = v.Year,
                    Color = v.Color,
                    Status = (VehicleStatus)v.Status,
                    Odometer = v.Odometer,
                    GroupId = v.GroupId,
                    GroupName = g.Name
                }).ToList()
            }).ToList();

            return Ok(groupDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user groups");
            return StatusCode(500, new { message = "An error occurred while retrieving groups" });
        }
    }

    /// <summary>
    /// Get group by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetGroup(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            
            var group = await _context.OwnershipGroups
                .Include(g => g.Members)
                .Include(g => g.Vehicles)
                .Where(g => g.Id == id && g.Members.Any(m => m.UserId == userId))
                .FirstOrDefaultAsync();

            if (group == null)
                return NotFound(new { message = "Group not found or access denied" });

            // Fetch user data via HTTP
            var accessToken = GetAccessToken();
            var memberUserIds = group.Members.Select(m => m.UserId).Distinct().ToList();
            var users = await _userServiceClient.GetUsersAsync(memberUserIds, accessToken);

            var groupDto = new GroupDto
            {
                Id = group.Id,
                Name = group.Name,
                Description = group.Description,
                Status = (GroupStatus)group.Status,
                CreatedBy = group.CreatedBy,
                CreatedAt = group.CreatedAt,
                Members = group.Members.Select(m =>
                {
                    var user = users.GetValueOrDefault(m.UserId);
                    return new GroupMemberDto
                    {
                        Id = m.Id,
                        UserId = m.UserId,
                        UserFirstName = user?.FirstName ?? "Unknown",
                        UserLastName = user?.LastName ?? "",
                        UserEmail = user?.Email ?? "",
                        SharePercentage = m.SharePercentage,
                        RoleInGroup = (GroupRole)m.RoleInGroup,
                        JoinedAt = m.JoinedAt
                    };
                }).ToList(),
                Vehicles = group.Vehicles.Select(v => new VehicleDto
                {
                    Id = v.Id,
                    Vin = v.Vin,
                    PlateNumber = v.PlateNumber,
                    Model = v.Model,
                    Year = v.Year,
                    Color = v.Color,
                    Status = (VehicleStatus)v.Status,
                    Odometer = v.Odometer,
                    GroupId = v.GroupId,
                    GroupName = group.Name
                }).ToList()
            };

            return Ok(groupDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting group {GroupId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving group" });
        }
    }

    /// <summary>
    /// Get group details with members (for inter-service communication)
    /// </summary>
    [HttpGet("{id:guid}/details")]
    public async Task<IActionResult> GetGroupDetails(Guid id)
    {
        try
        {
            _logger.LogInformation("GetGroupDetails called for GroupId: {GroupId}", id);

            var group = await _context.OwnershipGroups
                .Include(g => g.Members)  // Only load Members, NOT User
                .Where(g => g.Id == id)
                .FirstOrDefaultAsync();

            if (group == null)
            {
                _logger.LogWarning("Group {GroupId} not found", id);
                return NotFound(new { message = "Group not found" });
            }

            _logger.LogInformation("Group {GroupId} found: {GroupName}. Members collection count: {MemberCount}",
                id, group.Name, group.Members?.Count ?? 0);

            // Check if Members is null or empty
            if (group.Members == null)
            {
                _logger.LogWarning("Group {GroupId} Members collection is NULL", id);
            }
            else if (!group.Members.Any())
            {
                _logger.LogWarning("Group {GroupId} Members collection is EMPTY (Count = 0)", id);
            }
            else
            {
                _logger.LogInformation("Group {GroupId} has {Count} members. Member IDs: {MemberIds}",
                    id, group.Members.Count, string.Join(", ", group.Members.Select(m => m.UserId)));
            }

            // For microservices: Return only UserId, let the caller get user details from Auth Service
            var response = new
            {
                GroupId = group.Id,
                GroupName = group.Name,
                Members = group.Members.Select(m => new
                {
                    UserId = m.UserId,
                    OwnershipPercentage = m.SharePercentage * 100, // Convert to percentage
                    Role = m.RoleInGroup.ToString()
                }).ToList()
            };

            _logger.LogInformation("Returning response with {Count} members", response.Members.Count);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting group details {GroupId}", id);
            return StatusCode(500, new { message = "An error occurred while retrieving group details" });
        }
    }

    /// <summary>
    /// Create a new group
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateGroup([FromBody] CreateGroupDto createDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetCurrentUserId();

            // Validate share percentages sum to 100%
            var totalShares = createDto.Members.Sum(m => m.SharePercentage);
            if (Math.Abs(totalShares - 1.0m) > 0.0001m)
            {
                return BadRequest(new { message = "Total share percentages must equal 100%" });
            }

            // Create the group
            var group = new Domain.Entities.OwnershipGroup
            {
                Id = Guid.NewGuid(),
                Name = createDto.Name,
                Description = createDto.Description,
                Status = Domain.Entities.GroupStatus.Active,
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.OwnershipGroups.Add(group);
            await _context.SaveChangesAsync();

            // Add members
            var members = new List<Domain.Entities.GroupMember>();
            foreach (var memberDto in createDto.Members)
            {
                var member = new Domain.Entities.GroupMember
                {
                    Id = Guid.NewGuid(),
                    GroupId = group.Id,
                    UserId = memberDto.UserId,
                    SharePercentage = memberDto.SharePercentage,
                    RoleInGroup = (Domain.Entities.GroupRole)memberDto.RoleInGroup,
                    JoinedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                members.Add(member);
            }

            _context.GroupMembers.AddRange(members);
            await _context.SaveChangesAsync();

            // Publish group created event
            await _publishEndpoint.Publish(new GroupCreatedEvent
            {
                GroupId = group.Id,
                GroupName = group.Name,
                CreatedBy = userId,
                Members = members.Select(m => new GroupMemberData
                {
                    UserId = m.UserId,
                    SharePercentage = m.SharePercentage,
                    Role = (GroupRole)m.RoleInGroup
                }).ToList()
            });

            _logger.LogInformation("Group {GroupId} created by user {UserId}", group.Id, userId);

            // Return the created group
            return await GetGroup(group.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating group");
            return StatusCode(500, new { message = "An error occurred while creating group" });
        }
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid user ID in token");
        }
        return userId;
    }
}
