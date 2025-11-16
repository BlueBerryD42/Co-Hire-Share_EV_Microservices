namespace CoOwnershipVehicle.Analytics.Api.Models;

public class SuggestBookingRequest
{
	public Guid UserId { get; set; }
	public Guid GroupId { get; set; }
	public DateTime? PreferredDate { get; set; }
	public int DurationMinutes { get; set; }
}

public class SuggestBookingResponse
{
	public Guid UserId { get; set; }
	public Guid GroupId { get; set; }
	public List<BookingSuggestion> Suggestions { get; set; } = new();
}

public class BookingSuggestion
{
	public DateTime Start { get; set; }
	public DateTime End { get; set; }
	public decimal Confidence { get; set; } // 0-1
	public List<string> Reasons { get; set; } = new();
}











