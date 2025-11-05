using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Analytics.Api.Data.Entities;

public class BookingSuggestionFeedback : BaseEntity
{
	public Guid UserId { get; set; }
	public Guid GroupId { get; set; }
	public DateTime SuggestedStart { get; set; }
	public DateTime SuggestedEnd { get; set; }
	public bool Accepted { get; set; }
}









