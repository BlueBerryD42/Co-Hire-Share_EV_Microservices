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
            _logger.LogInformation("GetUserGroups called for UserId: {UserId}", userId);
            
            // Note: Vehicles are not included because Vehicle entity is in Vehicle service
            // Vehicles will be fetched separately via HTTP if needed
            // Users see only Active groups, except their own pending/rejected groups
            var groups = await _context.OwnershipGroups
                .Include(g => g.Members)
                .Where(g => g.Members.Any(m => m.UserId == userId) &&
                    (g.Status == Domain.Entities.GroupStatus.Active ||
                     g.Status == Domain.Entities.GroupStatus.PendingApproval ||
                     g.Status == Domain.Entities.GroupStatus.Rejected))
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
                RejectionReason = g.RejectionReason,
                SubmittedAt = g.SubmittedAt,
                ReviewedBy = g.ReviewedBy,
                ReviewedAt = g.ReviewedAt,
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
                // Vehicles are managed by Vehicle service - return empty list or fetch via HTTP if needed
                Vehicles = new List<VehicleDto>()
            }).ToList();

            return Ok(groupDtos);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt to get user groups");
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user groups: {Message}", ex.Message);
            return StatusCode(500, new { message = "An error occurred while retrieving groups", details = ex.Message });
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
            _logger.LogInformation("GetGroup endpoint called - Method: {Method}, Path: {Path}, ID: {Id}", 
                HttpContext.Request.Method, HttpContext.Request.Path, id);
            
            var userId = GetCurrentUserId();
            
            // First check if group exists
            var groupExists = await _context.OwnershipGroups.AnyAsync(g => g.Id == id);
            if (!groupExists)
            {
                _logger.LogWarning("Group {GroupId} does not exist", id);
                return NotFound(new { message = "Group not found" });
            }

            // Check if user is a member
            var isMember = await _context.GroupMembers.AnyAsync(m => m.GroupId == id && m.UserId == userId);
            if (!isMember)
            {
                _logger.LogWarning("User {UserId} is not a member of group {GroupId}", userId, id);
                // Log all members for debugging
                var allMembers = await _context.GroupMembers
                    .Where(m => m.GroupId == id)
                    .Select(m => m.UserId)
                    .ToListAsync();
                _logger.LogInformation("Group {GroupId} has {Count} members: {MemberIds}", 
                    id, allMembers.Count, string.Join(", ", allMembers));
                return NotFound(new { message = "Group not found or access denied" });
            }
            
            // Note: Vehicles are not included because Vehicle entity is in Vehicle service
            var group = await _context.OwnershipGroups
                .Include(g => g.Members)
                .Where(g => g.Id == id)
                .FirstOrDefaultAsync();

            if (group == null)
            {
                _logger.LogWarning("Group {GroupId} not found after membership check", id);
                return NotFound(new { message = "Group not found or access denied" });
            }

            // Fetch user data via HTTP
            var accessToken = GetAccessToken();
            var memberUserIds = group.Members.Select(m => m.UserId).Distinct().ToList();
            _logger.LogInformation("Fetching user data for {Count} members: {UserIds}", 
                memberUserIds.Count, string.Join(", ", memberUserIds));
            var users = await _userServiceClient.GetUsersAsync(memberUserIds, accessToken);
            _logger.LogInformation("Retrieved {Count} users from User service. User IDs: {UserIds}", 
                users.Count, string.Join(", ", users.Keys));

            var groupDto = new GroupDto
            {
                Id = group.Id,
                Name = group.Name,
                Description = group.Description,
                Status = (GroupStatus)group.Status,
                CreatedBy = group.CreatedBy,
                CreatedAt = group.CreatedAt,
                RejectionReason = group.RejectionReason,
                SubmittedAt = group.SubmittedAt,
                ReviewedBy = group.ReviewedBy,
                ReviewedAt = group.ReviewedAt,
                Members = group.Members.Select(m =>
                {
                    var user = users.GetValueOrDefault(m.UserId);
                    if (user == null)
                    {
                        _logger.LogWarning("User {UserId} not found in User service for group {GroupId}", m.UserId, group.Id);
                    }
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
                // Vehicles are managed by Vehicle service - return empty list or fetch via HTTP if needed
                Vehicles = new List<VehicleDto>()
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
    /// Get all groups (admin/staff only)
    /// </summary>
    [HttpGet("all")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    public async Task<IActionResult> GetAllGroups([FromQuery] GroupListRequestDto? request = null)
    {
        try
        {
            var userId = GetCurrentUserId();
            var userClaims = HttpContext.User.Claims.Select(c => $"{c.Type}={c.Value}").ToList();
            var userRoles = HttpContext.User.Claims
                .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role || c.Type == "role")
                .Select(c => c.Value)
                .ToList();
            
            _logger.LogInformation("GetAllGroups called. UserId: {UserId}, Request: {@Request}", userId, request);
            _logger.LogInformation("User claims: {Claims}, Roles: {Roles}", 
                string.Join(", ", userClaims), 
                string.Join(", ", userRoles));
            
            // Check if user has required role
            var hasSystemAdmin = HttpContext.User.IsInRole("SystemAdmin");
            var hasStaff = HttpContext.User.IsInRole("Staff");
            _logger.LogInformation("User role check - IsInRole(SystemAdmin): {HasSystemAdmin}, IsInRole(Staff): {HasStaff}", 
                hasSystemAdmin, hasStaff);
            
            var query = _context.OwnershipGroups
                .Include(g => g.Members)
                .AsQueryable();

            // Apply search filter
            if (request != null && !string.IsNullOrEmpty(request.Search))
            {
                var searchTerm = request.Search.ToLower();
                query = query.Where(g => 
                    (g.Name != null && g.Name.ToLower().Contains(searchTerm)) ||
                    (g.Description != null && g.Description.ToLower().Contains(searchTerm)));
            }

            // Apply status filter
            if (request != null && request.Status.HasValue)
            {
                query = query.Where(g => g.Status == (Domain.Entities.GroupStatus)request.Status.Value);
            }

            var groups = await query.ToListAsync();
            _logger.LogInformation("Found {Count} groups from database", groups.Count);

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
                RejectionReason = g.RejectionReason,
                SubmittedAt = g.SubmittedAt,
                ReviewedBy = g.ReviewedBy,
                ReviewedAt = g.ReviewedAt,
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
                Vehicles = new List<VehicleDto>()
            }).ToList();

            _logger.LogInformation("Returning {Count} group DTOs", groupDtos.Count);
            return Ok(groupDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all groups");
            return StatusCode(500, new { message = "An error occurred while retrieving groups" });
        }
    }

    /// <summary>
    /// Update group status (admin/staff only)
    /// </summary>
    [HttpPut("{id:guid}/status")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    public async Task<IActionResult> UpdateGroupStatus(Guid id, [FromBody] UpdateGroupStatusDto request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var group = await _context.OwnershipGroups.FindAsync(id);
            if (group == null)
                return NotFound(new { message = "Group not found" });

            group.Status = (Domain.Entities.GroupStatus)request.Status;
            group.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Group {GroupId} status updated to {Status}", id, request.Status);

            return Ok(new { message = "Group status updated successfully", groupId = id, status = request.Status });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating group status for {GroupId}", id);
            return StatusCode(500, new { message = "An error occurred while updating group status" });
        }
    }

    /// <summary>
    /// Create a new group
    /// </summary>
    [HttpPost]
    [Route("")]
    public async Task<IActionResult> CreateGroup([FromBody] CreateGroupDto createDto)
    {
        try
        {
            _logger.LogInformation("CreateGroup endpoint called - Method: {Method}, Path: {Path}", 
                HttpContext.Request.Method, HttpContext.Request.Path);
            
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("ModelState is invalid: {Errors}", 
                    string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                return BadRequest(ModelState);
            }

            var userId = GetCurrentUserId();
            _logger.LogInformation("Creating group for user {UserId}", userId);

            // Validate share percentages sum to 100%
            var totalShares = createDto.Members.Sum(m => m.SharePercentage);
            if (Math.Abs(totalShares - 1.0m) > 0.0001m)
            {
                return BadRequest(new { message = "Total share percentages must equal 100%" });
            }

            // Create the group with PendingApproval status
            var group = new Domain.Entities.OwnershipGroup
            {
                Id = Guid.NewGuid(),
                Name = createDto.Name,
                Description = createDto.Description,
                Status = Domain.Entities.GroupStatus.PendingApproval,
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                SubmittedAt = DateTime.UtcNow
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

            // Fetch user data for response
            var accessToken = GetAccessToken();
            var memberUserIds = members.Select(m => m.UserId).Distinct().ToList();
            var users = await _userServiceClient.GetUsersAsync(memberUserIds, accessToken);

            // Return the created group (don't call GetGroup action method to avoid routing issues)
            var groupDto = new GroupDto
            {
                Id = group.Id,
                Name = group.Name,
                Description = group.Description,
                Status = (GroupStatus)group.Status,
                CreatedBy = group.CreatedBy,
                CreatedAt = group.CreatedAt,
                RejectionReason = group.RejectionReason,
                SubmittedAt = group.SubmittedAt,
                ReviewedBy = group.ReviewedBy,
                ReviewedAt = group.ReviewedAt,
                Members = members.Select(m =>
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
                Vehicles = new List<VehicleDto>()
            };

            return Ok(groupDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating group");
            return StatusCode(500, new { message = "An error occurred while creating group" });
        }
    }

    /// <summary>
    /// Get pending groups (Staff/Admin only)
    /// </summary>
    [HttpGet("pending")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    public async Task<IActionResult> GetPendingGroups()
    {
        try
        {
            // Debug logging for authorization
            var userId = GetCurrentUserId();
            var userRoles = User.Claims.Where(c => c.Type == "role" || c.Type == System.Security.Claims.ClaimTypes.Role)
                .Select(c => c.Value).ToList();
            _logger.LogInformation("GetPendingGroups called by user {UserId}. User roles: {Roles}", userId, string.Join(", ", userRoles));
            _logger.LogInformation("User.IsInRole('Staff'): {IsStaff}, User.IsInRole('SystemAdmin'): {IsAdmin}", 
                User.IsInRole("Staff"), User.IsInRole("SystemAdmin"));
            
            var groups = await _context.OwnershipGroups
                .Include(g => g.Members)
                .Where(g => g.Status == Domain.Entities.GroupStatus.PendingApproval)
                .OrderBy(g => g.SubmittedAt ?? g.CreatedAt)
                .ToListAsync();

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
                RejectionReason = g.RejectionReason,
                SubmittedAt = g.SubmittedAt,
                ReviewedBy = g.ReviewedBy,
                ReviewedAt = g.ReviewedAt,
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
                Vehicles = new List<VehicleDto>()
            }).ToList();

            return Ok(groupDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending groups");
            return StatusCode(500, new { message = "An error occurred while retrieving pending groups" });
        }
    }

    /// <summary>
    /// Approve a group (Staff/Admin only)
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    public async Task<IActionResult> ApproveGroup(Guid id, [FromBody] ApproveGroupDto? request = null)
    {
        try
        {
            var reviewerId = GetCurrentUserId();
            var group = await _context.OwnershipGroups
                .Include(g => g.Members)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (group == null)
                return NotFound(new { message = "Group not found" });

            if (group.Status != Domain.Entities.GroupStatus.PendingApproval)
                return BadRequest(new { message = "Group is not pending approval" });

            // Validate group can be approved
            var totalShares = group.Members.Sum(m => m.SharePercentage);
            if (Math.Abs(totalShares - 1.0m) > 0.0001m)
            {
                return BadRequest(new { message = "Cannot approve group: ownership percentages do not total 100%" });
            }

            var hasGroupAdmin = group.Members.Any(m => m.RoleInGroup == Domain.Entities.GroupRole.Admin);
            if (!hasGroupAdmin)
            {
                return BadRequest(new { message = "Cannot approve group: must have at least one GroupAdmin member" });
            }

            // Approve the group
            group.Status = Domain.Entities.GroupStatus.Active;
            group.ReviewedBy = reviewerId;
            group.ReviewedAt = DateTime.UtcNow;
            group.UpdatedAt = DateTime.UtcNow;
            group.RejectionReason = null; // Clear any previous rejection reason

            await _context.SaveChangesAsync();

            _logger.LogInformation("Group {GroupId} approved by user {ReviewerId}", id, reviewerId);

            return Ok(new { message = "Group approved successfully", groupId = id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving group {GroupId}", id);
            return StatusCode(500, new { message = "An error occurred while approving group" });
        }
    }

    /// <summary>
    /// Reject a group (Staff/Admin only)
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    public async Task<IActionResult> RejectGroup(Guid id, [FromBody] RejectGroupDto request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var reviewerId = GetCurrentUserId();
            var group = await _context.OwnershipGroups.FindAsync(id);

            if (group == null)
                return NotFound(new { message = "Group not found" });

            if (group.Status != Domain.Entities.GroupStatus.PendingApproval)
                return BadRequest(new { message = "Group is not pending approval" });

            // Reject the group
            group.Status = Domain.Entities.GroupStatus.Rejected;
            group.ReviewedBy = reviewerId;
            group.ReviewedAt = DateTime.UtcNow;
            group.UpdatedAt = DateTime.UtcNow;
            group.RejectionReason = request.Reason;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Group {GroupId} rejected by user {ReviewerId}", id, reviewerId);

            return Ok(new { message = "Group rejected successfully", groupId = id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting group {GroupId}", id);
            return StatusCode(500, new { message = "An error occurred while rejecting group" });
        }
    }

    /// <summary>
    /// Resubmit a rejected group
    /// </summary>
    [HttpPut("{id:guid}/resubmit")]
    public async Task<IActionResult> ResubmitGroup(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var group = await _context.OwnershipGroups.FindAsync(id);

            if (group == null)
                return NotFound(new { message = "Group not found" });

            if (group.CreatedBy != userId)
                return Forbid("Only the group creator can resubmit");

            if (group.Status != Domain.Entities.GroupStatus.Rejected)
                return BadRequest(new { message = "Group is not rejected" });

            // Resubmit the group
            group.Status = Domain.Entities.GroupStatus.PendingApproval;
            group.SubmittedAt = DateTime.UtcNow;
            group.ReviewedBy = null;
            group.ReviewedAt = null;
            group.RejectionReason = null;
            group.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Group {GroupId} resubmitted by user {UserId}", id, userId);

            return Ok(new { message = "Group resubmitted successfully", groupId = id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resubmitting group {GroupId}", id);
            return StatusCode(500, new { message = "An error occurred while resubmitting group" });
        }
    }

    /// <summary>
    /// Browse/search groups for marketplace (public groups only - Active status)
    /// </summary>
    [HttpGet("browse")]
    [AllowAnonymous] // Allow unauthenticated access for marketplace browsing
    public async Task<IActionResult> BrowseGroups([FromQuery] BrowseGroupsRequestDto request)
    {
        try
        {
            _logger.LogInformation("BrowseGroups called with filters: Search={Search}, Location={Location}, VehicleType={VehicleType}, Availability={Availability}, Page={Page}, PageSize={PageSize}",
                request.Search, request.Location, request.VehicleType, request.Availability, request.Page, request.PageSize);

            // Only show Active groups in marketplace
            var query = _context.OwnershipGroups
                .Include(g => g.Members)
                .Where(g => g.Status == Domain.Entities.GroupStatus.Active)
                .AsQueryable();

            // Search filter (by group name or description)
            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var searchLower = request.Search.ToLower();
                query = query.Where(g => 
                    g.Name.ToLower().Contains(searchLower) || 
                    (g.Description != null && g.Description.ToLower().Contains(searchLower)));
            }

            // Location filter (if location field exists in future)
            // For now, skip location filter as it's not in the Group entity

            // Vehicle type filter (would need to join with Vehicle service - skip for now)
            // For now, skip vehicle type filter

            // Member count filter
            if (request.MinMembers.HasValue)
            {
                query = query.Where(g => g.Members.Count >= request.MinMembers.Value);
            }
            if (request.MaxMembers.HasValue)
            {
                query = query.Where(g => g.Members.Count <= request.MaxMembers.Value);
            }

            // Calculate total count before pagination
            var totalCount = await query.CountAsync();

            // Calculate available ownership percentage and filter by availability
            // Note: SharePercentage is stored as decimal (0-1), need to convert to percentage (0-100)
            // Hardcoded max 10 members for now - each member can own up to 10% (100% / 10 members)
            const int MAX_MEMBERS = 10;
            const decimal MAX_OWNERSHIP_PERCENTAGE = 100m; // 100% total ownership
            
            var groupsWithOwnership = await query
                .Select(g => new
                {
                    Group = g,
                    TotalOwnership = g.Members.Sum(m => m.SharePercentage) * 100m, // Convert to percentage
                    MemberCount = g.Members.Count
                })
                .ToListAsync();

            // Filter by availability
            // Available ownership = MAX_OWNERSHIP_PERCENTAGE - current ownership (based on max 10 members)
            var filteredGroups = groupsWithOwnership
                .Select(x => new
                {
                    x.Group,
                    x.TotalOwnership,
                    x.MemberCount,
                    AvailableOwnership = Math.Max(0, MAX_OWNERSHIP_PERCENTAGE - x.TotalOwnership)
                })
                .Where(x =>
                {
                    if (request.Availability == "Open")
                        return x.AvailableOwnership > 0;
                    if (request.Availability == "Full")
                        return x.AvailableOwnership <= 0;
                    return true; // "Any" or null
                })
                .ToList();

            // Apply sorting
            var sortedGroups = request.SortBy?.ToLower() switch
            {
                "name" => request.SortDescending
                    ? filteredGroups.OrderByDescending(x => x.Group.Name).ToList()
                    : filteredGroups.OrderBy(x => x.Group.Name).ToList(),
                "members" => request.SortDescending
                    ? filteredGroups.OrderByDescending(x => x.MemberCount).ToList()
                    : filteredGroups.OrderBy(x => x.MemberCount).ToList(),
                _ => request.SortDescending
                    ? filteredGroups.OrderByDescending(x => x.MemberCount).ToList()
                    : filteredGroups.OrderBy(x => x.MemberCount).ToList()
            };

            // Apply pagination
            var page = Math.Max(1, request.Page);
            var pageSize = Math.Clamp(request.PageSize, 1, 100);
            var paginatedGroups = sortedGroups
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // Map to MarketplaceGroupDto
            var marketplaceGroups = paginatedGroups.Select(x =>
            {
                var group = x.Group;
                var availableOwnership = x.AvailableOwnership;
                var totalOwnership = x.TotalOwnership;

                _logger.LogInformation("Mapping group {GroupId}: CurrentMembers={CurrentMembers}, TotalMembers={TotalMembers}", 
                    group.Id, x.MemberCount, MAX_MEMBERS);

                return new MarketplaceGroupDto
                {
                    GroupId = group.Id,
                    GroupName = group.Name,
                    Description = group.Description,
                    Status = group.Status.ToString(),
                    VehicleId = null, // Vehicle info would need to be fetched from Vehicle service
                    VehiclePhoto = null,
                    VehicleMake = null,
                    VehicleModel = null,
                    VehicleYear = null,
                    VehiclePlateNumber = null,
                    Location = null, // Location not available in current Group entity
                    TotalOwnershipPercentage = totalOwnership,
                    AvailableOwnershipPercentage = availableOwnership,
                    TotalMembers = MAX_MEMBERS, // Hardcoded max 10 members
                    CurrentMembers = x.MemberCount,
                    MonthlyEstimatedCost = null, // Would need to calculate from expenses/analytics
                    UtilizationRate = null,
                    ParticipationRate = null,
                    TotalBookings = null,
                    TotalVehicles = null
                };
            }).ToList();

            var response = new BrowseGroupsResponseDto
            {
                Groups = marketplaceGroups,
                TotalCount = filteredGroups.Count,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)filteredGroups.Count / pageSize)
            };

            _logger.LogInformation("BrowseGroups returned {Count} groups (page {Page} of {TotalPages})", 
                marketplaceGroups.Count, page, response.TotalPages);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error browsing groups: {Message}", ex.Message);
            return StatusCode(500, new { message = "An error occurred while browsing groups", details = ex.Message });
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
