using System.Security.Claims;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Group.Api.Controllers;
using CoOwnershipVehicle.Group.Api.Contracts;
using CoOwnershipVehicle.Group.Api.Data;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using MassTransit;

namespace CoOwnershipVehicle.Group.Api.Tests;

public class ProposalControllerTests : IDisposable
{
    private readonly GroupDbContext _context;
    private readonly Mock<IProposalService> _proposalServiceMock;
    private readonly Mock<ILogger<ProposalController>> _loggerMock;
    private readonly ProposalController _controller;
    private readonly Guid _testUserId;
    private readonly Guid _testGroupId;

    public ProposalControllerTests()
    {
        var options = new DbContextOptionsBuilder<GroupDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new GroupDbContext(options);
        _proposalServiceMock = new Mock<IProposalService>();
        _loggerMock = new Mock<ILogger<ProposalController>>();
        _testUserId = Guid.NewGuid();
        _testGroupId = Guid.NewGuid();

        _controller = new ProposalController(_proposalServiceMock.Object, _loggerMock.Object);
        
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

        var group = new OwnershipGroup
        {
            Id = _testGroupId,
            Name = "Test Group",
            Status = GroupStatus.Active,
            CreatedBy = _testUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.OwnershipGroups.Add(group);

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
    public async Task CreateProposal_ShouldReturnOk_WhenValid()
    {
        // Arrange
        var createDto = new CreateProposalDto
        {
            GroupId = _testGroupId,
            Title = "Test Proposal",
            Description = "Test Description",
            Type = ProposalType.Other,
            VotingStartDate = DateTime.UtcNow,
            VotingEndDate = DateTime.UtcNow.AddDays(7),
            RequiredMajority = 0.5m
        };

        var proposalDto = new ProposalDto
        {
            Id = Guid.NewGuid(),
            GroupId = _testGroupId,
            Title = createDto.Title,
            Description = createDto.Description,
            Type = createDto.Type,
            Status = ProposalStatus.Active,
            VotingStartDate = createDto.VotingStartDate,
            VotingEndDate = createDto.VotingEndDate,
            RequiredMajority = createDto.RequiredMajority,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _proposalServiceMock
            .Setup(x => x.CreateProposalAsync(createDto, _testUserId))
            .ReturnsAsync(proposalDto);

        // Act
        var result = await _controller.CreateProposal(createDto);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var returnedProposal = okResult!.Value as ProposalDto;
        returnedProposal.Should().NotBeNull();
        returnedProposal!.Title.Should().Be("Test Proposal");
    }

    [Fact]
    public async Task CreateProposal_ShouldReturnBadRequest_WhenModelInvalid()
    {
        // Arrange
        var createDto = new CreateProposalDto
        {
            GroupId = _testGroupId,
            Title = "", // Invalid
            Description = "Test Description",
            Type = ProposalType.Other,
            VotingStartDate = DateTime.UtcNow,
            VotingEndDate = DateTime.UtcNow.AddDays(7),
            RequiredMajority = 0.5m
        };

        _controller.ModelState.AddModelError("Title", "Title is required");

        // Act
        var result = await _controller.CreateProposal(createDto);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateProposal_ShouldReturnForbidden_WhenUnauthorized()
    {
        // Arrange
        var createDto = new CreateProposalDto
        {
            GroupId = _testGroupId,
            Title = "Test Proposal",
            Description = "Test Description",
            Type = ProposalType.Other,
            VotingStartDate = DateTime.UtcNow,
            VotingEndDate = DateTime.UtcNow.AddDays(7),
            RequiredMajority = 0.5m
        };

        _proposalServiceMock
            .Setup(x => x.CreateProposalAsync(createDto, _testUserId))
            .ThrowsAsync(new UnauthorizedAccessException("User is not a member"));

        // Act
        var result = await _controller.CreateProposal(createDto);

        // Assert
        result.Result.Should().BeOfType<ObjectResult>();
        var objectResult = result.Result as ObjectResult;
        objectResult!.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task GetProposalsByGroup_ShouldReturnOk_WhenValid()
    {
        // Arrange
        var proposals = new List<ProposalListDto>
        {
            new ProposalListDto
            {
                Id = Guid.NewGuid(),
                Title = "Proposal 1",
                Status = ProposalStatus.Active,
                CreatedAt = DateTime.UtcNow
            }
        };

        _proposalServiceMock
            .Setup(x => x.GetProposalsByGroupAsync(_testGroupId, _testUserId, null))
            .ReturnsAsync(proposals);

        // Act
        var result = await _controller.GetProposalsByGroup(_testGroupId);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var returnedProposals = okResult!.Value as List<ProposalListDto>;
        returnedProposals.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetProposal_ShouldReturnOk_WhenValid()
    {
        // Arrange
        var proposalId = Guid.NewGuid();
        var proposalDetails = new ProposalDetailsDto
        {
            Id = proposalId,
            GroupId = _testGroupId,
            Title = "Test Proposal",
            Status = ProposalStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _proposalServiceMock
            .Setup(x => x.GetProposalByIdAsync(proposalId, _testUserId))
            .ReturnsAsync(proposalDetails);

        // Act
        var result = await _controller.GetProposal(proposalId);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var returnedProposal = okResult!.Value as ProposalDetailsDto;
        returnedProposal.Should().NotBeNull();
        returnedProposal!.Title.Should().Be("Test Proposal");
    }

    [Fact]
    public async Task GetProposal_ShouldReturnNotFound_WhenProposalNotFound()
    {
        // Arrange
        var proposalId = Guid.NewGuid();

        _proposalServiceMock
            .Setup(x => x.GetProposalByIdAsync(proposalId, _testUserId))
            .ThrowsAsync(new KeyNotFoundException("Proposal not found"));

        // Act
        var result = await _controller.GetProposal(proposalId);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task CastVote_ShouldReturnOk_WhenValid()
    {
        // Arrange
        var proposalId = Guid.NewGuid();
        var voteDto = new CastVoteDto
        {
            Choice = VoteChoice.Yes,
            Comment = "I agree"
        };

        var voteResult = new VoteDto
        {
            Id = Guid.NewGuid(),
            ProposalId = proposalId,
            VoterId = _testUserId,
            Choice = VoteChoice.Yes,
            Weight = 1.0m,
            VotedAt = DateTime.UtcNow
        };

        _proposalServiceMock
            .Setup(x => x.CastVoteAsync(proposalId, voteDto, _testUserId))
            .ReturnsAsync(voteResult);

        // Act
        var result = await _controller.CastVote(proposalId, voteDto);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var returnedVote = okResult!.Value as VoteDto;
        returnedVote.Should().NotBeNull();
        returnedVote!.Choice.Should().Be(VoteChoice.Yes);
    }

    [Fact]
    public async Task CastVote_ShouldReturnConflict_WhenAlreadyVoted()
    {
        // Arrange
        var proposalId = Guid.NewGuid();
        var voteDto = new CastVoteDto
        {
            Choice = VoteChoice.Yes
        };

        _proposalServiceMock
            .Setup(x => x.CastVoteAsync(proposalId, voteDto, _testUserId))
            .ThrowsAsync(new InvalidOperationException("User has already voted on this proposal"));

        // Act
        var result = await _controller.CastVote(proposalId, voteDto);

        // Assert
        result.Result.Should().BeOfType<ObjectResult>();
        var objectResult = result.Result as ObjectResult;
        objectResult!.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task GetProposalResults_ShouldReturnOk_WhenValid()
    {
        // Arrange
        var proposalId = Guid.NewGuid();
        var results = new ProposalResultsDto
        {
            ProposalId = proposalId,
            Status = ProposalStatus.Active,
            QuorumMet = true,
            Passed = true
        };

        _proposalServiceMock
            .Setup(x => x.GetProposalResultsAsync(proposalId, _testUserId))
            .ReturnsAsync(results);

        // Act
        var result = await _controller.GetProposalResults(proposalId);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var returnedResults = okResult!.Value as ProposalResultsDto;
        returnedResults.Should().NotBeNull();
        returnedResults!.QuorumMet.Should().BeTrue();
    }

    [Fact]
    public async Task CloseProposal_ShouldReturnOk_WhenValid()
    {
        // Arrange
        var proposalId = Guid.NewGuid();
        var proposalDto = new ProposalDto
        {
            Id = proposalId,
            GroupId = _testGroupId,
            Title = "Test Proposal",
            Status = ProposalStatus.Passed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _proposalServiceMock
            .Setup(x => x.CloseProposalAsync(proposalId, _testUserId))
            .ReturnsAsync(proposalDto);

        // Act
        var result = await _controller.CloseProposal(proposalId);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var returnedProposal = okResult!.Value as ProposalDto;
        returnedProposal.Should().NotBeNull();
        returnedProposal!.Status.Should().Be(ProposalStatus.Passed);
    }

    [Fact]
    public async Task CancelProposal_ShouldReturnOk_WhenValid()
    {
        // Arrange
        var proposalId = Guid.NewGuid();

        _proposalServiceMock
            .Setup(x => x.CancelProposalAsync(proposalId, _testUserId))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.CancelProposal(proposalId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CancelProposal_ShouldReturnBadRequest_WhenNotActive()
    {
        // Arrange
        var proposalId = Guid.NewGuid();

        _proposalServiceMock
            .Setup(x => x.CancelProposalAsync(proposalId, _testUserId))
            .ThrowsAsync(new InvalidOperationException("Cannot cancel proposal with status Passed"));

        // Act
        var result = await _controller.CancelProposal(proposalId);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}



