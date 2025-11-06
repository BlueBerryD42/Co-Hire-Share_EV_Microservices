using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Shared.Contracts.DTOs;

namespace CoOwnershipVehicle.Group.Api.Contracts;

public interface IVotingService
{
    Task<VoteTallyDto> CalculateVoteTallyAsync(Guid proposalId);
    Task<bool> CheckQuorumAsync(Guid proposalId, decimal requiredMajority);
    Task<bool> DetermineProposalOutcomeAsync(Guid proposalId);
    Task<bool> CanTransitionStatusAsync(Proposal proposal, ProposalStatus newStatus);
    Task<ProposalStatus> ResolveProposalStatusAsync(Proposal proposal);
    Task ProcessExpiredProposalsAsync();
}

