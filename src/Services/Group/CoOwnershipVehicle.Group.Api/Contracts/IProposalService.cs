using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Shared.Contracts.DTOs;

namespace CoOwnershipVehicle.Group.Api.Contracts;

public interface IProposalService
{
    Task<ProposalDto> CreateProposalAsync(CreateProposalDto createDto, Guid userId);
    Task<List<ProposalListDto>> GetProposalsByGroupAsync(Guid groupId, Guid userId, ProposalStatus? status = null);
    Task<ProposalDetailsDto> GetProposalByIdAsync(Guid proposalId, Guid userId);
    Task<VoteDto> CastVoteAsync(Guid proposalId, CastVoteDto voteDto, Guid userId);
    Task<ProposalResultsDto> GetProposalResultsAsync(Guid proposalId, Guid userId);
    Task<ProposalDto> CloseProposalAsync(Guid proposalId, Guid userId);
    Task<bool> CancelProposalAsync(Guid proposalId, Guid userId);
}

