using System.ComponentModel.DataAnnotations;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Shared.Contracts.DTOs;

public class CreateProposalDto
{
    [Required]
    public Guid GroupId { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public ProposalType Type { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? Amount { get; set; }

    [Required]
    public DateTime VotingStartDate { get; set; }

    [Required]
    public DateTime VotingEndDate { get; set; }

    [Range(0.0001, 1.0000)]
    public decimal RequiredMajority { get; set; } = 0.5m;
}

public class ProposalDto
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public Guid CreatedBy { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ProposalType Type { get; set; }
    public decimal? Amount { get; set; }
    public ProposalStatus Status { get; set; }
    public DateTime VotingStartDate { get; set; }
    public DateTime VotingEndDate { get; set; }
    public decimal RequiredMajority { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string CreatorName { get; set; } = string.Empty;
}

public class ProposalListDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ProposalType Type { get; set; }
    public ProposalStatus Status { get; set; }
    public decimal? Amount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime VotingEndDate { get; set; }
    public int TotalVotes { get; set; }
    public int YesVotes { get; set; }
    public int NoVotes { get; set; }
    public int AbstainVotes { get; set; }
    public decimal VotingProgress { get; set; } // Percentage of ownership that has voted
}

public class ProposalDetailsDto
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public Guid CreatedBy { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ProposalType Type { get; set; }
    public decimal? Amount { get; set; }
    public ProposalStatus Status { get; set; }
    public DateTime VotingStartDate { get; set; }
    public DateTime VotingEndDate { get; set; }
    public decimal RequiredMajority { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string CreatorName { get; set; } = string.Empty;
    public List<VoteDto> Votes { get; set; } = new();
    public VoteTallyDto VoteTally { get; set; } = new();
    public decimal VotingProgress { get; set; } // Percentage of ownership that has voted
    public TimeSpan? TimeRemaining { get; set; }
}

public class CastVoteDto
{
    [Required]
    public VoteChoice Choice { get; set; }

    [StringLength(500)]
    public string? Comment { get; set; }
}

public class VoteDto
{
    public Guid Id { get; set; }
    public Guid ProposalId { get; set; }
    public Guid VoterId { get; set; }
    public string VoterName { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public VoteChoice Choice { get; set; }
    public string? Comment { get; set; }
    public DateTime VotedAt { get; set; }
}

public class VoteTallyDto
{
    public decimal YesWeight { get; set; }
    public decimal NoWeight { get; set; }
    public decimal AbstainWeight { get; set; }
    public decimal TotalWeight { get; set; }
    public decimal YesPercentage { get; set; }
    public decimal NoPercentage { get; set; }
    public decimal AbstainPercentage { get; set; }
}

public class ProposalResultsDto
{
    public Guid ProposalId { get; set; }
    public ProposalStatus Status { get; set; }
    public VoteTallyDto VoteTally { get; set; } = new();
    public bool QuorumMet { get; set; }
    public decimal QuorumPercentage { get; set; }
    public decimal RequiredMajority { get; set; }
    public bool Passed { get; set; }
    public List<VoteBreakdownDto> VoteBreakdown { get; set; } = new();
    public DateTime? ClosedAt { get; set; }
}

public class VoteBreakdownDto
{
    public Guid VoterId { get; set; }
    public string VoterName { get; set; } = string.Empty;
    public VoteChoice Choice { get; set; }
    public decimal Weight { get; set; }
    public decimal WeightPercentage { get; set; }
    public DateTime VotedAt { get; set; }
    public string? Comment { get; set; }
}

