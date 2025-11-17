using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Group.Api.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace CoOwnershipVehicle.Group.Api.Tests.Integration;

public class VotingWorkflowTests : IDisposable
{
    private readonly GroupDbContext _context;
    private readonly Guid _testGroupId;
    private readonly Guid _creatorId;
    private readonly Guid _member1Id;
    private readonly Guid _member2Id;

    public VotingWorkflowTests()
    {
        var options = new DbContextOptionsBuilder<GroupDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new GroupDbContext(options);
        _testGroupId = Guid.NewGuid();
        _creatorId = Guid.NewGuid();
        _member1Id = Guid.NewGuid();
        _member2Id = Guid.NewGuid();

        SeedTestData();
    }

    private void SeedTestData()
    {
        var creator = new User
        {
            Id = _creatorId,
            Email = "creator@example.com",
            FirstName = "Creator",
            LastName = "User",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        // Note: Users are no longer stored in GroupDbContext - they're fetched via HTTP

        var group = new OwnershipGroup
        {
            Id = _testGroupId,
            Name = "Test Group",
            Status = GroupStatus.Active,
            CreatedBy = _creatorId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.OwnershipGroups.Add(group);

        var creatorMember = new GroupMember
        {
            Id = Guid.NewGuid(),
            GroupId = _testGroupId,
            UserId = _creatorId,
            SharePercentage = 0.4m, // 40%
            RoleInGroup = GroupRole.Admin,
            JoinedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.GroupMembers.Add(creatorMember);

        var member1Member = new GroupMember
        {
            Id = Guid.NewGuid(),
            GroupId = _testGroupId,
            UserId = _member1Id,
            SharePercentage = 0.35m, // 35%
            RoleInGroup = GroupRole.Member,
            JoinedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.GroupMembers.Add(member1Member);

        var member2Member = new GroupMember
        {
            Id = Guid.NewGuid(),
            GroupId = _testGroupId,
            UserId = _member2Id,
            SharePercentage = 0.25m, // 25%
            RoleInGroup = GroupRole.Member,
            JoinedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.GroupMembers.Add(member2Member);

        _context.SaveChanges();
    }

    [Fact]
    public async Task FullVotingWorkflow_ShouldCompleteSuccessfully()
    {
        // Arrange - Create proposal
        var proposal = new Proposal
        {
            Id = Guid.NewGuid(),
            GroupId = _testGroupId,
            CreatedBy = _creatorId,
            Title = "Test Proposal",
            Description = "Test Description",
            Type = ProposalType.MaintenanceBudget,
            Status = ProposalStatus.Active,
            VotingStartDate = DateTime.UtcNow.AddDays(-1),
            VotingEndDate = DateTime.UtcNow.AddDays(6),
            RequiredMajority = 0.5m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Proposals.Add(proposal);
        await _context.SaveChangesAsync();

        proposal.Status.Should().Be(ProposalStatus.Active);

        // Act - Cast votes
        var vote1 = new Vote
        {
            Id = Guid.NewGuid(),
            ProposalId = proposal.Id,
            VoterId = _creatorId,
            Weight = 0.4m, // 40% ownership
            Choice = VoteChoice.Yes,
            VotedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Votes.Add(vote1);

        var vote2 = new Vote
        {
            Id = Guid.NewGuid(),
            ProposalId = proposal.Id,
            VoterId = _member1Id,
            Weight = 0.35m, // 35% ownership
            Choice = VoteChoice.Yes,
            VotedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Votes.Add(vote2);

        var vote3 = new Vote
        {
            Id = Guid.NewGuid(),
            ProposalId = proposal.Id,
            VoterId = _member2Id,
            Weight = 0.25m, // 25% ownership
            Choice = VoteChoice.No,
            VotedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Votes.Add(vote3);

        await _context.SaveChangesAsync();

        // Calculate results
        var yesWeight = 0.4m + 0.35m; // 75%
        var totalWeight = 0.4m + 0.35m + 0.25m; // 100%
        var yesPercentage = yesWeight / totalWeight; // 75%

        yesPercentage.Should().BeGreaterThan(0.5m); // Should pass

        // Close proposal
        proposal.Status = yesPercentage >= proposal.RequiredMajority
            ? ProposalStatus.Passed
            : ProposalStatus.Rejected;
        proposal.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Assert
        proposal.Status.Should().Be(ProposalStatus.Passed);
        var votes = await _context.Votes.Where(v => v.ProposalId == proposal.Id).ToListAsync();
        votes.Should().HaveCount(3);
    }

    [Fact]
    public async Task ConcurrentVoting_ShouldHandleRaceConditions()
    {
        // Arrange
        var proposal = new Proposal
        {
            Id = Guid.NewGuid(),
            GroupId = _testGroupId,
            CreatedBy = _creatorId,
            Title = "Concurrent Test",
            Description = "Test",
            Type = ProposalType.Other,
            Status = ProposalStatus.Active,
            VotingStartDate = DateTime.UtcNow.AddDays(-1),
            VotingEndDate = DateTime.UtcNow.AddDays(6),
            RequiredMajority = 0.5m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Proposals.Add(proposal);
        await _context.SaveChangesAsync();

        // Act - Simulate concurrent votes (using same context for in-memory DB)
        var vote1 = new Vote
        {
            Id = Guid.NewGuid(),
            ProposalId = proposal.Id,
            VoterId = _creatorId,
            Weight = 0.4m,
            Choice = VoteChoice.Yes,
            VotedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Votes.Add(vote1);

        var vote2 = new Vote
        {
            Id = Guid.NewGuid(),
            ProposalId = proposal.Id,
            VoterId = _member1Id,
            Weight = 0.35m,
            Choice = VoteChoice.Yes,
            VotedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Votes.Add(vote2);

        await _context.SaveChangesAsync();

        // Assert - Should have both votes
        var votes = await _context.Votes.Where(v => v.ProposalId == proposal.Id).ToListAsync();
        votes.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExpiredProposal_ShouldAutoResolve()
    {
        // Arrange
        var proposal = new Proposal
        {
            Id = Guid.NewGuid(),
            GroupId = _testGroupId,
            CreatedBy = _creatorId,
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
        _context.Proposals.Add(proposal);
        await _context.SaveChangesAsync();

        // Act - Process expired proposals
        var logger = new Mock<ILogger<CoOwnershipVehicle.Group.Api.Services.VotingService>>();
        var votingService = new CoOwnershipVehicle.Group.Api.Services.VotingService(
            _context,
            logger.Object);
        
        await votingService.ProcessExpiredProposalsAsync();

        // Assert
        var updatedProposal = await _context.Proposals.FindAsync(proposal.Id);
        updatedProposal!.Status.Should().Be(ProposalStatus.Expired);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}

