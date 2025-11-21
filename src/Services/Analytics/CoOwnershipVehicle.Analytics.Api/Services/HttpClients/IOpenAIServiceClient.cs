using CoOwnershipVehicle.Analytics.Api.Models;

namespace CoOwnershipVehicle.Analytics.Api.Services.HttpClients;

public interface IOpenAIServiceClient
{
	Task<FairnessAnalysisResponse?> AnalyzeFairnessAsync(string prompt, CancellationToken cancellationToken = default);
	Task<SuggestBookingResponse?> SuggestBookingTimesAsync(string prompt, CancellationToken cancellationToken = default);
	Task<UsagePredictionResponse?> PredictUsageAsync(string prompt, CancellationToken cancellationToken = default);
	Task<CostOptimizationResponse?> OptimizeCostsAsync(string prompt, CancellationToken cancellationToken = default);
	Task<PredictiveMaintenanceResponse?> GetPredictiveMaintenanceAsync(string prompt, CancellationToken cancellationToken = default);
}


