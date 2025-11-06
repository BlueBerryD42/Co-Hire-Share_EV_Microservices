using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Group.Api.Contracts;
using CoOwnershipVehicle.Group.Api.Data;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VoteChoice = CoOwnershipVehicle.Domain.Entities.VoteChoice;

namespace CoOwnershipVehicle.Group.Api.Services;

public class VotingService : IVotingService
{
    private readonly GroupDbContext _context;
    private readonly ILogger<VotingService> _logger;

    public VotingService(
        GroupDbContext context,
        ILogger<VotingService> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<VoteTallyDto> CalculateVoteTallyAsync(Guid proposalId)
    {
        var proposal = await _context.Proposals
            .Include(p => p.Votes)
            .FirstOrDefaultAsync(p => p.Id == proposalId);

        if (proposal == null)
        {
            throw new KeyNotFoundException("Proposal not found");
        }

        var yesWeight = proposal.Votes.Where(v => v.Choice == VoteChoice.Yes).Sum(v => v.Weight);
        var noWeight = proposal.Votes.Where(v => v.Choice == VoteChoice.No).Sum(v => v.Weight);
        var abstainWeight = proposal.Votes.Where(v => v.Choice == VoteChoice.Abstain).Sum(v => v.Weight);
        var totalWeight = proposal.Votes.Sum(v => v.Weight);

        return new VoteTallyDto
        {
            YesWeight = yesWeight,
            NoWeight = noWeight,
            AbstainWeight = abstainWeight,
            TotalWeight = totalWeight,
            YesPercentage = totalWeight > 0 ? yesWeight / totalWeight : 0m,
            NoPercentage = totalWeight > 0 ? noWeight / totalWeight : 0m,
            AbstainPercentage = totalWeight > 0 ? abstainWeight / totalWeight : 0m
        };
    }

    public async Task<bool> CheckQuorumAsync(Guid proposalId, decimal requiredMajority)
    {
        var proposal = await _context.Proposals
            .Include(p => p.Group)
            .Include(p => p.Votes)
            .FirstOrDefaultAsync(p => p.Id == proposalId);

        if (proposal == null)
        {
            throw new KeyNotFoundException("Proposal not found");
        }

        var totalOwnership = await _context.GroupMembers
            .Where(m => m.GroupId == proposal.GroupId)
            .SumAsync(m => m.SharePercentage);

        var votedOwnership = proposal.Votes.Sum(v => v.Weight);
        var quorumThreshold = totalOwnership * requiredMajority;

        return votedOwnership >= quorumThreshold;
    }

    public async Task<bool> DetermineProposalOutcomeAsync(Guid proposalId)
    {
        var proposal = await _context.Proposals
            .Include(p => p.Votes)
            .FirstOrDefaultAsync(p => p.Id == proposalId);

        if (proposal == null)
        {
            throw new KeyNotFoundException("Proposal not found");
        }

        var voteTally = await CalculateVoteTallyAsync(proposalId);
        var quorumMet = await CheckQuorumAsync(proposalId, proposal.RequiredMajority);

        if (!quorumMet)
        {
            return false;
        }

        // Proposal passes if yes votes meet the required majority
        var yesPercentage = voteTally.TotalWeight > 0 
            ? voteTally.YesWeight / voteTally.TotalWeight 
            : 0m;

        return yesPercentage >= proposal.RequiredMajority;
    }

    public Task<bool> CanTransitionStatusAsync(Proposal proposal, ProposalStatus newStatus)
    {
        // State machine rules
        return Task.FromResult(proposal.Status switch
        {
            ProposalStatus.Active => newStatus == ProposalStatus.Passed ||
                                   newStatus == ProposalStatus.Rejected ||
                                   newStatus == ProposalStatus.Expired ||
                                   newStatus == ProposalStatus.Cancelled,
            ProposalStatus.Passed => false, // Cannot transition from Passed
            ProposalStatus.Rejected => false, // Cannot transition from Rejected
            ProposalStatus.Expired => false, // Cannot transition from Expired
            ProposalStatus.Cancelled => false, // Cannot transition from Cancelled
            _ => false
        });
    }

    public async Task<ProposalStatus> ResolveProposalStatusAsync(Proposal proposal)
    {
        // Check if voting period has ended
        if (DateTime.UtcNow > proposal.VotingEndDate && proposal.Status == ProposalStatus.Active)
        {
            var quorumMet = await CheckQuorumAsync(proposal.Id, proposal.RequiredMajority);
            
            if (!quorumMet)
            {
                _logger.LogInformation("Proposal {ProposalId} expired without quorum", proposal.Id);
                return ProposalStatus.Expired;
            }

            var passed = await DetermineProposalOutcomeAsync(proposal.Id);
            return passed ? ProposalStatus.Passed : ProposalStatus.Rejected;
        }

        // If still active and within voting period, return current status
        return proposal.Status;
    }

    public async Task ProcessExpiredProposalsAsync()
    {
        var expiredProposals = await _context.Proposals
            .Include(p => p.Votes)
            .Where(p => p.Status == ProposalStatus.Active && 
                       p.VotingEndDate < DateTime.UtcNow)
            .ToListAsync();

        foreach (var proposal in expiredProposals)
        {
            try
            {
                var newStatus = await ResolveProposalStatusAsync(proposal);
                
                if (newStatus != proposal.Status)
                {
                    var oldStatus = proposal.Status;
                    proposal.Status = newStatus;
                    proposal.UpdatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();

                    _logger.LogInformation(
                        "Proposal {ProposalId} status changed from {OldStatus} to {NewStatus}",
                        proposal.Id, oldStatus, newStatus);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing expired proposal {ProposalId}", proposal.Id);
            }
        }
    }
}

