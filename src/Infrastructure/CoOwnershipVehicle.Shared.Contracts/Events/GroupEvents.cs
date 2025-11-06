using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Shared.Contracts.Events;

public class ProposalCreatedEvent : BaseEvent
{
    public Guid ProposalId { get; set; }
    public Guid GroupId { get; set; }
    public Guid CreatedBy { get; set; }
    public string Title { get; set; } = string.Empty;
    public ProposalType Type { get; set; }
    public DateTime VotingEndDate { get; set; }

    public ProposalCreatedEvent()
    {
        EventType = nameof(ProposalCreatedEvent);
    }
}

public class ProposalClosedEvent : BaseEvent
{
    public Guid ProposalId { get; set; }
    public Guid GroupId { get; set; }
    public bool Passed { get; set; }
    public decimal YesPercentage { get; set; }
    public decimal NoPercentage { get; set; }
    public DateTime ClosedAt { get; set; }

    public ProposalClosedEvent()
    {
        EventType = nameof(ProposalClosedEvent);
    }
}

public class ProposalCancelledEvent : BaseEvent
{
    public Guid ProposalId { get; set; }
    public Guid GroupId { get; set; }
    public Guid CancelledBy { get; set; }
    public DateTime CancelledAt { get; set; }

    public ProposalCancelledEvent()
    {
        EventType = nameof(ProposalCancelledEvent);
    }
}

public class FundDepositEvent : BaseEvent
{
    public Guid TransactionId { get; set; }
    public Guid GroupId { get; set; }
    public Guid DepositedBy { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime DepositedAt { get; set; }

    public FundDepositEvent()
    {
        EventType = nameof(FundDepositEvent);
    }
}

public class FundWithdrawalEvent : BaseEvent
{
    public Guid TransactionId { get; set; }
    public Guid GroupId { get; set; }
    public Guid WithdrawnBy { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public string Reason { get; set; } = string.Empty;
    public FundTransactionStatus Status { get; set; }
    public DateTime WithdrawnAt { get; set; }

    public FundWithdrawalEvent()
    {
        EventType = nameof(FundWithdrawalEvent);
    }
}

public class FundAllocationEvent : BaseEvent
{
    public Guid TransactionId { get; set; }
    public Guid GroupId { get; set; }
    public Guid AllocatedBy { get; set; }
    public decimal Amount { get; set; }
    public FundTransactionType Type { get; set; } // Allocation or Release
    public decimal ReserveBalanceAfter { get; set; }
    public decimal AvailableBalanceAfter { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime AllocatedAt { get; set; }

    public FundAllocationEvent()
    {
        EventType = nameof(FundAllocationEvent);
    }
}

