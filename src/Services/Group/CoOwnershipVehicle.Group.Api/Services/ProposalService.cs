using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Group.Api.Contracts;
using CoOwnershipVehicle.Group.Api.Data;
using CoOwnershipVehicle.Group.Api.Services.Interfaces;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Shared.Contracts.Events;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VoteChoice = CoOwnershipVehicle.Domain.Entities.VoteChoice;

namespace CoOwnershipVehicle.Group.Api.Services;

public class ProposalService : IProposalService
{
    private readonly GroupDbContext _context;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<ProposalService> _logger;
    private readonly IUserServiceClient _userServiceClient;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ProposalService(
        GroupDbContext context,
        IPublishEndpoint publishEndpoint,
        ILogger<ProposalService> logger,
        IUserServiceClient userServiceClient,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _userServiceClient = userServiceClient ?? throw new ArgumentNullException(nameof(userServiceClient));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    private string GetAccessToken()
    {
        var authHeader = _httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            return string.Empty;
        }
        return authHeader.Substring("Bearer ".Length).Trim();
    }

    public async Task<ProposalDto> CreateProposalAsync(CreateProposalDto createDto, Guid userId)
    {
        // Validate user is member of the group
        var membership = await _context.GroupMembers
            .FirstOrDefaultAsync(m => m.GroupId == createDto.GroupId && m.UserId == userId);

        if (membership == null)
        {
            throw new UnauthorizedAccessException("User is not a member of this group");
        }

        // Validate group exists
        var group = await _context.OwnershipGroups
            .FirstOrDefaultAsync(g => g.Id == createDto.GroupId);

        if (group == null)
        {
            throw new KeyNotFoundException("Group not found");
        }

        // Validate voting dates
        if (createDto.VotingEndDate <= createDto.VotingStartDate)
        {
            throw new ArgumentException("Voting end date must be after start date");
        }

        if (createDto.VotingStartDate < DateTime.UtcNow.AddMinutes(-5)) // Allow 5 minute tolerance
        {
            throw new ArgumentException("Voting start date cannot be in the past");
        }

        // Validate proposal type
        if (!Enum.IsDefined(typeof(ProposalType), createDto.Type))
        {
            throw new ArgumentException("Invalid proposal type");
        }

        // Create proposal
        var proposal = new Proposal
        {
            Id = Guid.NewGuid(),
            GroupId = createDto.GroupId,
            CreatedBy = userId,
            Title = createDto.Title,
            Description = createDto.Description,
            Type = createDto.Type,
            Amount = createDto.Amount,
            Status = ProposalStatus.Active,
            VotingStartDate = createDto.VotingStartDate,
            VotingEndDate = createDto.VotingEndDate,
            RequiredMajority = createDto.RequiredMajority,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Proposals.Add(proposal);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Proposal {ProposalId} created for group {GroupId} by user {UserId}",
            proposal.Id, proposal.GroupId, userId);

        // Publish event
        await _publishEndpoint.Publish(new ProposalCreatedEvent
        {
            ProposalId = proposal.Id,
            GroupId = proposal.GroupId,
            CreatedBy = userId,
            Title = proposal.Title,
            Type = proposal.Type,
            VotingEndDate = proposal.VotingEndDate
        });

        // Get creator name via HTTP call
        var accessToken = GetAccessToken();
        var creator = await _userServiceClient.GetUserAsync(userId, accessToken);

        return MapToDto(proposal, creator);
    }

    public async Task<List<ProposalListDto>> GetProposalsByGroupAsync(Guid groupId, Guid userId, ProposalStatus? status = null)
    {
        // Validate user is member of the group
        var membership = await _context.GroupMembers
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId);

        if (membership == null)
        {
            throw new UnauthorizedAccessException("User is not a member of this group");
        }

        var query = _context.Proposals
            .Include(p => p.Votes)
            .Where(p => p.GroupId == groupId);

        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        var proposals = await query
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        var result = new List<ProposalListDto>();

        foreach (var proposal in proposals)
        {
            var totalOwnership = await _context.GroupMembers
                .Where(m => m.GroupId == groupId)
                .SumAsync(m => m.SharePercentage);

            var votedOwnership = proposal.Votes.Sum(v => v.Weight);
            var votingProgress = totalOwnership > 0 ? votedOwnership / totalOwnership : 0m;

            result.Add(new ProposalListDto
            {
                Id = proposal.Id,
                Title = proposal.Title,
                Description = proposal.Description,
                Type = proposal.Type,
                Status = proposal.Status,
                Amount = proposal.Amount,
                CreatedAt = proposal.CreatedAt,
                VotingEndDate = proposal.VotingEndDate,
                TotalVotes = proposal.Votes.Count,
                YesVotes = proposal.Votes.Count(v => v.Choice == VoteChoice.Yes),
                NoVotes = proposal.Votes.Count(v => v.Choice == VoteChoice.No),
                AbstainVotes = proposal.Votes.Count(v => v.Choice == VoteChoice.Abstain),
                VotingProgress = votingProgress
            });
        }

        return result;
    }

    public async Task<ProposalDetailsDto> GetProposalByIdAsync(Guid proposalId, Guid userId)
    {
        var proposal = await _context.Proposals
            .Include(p => p.Votes)
            .Include(p => p.Group)
            .FirstOrDefaultAsync(p => p.Id == proposalId);

        if (proposal == null)
        {
            throw new KeyNotFoundException("Proposal not found");
        }

        // Validate user is member of the group
        var membership = await _context.GroupMembers
            .FirstOrDefaultAsync(m => m.GroupId == proposal.GroupId && m.UserId == userId);

        if (membership == null)
        {
            throw new UnauthorizedAccessException("User is not a member of this group");
        }

        // Calculate vote tally
        var totalOwnership = await _context.GroupMembers
            .Where(m => m.GroupId == proposal.GroupId)
            .SumAsync(m => m.SharePercentage);

        var votedOwnership = proposal.Votes.Sum(v => v.Weight);
        var votingProgress = totalOwnership > 0 ? votedOwnership / totalOwnership : 0m;

        var yesWeight = proposal.Votes.Where(v => v.Choice == VoteChoice.Yes).Sum(v => v.Weight);
        var noWeight = proposal.Votes.Where(v => v.Choice == VoteChoice.No).Sum(v => v.Weight);
        var abstainWeight = proposal.Votes.Where(v => v.Choice == VoteChoice.Abstain).Sum(v => v.Weight);
        var totalWeight = proposal.Votes.Sum(v => v.Weight);

        var voteTally = new VoteTallyDto
        {
            YesWeight = yesWeight,
            NoWeight = noWeight,
            AbstainWeight = abstainWeight,
            TotalWeight = totalWeight,
            YesPercentage = totalWeight > 0 ? yesWeight / totalWeight : 0m,
            NoPercentage = totalWeight > 0 ? noWeight / totalWeight : 0m,
            AbstainPercentage = totalWeight > 0 ? abstainWeight / totalWeight : 0m
        };

        var timeRemaining = proposal.VotingEndDate > DateTime.UtcNow
            ? proposal.VotingEndDate - DateTime.UtcNow
            : (TimeSpan?)null;

        // Fetch user data via HTTP
        var accessToken = GetAccessToken();
        var userIds = proposal.Votes.Select(v => v.VoterId).ToList();
        userIds.Add(proposal.CreatedBy);
        var users = await _userServiceClient.GetUsersAsync(userIds.Distinct().ToList(), accessToken);

        var creator = users.GetValueOrDefault(proposal.CreatedBy);
        var creatorName = creator != null ? $"{creator.FirstName} {creator.LastName}" : "Unknown";

        var votes = new List<VoteDto>();
        foreach (var vote in proposal.Votes)
        {
            var voter = users.GetValueOrDefault(vote.VoterId);
            votes.Add(new VoteDto
            {
                Id = vote.Id,
                ProposalId = vote.ProposalId,
                VoterId = vote.VoterId,
                VoterName = voter != null ? $"{voter.FirstName} {voter.LastName}" : "Unknown",
                Weight = vote.Weight,
                Choice = vote.Choice,
                Comment = vote.Comment,
                VotedAt = vote.VotedAt
            });
        }

        return new ProposalDetailsDto
        {
            Id = proposal.Id,
            GroupId = proposal.GroupId,
            CreatedBy = proposal.CreatedBy,
            Title = proposal.Title,
            Description = proposal.Description,
            Type = proposal.Type,
            Amount = proposal.Amount,
            Status = proposal.Status,
            VotingStartDate = proposal.VotingStartDate,
            VotingEndDate = proposal.VotingEndDate,
            RequiredMajority = proposal.RequiredMajority,
            CreatedAt = proposal.CreatedAt,
            UpdatedAt = proposal.UpdatedAt,
            CreatorName = creatorName,
            Votes = votes,
            VoteTally = voteTally,
            VotingProgress = votingProgress,
            TimeRemaining = timeRemaining
        };
    }

    public async Task<VoteDto> CastVoteAsync(Guid proposalId, CastVoteDto voteDto, Guid userId)
    {
        var proposal = await _context.Proposals
            .Include(p => p.Group)
            .FirstOrDefaultAsync(p => p.Id == proposalId);

        if (proposal == null)
        {
            throw new KeyNotFoundException("Proposal not found");
        }

        // Validate user is member of the group
        var membership = await _context.GroupMembers
            .FirstOrDefaultAsync(m => m.GroupId == proposal.GroupId && m.UserId == userId);

        if (membership == null)
        {
            throw new UnauthorizedAccessException("User is not a member of this group");
        }

        // Check if user already voted
        var existingVote = await _context.Votes
            .FirstOrDefaultAsync(v => v.ProposalId == proposalId && v.VoterId == userId);

        if (existingVote != null)
        {
            throw new InvalidOperationException("User has already voted on this proposal");
        }

        // Validate voting period
        if (DateTime.UtcNow < proposal.VotingStartDate)
        {
            throw new InvalidOperationException("Voting has not started yet");
        }

        if (DateTime.UtcNow > proposal.VotingEndDate)
        {
            throw new InvalidOperationException("Voting period has ended");
        }

        // Validate proposal status
        if (proposal.Status != ProposalStatus.Active)
        {
            throw new InvalidOperationException($"Cannot vote on proposal with status {proposal.Status}");
        }

        // Create vote with weight based on ownership percentage
        var vote = new Vote
        {
            Id = Guid.NewGuid(),
            ProposalId = proposalId,
            VoterId = userId,
            Weight = membership.SharePercentage,
            Choice = voteDto.Choice,
            Comment = voteDto.Comment,
            VotedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Votes.Add(vote);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Vote cast on proposal {ProposalId} by user {UserId} with choice {Choice}",
            proposalId, userId, voteDto.Choice);

        // Get voter name via HTTP call
        var accessToken = GetAccessToken();
        var voter = await _userServiceClient.GetUserAsync(userId, accessToken);

        // Publish event (using existing VoteCreatedEvent)
        await _publishEndpoint.Publish(new VoteCreatedEvent
        {
            VoteId = vote.Id,
            ProposalId = proposalId,
            GroupId = proposal.GroupId,
            VoterId = userId,
            Weight = vote.Weight,
            Choice = (CoOwnershipVehicle.Shared.Contracts.Events.VoteChoice)voteDto.Choice,
            VotedAt = vote.VotedAt
        });

        return new VoteDto
        {
            Id = vote.Id,
            ProposalId = vote.ProposalId,
            VoterId = vote.VoterId,
            VoterName = voter != null ? $"{voter.FirstName} {voter.LastName}" : "Unknown",
            Weight = vote.Weight,
            Choice = vote.Choice,
            Comment = vote.Comment,
            VotedAt = vote.VotedAt
        };
    }

    public async Task<ProposalResultsDto> GetProposalResultsAsync(Guid proposalId, Guid userId)
    {
        var proposal = await _context.Proposals
            .Include(p => p.Votes)
            .Include(p => p.Group)
            .FirstOrDefaultAsync(p => p.Id == proposalId);

        if (proposal == null)
        {
            throw new KeyNotFoundException("Proposal not found");
        }

        // Validate user is member of the group
        var membership = await _context.GroupMembers
            .FirstOrDefaultAsync(m => m.GroupId == proposal.GroupId && m.UserId == userId);

        if (membership == null)
        {
            throw new UnauthorizedAccessException("User is not a member of this group");
        }

        // Calculate vote weights
        var totalOwnership = await _context.GroupMembers
            .Where(m => m.GroupId == proposal.GroupId)
            .SumAsync(m => m.SharePercentage);

        var yesWeight = proposal.Votes.Where(v => v.Choice == VoteChoice.Yes).Sum(v => v.Weight);
        var noWeight = proposal.Votes.Where(v => v.Choice == VoteChoice.No).Sum(v => v.Weight);
        var abstainWeight = proposal.Votes.Where(v => v.Choice == VoteChoice.Abstain).Sum(v => v.Weight);
        var totalWeight = proposal.Votes.Sum(v => v.Weight);

        var quorumMet = totalWeight >= (totalOwnership * proposal.RequiredMajority);
        var quorumPercentage = totalOwnership > 0 ? totalWeight / totalOwnership : 0m;

        var voteTally = new VoteTallyDto
        {
            YesWeight = yesWeight,
            NoWeight = noWeight,
            AbstainWeight = abstainWeight,
            TotalWeight = totalWeight,
            YesPercentage = totalWeight > 0 ? yesWeight / totalWeight : 0m,
            NoPercentage = totalWeight > 0 ? noWeight / totalWeight : 0m,
            AbstainPercentage = totalWeight > 0 ? abstainWeight / totalWeight : 0m
        };

        var passed = quorumMet && yesWeight >= (totalWeight * proposal.RequiredMajority);

        // Fetch user data via HTTP
        var accessToken = GetAccessToken();
        var userIds = proposal.Votes.Select(v => v.VoterId).Distinct().ToList();
        var users = await _userServiceClient.GetUsersAsync(userIds, accessToken);

        var voteBreakdown = proposal.Votes.Select(v =>
        {
            var voter = users.GetValueOrDefault(v.VoterId);
            return new VoteBreakdownDto
            {
                VoterId = v.VoterId,
                VoterName = voter != null ? $"{voter.FirstName} {voter.LastName}" : "Unknown",
                Choice = v.Choice,
                Weight = v.Weight,
                WeightPercentage = totalOwnership > 0 ? v.Weight / totalOwnership : 0m,
                VotedAt = v.VotedAt,
                Comment = v.Comment
            };
        }).ToList();

        return new ProposalResultsDto
        {
            ProposalId = proposal.Id,
            Status = proposal.Status,
            VoteTally = voteTally,
            QuorumMet = quorumMet,
            QuorumPercentage = quorumPercentage,
            RequiredMajority = proposal.RequiredMajority,
            Passed = passed,
            VoteBreakdown = voteBreakdown,
            ClosedAt = proposal.Status != ProposalStatus.Active ? proposal.UpdatedAt : null
        };
    }

    public async Task<ProposalDto> CloseProposalAsync(Guid proposalId, Guid userId)
    {
        var proposal = await _context.Proposals
            .Include(p => p.Votes)
            .Include(p => p.Group)
            .FirstOrDefaultAsync(p => p.Id == proposalId);

        if (proposal == null)
        {
            throw new KeyNotFoundException("Proposal not found");
        }

        // Validate user is admin or creator
        var membership = await _context.GroupMembers
            .FirstOrDefaultAsync(m => m.GroupId == proposal.GroupId && m.UserId == userId);

        if (membership == null)
        {
            throw new UnauthorizedAccessException("User is not a member of this group");
        }

        var isAdmin = membership.RoleInGroup == GroupRole.Admin;
        var isCreator = proposal.CreatedBy == userId;

        if (!isAdmin && !isCreator)
        {
            throw new UnauthorizedAccessException("Only group admin or proposal creator can close proposals");
        }

        if (proposal.Status != ProposalStatus.Active)
        {
            throw new InvalidOperationException($"Cannot close proposal with status {proposal.Status}");
        }

        // Calculate results
        var totalOwnership = await _context.GroupMembers
            .Where(m => m.GroupId == proposal.GroupId)
            .SumAsync(m => m.SharePercentage);

        var yesWeight = proposal.Votes.Where(v => v.Choice == VoteChoice.Yes).Sum(v => v.Weight);
        var totalWeight = proposal.Votes.Sum(v => v.Weight);

        var quorumMet = totalWeight >= (totalOwnership * proposal.RequiredMajority);
        var passed = quorumMet && yesWeight >= (totalWeight * proposal.RequiredMajority);

        // Update status
        proposal.Status = passed ? ProposalStatus.Passed : ProposalStatus.Rejected;
        proposal.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Proposal {ProposalId} closed with status {Status}",
            proposalId, proposal.Status);

        // Publish event
        await _publishEndpoint.Publish(new ProposalClosedEvent
        {
            ProposalId = proposalId,
            GroupId = proposal.GroupId,
            Passed = passed,
            YesPercentage = totalWeight > 0 ? yesWeight / totalWeight : 0m,
            NoPercentage = totalWeight > 0 ? (totalWeight - yesWeight) / totalWeight : 0m,
            ClosedAt = DateTime.UtcNow
        });

        // Get creator name via HTTP call
        var accessToken = GetAccessToken();
        var creator = await _userServiceClient.GetUserAsync(proposal.CreatedBy, accessToken);
        return MapToDto(proposal, creator);
    }

    public async Task<bool> CancelProposalAsync(Guid proposalId, Guid userId)
    {
        var proposal = await _context.Proposals
            .Include(p => p.Group)
            .FirstOrDefaultAsync(p => p.Id == proposalId);

        if (proposal == null)
        {
            throw new KeyNotFoundException("Proposal not found");
        }

        // Validate user is admin or creator
        var membership = await _context.GroupMembers
            .FirstOrDefaultAsync(m => m.GroupId == proposal.GroupId && m.UserId == userId);

        if (membership == null)
        {
            throw new UnauthorizedAccessException("User is not a member of this group");
        }

        var isAdmin = membership.RoleInGroup == GroupRole.Admin;
        var isCreator = proposal.CreatedBy == userId;

        if (!isAdmin && !isCreator)
        {
            throw new UnauthorizedAccessException("Only group admin or proposal creator can cancel proposals");
        }

        if (proposal.Status != ProposalStatus.Active)
        {
            throw new InvalidOperationException($"Cannot cancel proposal with status {proposal.Status}");
        }

        proposal.Status = ProposalStatus.Cancelled;
        proposal.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Proposal {ProposalId} cancelled by user {UserId}",
            proposalId, userId);

        // Publish event
        await _publishEndpoint.Publish(new ProposalCancelledEvent
        {
            ProposalId = proposalId,
            GroupId = proposal.GroupId,
            CancelledBy = userId,
            CancelledAt = DateTime.UtcNow
        });

        return true;
    }

    private static ProposalDto MapToDto(Proposal proposal, UserInfoDto? creator)
    {
        return new ProposalDto
        {
            Id = proposal.Id,
            GroupId = proposal.GroupId,
            CreatedBy = proposal.CreatedBy,
            Title = proposal.Title,
            Description = proposal.Description,
            Type = proposal.Type,
            Amount = proposal.Amount,
            Status = proposal.Status,
            VotingStartDate = proposal.VotingStartDate,
            VotingEndDate = proposal.VotingEndDate,
            RequiredMajority = proposal.RequiredMajority,
            CreatedAt = proposal.CreatedAt,
            UpdatedAt = proposal.UpdatedAt,
            CreatorName = creator != null ? $"{creator.FirstName} {creator.LastName}" : "Unknown"
        };
    }
}

