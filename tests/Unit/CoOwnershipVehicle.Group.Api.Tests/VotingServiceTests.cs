using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Group.Api.Data;
using CoOwnershipVehicle.Group.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace CoOwnershipVehicle.Group.Api.Tests;

public class VotingServiceTests : IDisposable
{
    private readonly GroupDbContext _context;
    private readonly Mock<ILogger<VotingService>> _loggerMock;
    private readonly VotingService _votingService;
    private readonly Guid _testGroupId;
    private readonly Guid _testProposalId;

    public VotingServiceTests()
    {
        var options = new DbContextOptionsBuilder<GroupDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new GroupDbContext(options);
        _loggerMock = new Mock<ILogger<VotingService>>();
        _votingService = new VotingService(_context, _loggerMock.Object);
        _testGroupId = Guid.NewGuid();
        _testProposalId = Guid.NewGuid();

        SeedTestData();
    }

    private void SeedTestData()
    {
        var group = new OwnershipGroup
        {
            Id = _testGroupId,
            Name = "Test Group",
            Status = GroupStatus.Active,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.OwnershipGroups.Add(group);

        // Create members with different ownership percentages
        var member1 = new GroupMember
        {
            Id = Guid.NewGuid(),
            GroupId = _testGroupId,
            UserId = Guid.NewGuid(),
            SharePercentage = 0.5m, // 50%
            RoleInGroup = GroupRole.Admin,
            JoinedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.GroupMembers.Add(member1);

        var member2 = new GroupMember
        {
            Id = Guid.NewGuid(),
            GroupId = _testGroupId,
            UserId = Guid.NewGuid(),
            SharePercentage = 0.3m, // 30%
            RoleInGroup = GroupRole.Member,
            JoinedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.GroupMembers.Add(member2);

        var member3 = new GroupMember
        {
            Id = Guid.NewGuid(),
            GroupId = _testGroupId,
            UserId = Guid.NewGuid(),
            SharePercentage = 0.2m, // 20%
            RoleInGroup = GroupRole.Member,
            JoinedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.GroupMembers.Add(member3);

        var proposal = new Proposal
        {
            Id = _testProposalId,
            GroupId = _testGroupId,
            CreatedBy = member1.UserId,
            Title = "Test Proposal",
            Description = "Test Description",
            Type = ProposalType.Other,
            Status = ProposalStatus.Active,
            VotingStartDate = DateTime.UtcNow.AddDays(-1),
            VotingEndDate = DateTime.UtcNow.AddDays(6),
            RequiredMajority = 0.5m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Proposals.Add(proposal);

        _context.SaveChanges();
    }

    [Fact]
    public async Task CalculateVoteTally_ShouldReturnCorrectWeights_WhenVotesExist()
    {
        // Arrange
        var vote1 = new Vote
        {
            Id = Guid.NewGuid(),
            ProposalId = _testProposalId,
            VoterId = Guid.NewGuid(),
            Weight = 0.5m,
            Choice = VoteChoice.Yes,
            VotedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Votes.Add(vote1);

        var vote2 = new Vote
        {
            Id = Guid.NewGuid(),
            ProposalId = _testProposalId,
            VoterId = Guid.NewGuid(),
            Weight = 0.3m,
            Choice = VoteChoice.No,
            VotedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Votes.Add(vote2);

        await _context.SaveChangesAsync();

        // Act
        var result = await _votingService.CalculateVoteTallyAsync(_testProposalId);

        // Assert
        result.Should().NotBeNull();
        result.YesWeight.Should().Be(0.5m);
        result.NoWeight.Should().Be(0.3m);
        result.TotalWeight.Should().Be(0.8m);
        result.YesPercentage.Should().BeApproximately(0.625m, 0.001m);
        result.NoPercentage.Should().BeApproximately(0.375m, 0.001m);
    }

    [Fact]
    public async Task CalculateVoteTally_ShouldReturnZero_WhenNoVotes()
    {
        // Act
        var result = await _votingService.CalculateVoteTallyAsync(_testProposalId);

        // Assert
        result.Should().NotBeNull();
        result.YesWeight.Should().Be(0m);
        result.NoWeight.Should().Be(0m);
        result.AbstainWeight.Should().Be(0m);
        result.TotalWeight.Should().Be(0m);
    }

    [Fact]
    public async Task CheckQuorum_ShouldReturnTrue_WhenQuorumMet()
    {
        // Arrange
        var vote = new Vote
        {
            Id = Guid.NewGuid(),
            ProposalId = _testProposalId,
            VoterId = Guid.NewGuid(),
            Weight = 0.6m, // 60% > 50% required
            Choice = VoteChoice.Yes,
            VotedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Votes.Add(vote);
        await _context.SaveChangesAsync();

        // Act
        var result = await _votingService.CheckQuorumAsync(_testProposalId, 0.5m);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CheckQuorum_ShouldReturnFalse_WhenQuorumNotMet()
    {
        // Arrange
        var vote = new Vote
        {
            Id = Guid.NewGuid(),
            ProposalId = _testProposalId,
            VoterId = Guid.NewGuid(),
            Weight = 0.3m, // 30% < 50% required
            Choice = VoteChoice.Yes,
            VotedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Votes.Add(vote);
        await _context.SaveChangesAsync();

        // Act
        var result = await _votingService.CheckQuorumAsync(_testProposalId, 0.5m);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DetermineProposalOutcome_ShouldReturnTrue_WhenPassed()
    {
        // Arrange
        var vote1 = new Vote
        {
            Id = Guid.NewGuid(),
            ProposalId = _testProposalId,
            VoterId = Guid.NewGuid(),
            Weight = 0.5m,
            Choice = VoteChoice.Yes,
            VotedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Votes.Add(vote1);

        var vote2 = new Vote
        {
            Id = Guid.NewGuid(),
            ProposalId = _testProposalId,
            VoterId = Guid.NewGuid(),
            Weight = 0.3m,
            Choice = VoteChoice.Yes,
            VotedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Votes.Add(vote2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _votingService.DetermineProposalOutcomeAsync(_testProposalId);

        // Assert
        result.Should().BeTrue(); // 80% yes votes > 50% required
    }

    [Fact]
    public async Task DetermineProposalOutcome_ShouldReturnFalse_WhenRejected()
    {
        // Arrange
        var vote1 = new Vote
        {
            Id = Guid.NewGuid(),
            ProposalId = _testProposalId,
            VoterId = Guid.NewGuid(),
            Weight = 0.5m,
            Choice = VoteChoice.No,
            VotedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Votes.Add(vote1);
        await _context.SaveChangesAsync();

        // Act
        var result = await _votingService.DetermineProposalOutcomeAsync(_testProposalId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanTransitionStatus_ShouldReturnTrue_ForValidTransitions()
    {
        // Arrange
        var proposal = await _context.Proposals.FindAsync(_testProposalId);

        // Act & Assert
        (await _votingService.CanTransitionStatusAsync(proposal!, ProposalStatus.Passed)).Should().BeTrue();
        (await _votingService.CanTransitionStatusAsync(proposal!, ProposalStatus.Rejected)).Should().BeTrue();
        (await _votingService.CanTransitionStatusAsync(proposal!, ProposalStatus.Expired)).Should().BeTrue();
        (await _votingService.CanTransitionStatusAsync(proposal!, ProposalStatus.Cancelled)).Should().BeTrue();
    }

    [Fact]
    public async Task CanTransitionStatus_ShouldReturnFalse_ForInvalidTransitions()
    {
        // Arrange
        var proposal = await _context.Proposals.FindAsync(_testProposalId);
        proposal!.Status = ProposalStatus.Passed;

        // Act & Assert
        (await _votingService.CanTransitionStatusAsync(proposal, ProposalStatus.Active)).Should().BeFalse();
        (await _votingService.CanTransitionStatusAsync(proposal, ProposalStatus.Rejected)).Should().BeFalse();
    }

    [Fact]
    public async Task ResolveProposalStatus_ShouldReturnExpired_WhenVotingEndedWithoutQuorum()
    {
        // Arrange
        var proposal = await _context.Proposals.FindAsync(_testProposalId);
        proposal!.VotingEndDate = DateTime.UtcNow.AddDays(-1);
        proposal.Status = ProposalStatus.Active;
        await _context.SaveChangesAsync();

        // Act
        var result = await _votingService.ResolveProposalStatusAsync(proposal);

        // Assert
        result.Should().Be(ProposalStatus.Expired);
    }

    [Fact]
    public async Task ProcessExpiredProposals_ShouldUpdateExpiredProposals()
    {
        // Arrange
        var expiredProposal = new Proposal
        {
            Id = Guid.NewGuid(),
            GroupId = _testGroupId,
            CreatedBy = Guid.NewGuid(),
            Title = "Expired Proposal",
            Description = "Test",
            Type = ProposalType.Other,
            Status = ProposalStatus.Active,
            VotingStartDate = DateTime.UtcNow.AddDays(-10),
            VotingEndDate = DateTime.UtcNow.AddDays(-1), // Expired
            RequiredMajority = 0.5m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Proposals.Add(expiredProposal);
        await _context.SaveChangesAsync();

        // Act
        await _votingService.ProcessExpiredProposalsAsync();

        // Assert
        var updatedProposal = await _context.Proposals.FindAsync(expiredProposal.Id);
        updatedProposal!.Status.Should().Be(ProposalStatus.Expired);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}



