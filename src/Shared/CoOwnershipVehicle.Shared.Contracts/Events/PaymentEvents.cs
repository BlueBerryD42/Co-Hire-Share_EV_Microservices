using CoOwnershipVehicle.Shared.Contracts.DTOs;

namespace CoOwnershipVehicle.Shared.Contracts.Events;

public class ExpenseCreatedEvent : BaseEvent
{
    public Guid ExpenseId { get; set; }
    public Guid GroupId { get; set; }
    public Guid? VehicleId { get; set; }
    public ExpenseType ExpenseType { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime DateIncurred { get; set; }
    public Guid CreatedBy { get; set; }
    
    public ExpenseCreatedEvent()
    {
        EventType = nameof(ExpenseCreatedEvent);
    }
}

public class PaymentSettledEvent : BaseEvent
{
    public Guid PaymentId { get; set; }
    public Guid InvoiceId { get; set; }
    public Guid ExpenseId { get; set; }
    public Guid PayerId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public string? TransactionReference { get; set; }
    public DateTime PaidAt { get; set; }
    
    public PaymentSettledEvent()
    {
        EventType = nameof(PaymentSettledEvent);
    }
}

public class DocumentSignedEvent : BaseEvent
{
    public Guid DocumentId { get; set; }
    public Guid GroupId { get; set; }
    public Guid SignerId { get; set; }
    public DateTime SignedAt { get; set; }
    public string SignatureReference { get; set; } = string.Empty;
    public bool IsFullySigned { get; set; }
    
    public DocumentSignedEvent()
    {
        EventType = nameof(DocumentSignedEvent);
    }
}

public class VoteCreatedEvent : BaseEvent
{
    public Guid VoteId { get; set; }
    public Guid ProposalId { get; set; }
    public Guid GroupId { get; set; }
    public Guid VoterId { get; set; }
    public decimal Weight { get; set; }
    public VoteChoice Choice { get; set; }
    public DateTime VotedAt { get; set; }
    
    public VoteCreatedEvent()
    {
        EventType = nameof(VoteCreatedEvent);
    }
}

public class VoteClosedEvent : BaseEvent
{
    public Guid ProposalId { get; set; }
    public Guid GroupId { get; set; }
    public bool Passed { get; set; }
    public decimal YesPercentage { get; set; }
    public decimal NoPercentage { get; set; }
    public DateTime ClosedAt { get; set; }
    
    public VoteClosedEvent()
    {
        EventType = nameof(VoteClosedEvent);
    }
}

public enum VoteChoice
{
    Yes = 0,
    No = 1,
    Abstain = 2
}
