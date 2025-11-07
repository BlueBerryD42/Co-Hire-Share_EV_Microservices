using System.Security.Claims;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Group.Api.Controllers;
using CoOwnershipVehicle.Group.Api.Data;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Shared.Contracts.Events;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using MassTransit;

namespace CoOwnershipVehicle.Group.Api.Tests;

public class GroupControllerTests : IDisposable
{
    private readonly GroupDbContext _context;
    private readonly Mock<IPublishEndpoint> _publishEndpointMock;
    private readonly Mock<ILogger<GroupController>> _loggerMock;
    private readonly GroupController _controller;
    private readonly Guid _testUserId;
    private readonly Guid _testGroupId;

    public GroupControllerTests()
    {
        var options = new DbContextOptionsBuilder<GroupDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new GroupDbContext(options);
        _publishEndpointMock = new Mock<IPublishEndpoint>();
        _loggerMock = new Mock<ILogger<GroupController>>();
        _testUserId = Guid.NewGuid();
        _testGroupId = Guid.NewGuid();

        _controller = new GroupController(_context, _publishEndpointMock.Object, _loggerMock.Object);
        
        // Setup user claims
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, _testUserId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = principal
            }
        };

        SeedTestData();
    }

    private void SeedTestData()
    {
        // Create test user
        var user = new User
        {
            Id = _testUserId,
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);

        // Create test group
        var group = new OwnershipGroup
        {
            Id = _testGroupId,
            Name = "Test Group",
            Description = "Test Description",
            Status = GroupStatus.Active,
            CreatedBy = _testUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.OwnershipGroups.Add(group);

        // Create group member
        var member = new GroupMember
        {
            Id = Guid.NewGuid(),
            GroupId = _testGroupId,
            UserId = _testUserId,
            SharePercentage = 1.0m,
            RoleInGroup = GroupRole.Admin,
            JoinedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.GroupMembers.Add(member);

        _context.SaveChanges();
    }

    [Fact]
    public async Task GetUserGroups_ShouldReturnGroupsForCurrentUser()
    {
        // Act
        var result = await _controller.GetUserGroups();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var groups = okResult!.Value as List<GroupDto>;
        groups.Should().NotBeNull();
        groups.Should().HaveCount(1);
        groups![0].Id.Should().Be(_testGroupId);
        groups[0].Name.Should().Be("Test Group");
        groups[0].Members.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetUserGroups_ShouldNotReturnGroupsForOtherUsers()
    {
        // Arrange - Create another user and group
        var otherUserId = Guid.NewGuid();
        var otherUser = new User
        {
            Id = otherUserId,
            Email = "other@example.com",
            FirstName = "Other",
            LastName = "User",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(otherUser);

        var otherGroup = new OwnershipGroup
        {
            Id = Guid.NewGuid(),
            Name = "Other Group",
            Status = GroupStatus.Active,
            CreatedBy = otherUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.OwnershipGroups.Add(otherGroup);

        var otherMember = new GroupMember
        {
            Id = Guid.NewGuid(),
            GroupId = otherGroup.Id,
            UserId = otherUserId,
            SharePercentage = 1.0m,
            RoleInGroup = GroupRole.Member,
            JoinedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.GroupMembers.Add(otherMember);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetUserGroups();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var groups = okResult!.Value as List<GroupDto>;
        groups.Should().HaveCount(1);
        groups![0].Id.Should().Be(_testGroupId);
        groups[0].Name.Should().Be("Test Group");
    }

    [Fact]
    public async Task GetGroup_ShouldReturnGroupDetails()
    {
        // Act
        var result = await _controller.GetGroup(_testGroupId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var group = okResult!.Value as GroupDto;
        group.Should().NotBeNull();
        group!.Id.Should().Be(_testGroupId);
        group.Name.Should().Be("Test Group");
        group.Description.Should().Be("Test Description");
        group.Members.Should().HaveCount(1);
        group.Members[0].UserId.Should().Be(_testUserId);
        group.Members[0].SharePercentage.Should().Be(1.0m);
    }

    [Fact]
    public async Task GetGroup_ShouldReturnNotFoundForNonExistentGroup()
    {
        // Act
        var result = await _controller.GetGroup(Guid.NewGuid());

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetGroup_ShouldReturnNotFoundForGroupUserNotMemberOf()
    {
        // Arrange - Create group with different user
        var otherUserId = Guid.NewGuid();
        var otherUser = new User
        {
            Id = otherUserId,
            Email = "other@example.com",
            FirstName = "Other",
            LastName = "User",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(otherUser);

        var otherGroup = new OwnershipGroup
        {
            Id = Guid.NewGuid(),
            Name = "Other Group",
            Status = GroupStatus.Active,
            CreatedBy = otherUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.OwnershipGroups.Add(otherGroup);

        var otherMember = new GroupMember
        {
            Id = Guid.NewGuid(),
            GroupId = otherGroup.Id,
            UserId = otherUserId,
            SharePercentage = 1.0m,
            RoleInGroup = GroupRole.Member,
            JoinedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.GroupMembers.Add(otherMember);
        await _context.SaveChangesAsync();

        // Act
        var result = await _controller.GetGroup(otherGroup.Id);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task CreateGroup_ShouldCreateGroupWithValidData()
    {
        // Arrange
        var createDto = new CreateGroupDto
        {
            Name = "New Group",
            Description = "New Description",
            Members = new List<CreateGroupMemberDto>
            {
                new CreateGroupMemberDto
                {
                    UserId = _testUserId,
                    SharePercentage = 1.0m,
                    RoleInGroup = GroupRole.Admin
                }
            }
        };

        // Act
        var result = await _controller.CreateGroup(createDto);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var group = okResult!.Value as GroupDto;
        group.Should().NotBeNull();
        group!.Name.Should().Be("New Group");
        group.Members.Should().HaveCount(1);
        group.Members[0].SharePercentage.Should().Be(1.0m);

        // Verify event was published
        _publishEndpointMock.Verify(
            x => x.Publish(It.IsAny<GroupCreatedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateGroup_ShouldRejectInvalidSharePercentages()
    {
        // Arrange
        var createDto = new CreateGroupDto
        {
            Name = "Invalid Group",
            Members = new List<CreateGroupMemberDto>
            {
                new CreateGroupMemberDto
                {
                    UserId = _testUserId,
                    SharePercentage = 0.5m, // Only 50%, should be 100%
                    RoleInGroup = GroupRole.Admin
                }
            }
        };

        // Act
        var result = await _controller.CreateGroup(createDto);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result as BadRequestObjectResult;
        badRequest!.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateGroup_ShouldRejectSharePercentagesOver100()
    {
        // Arrange
        var createDto = new CreateGroupDto
        {
            Name = "Invalid Group",
            Members = new List<CreateGroupMemberDto>
            {
                new CreateGroupMemberDto
                {
                    UserId = _testUserId,
                    SharePercentage = 1.5m, // 150%, should be 100%
                    RoleInGroup = GroupRole.Admin
                }
            }
        };

        // Act
        var result = await _controller.CreateGroup(createDto);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateGroup_ShouldAcceptMultipleMembersWithValidShares()
    {
        // Arrange
        var member2Id = Guid.NewGuid();
        var member2 = new User
        {
            Id = member2Id,
            Email = "member2@example.com",
            FirstName = "Member",
            LastName = "Two",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(member2);
        await _context.SaveChangesAsync();

        var createDto = new CreateGroupDto
        {
            Name = "Multi Member Group",
            Members = new List<CreateGroupMemberDto>
            {
                new CreateGroupMemberDto
                {
                    UserId = _testUserId,
                    SharePercentage = 0.6m,
                    RoleInGroup = GroupRole.Admin
                },
                new CreateGroupMemberDto
                {
                    UserId = member2Id,
                    SharePercentage = 0.4m,
                    RoleInGroup = GroupRole.Member
                }
            }
        };

        // Act
        var result = await _controller.CreateGroup(createDto);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var group = okResult!.Value as GroupDto;
        group.Should().NotBeNull();
        group!.Members.Should().HaveCount(2);
        group.Members.Sum(m => m.SharePercentage).Should().BeApproximately(1.0m, 0.0001m);
    }

    [Fact]
    public async Task CreateGroup_ShouldSetCorrectGroupStatus()
    {
        // Arrange
        var createDto = new CreateGroupDto
        {
            Name = "Status Test Group",
            Members = new List<CreateGroupMemberDto>
            {
                new CreateGroupMemberDto
                {
                    UserId = _testUserId,
                    SharePercentage = 1.0m,
                    RoleInGroup = GroupRole.Admin
                }
            }
        };

        // Act
        var result = await _controller.CreateGroup(createDto);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        var group = okResult!.Value as GroupDto;
        group.Should().NotBeNull();
        group!.Status.Should().Be(GroupStatus.Active);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}

