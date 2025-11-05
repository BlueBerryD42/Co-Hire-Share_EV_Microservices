using CoOwnershipVehicle.Analytics.Api.Models;

namespace CoOwnershipVehicle.Analytics.Api.Services;

public interface IAIService
{
	Task<FairnessAnalysisResponse?> CalculateFairnessAsync(Guid groupId, DateTime? startDate, DateTime? endDate);
	Task<SuggestBookingResponse?> SuggestBookingTimesAsync(SuggestBookingRequest request);
	Task<UsagePredictionResponse?> GetUsagePredictionsAsync(Guid groupId);
	Task<CostOptimizationResponse?> GetCostOptimizationAsync(Guid groupId);
}


