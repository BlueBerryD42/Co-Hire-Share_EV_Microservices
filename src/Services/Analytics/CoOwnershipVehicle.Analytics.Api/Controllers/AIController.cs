using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CoOwnershipVehicle.Analytics.Api.Services;
using CoOwnershipVehicle.Analytics.Api.Models;

namespace CoOwnershipVehicle.Analytics.Api.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize]
public class AIController : ControllerBase
{
	private readonly IAIService _aiService;

	public AIController(IAIService aiService)
	{
		_aiService = aiService;
	}

	/// <summary>
	/// Calculate fairness scores for an ownership group based on usage vs ownership.
	/// </summary>
	[HttpGet("fairness-score/{groupId}")]
	[ProducesResponseType(typeof(FairnessAnalysisResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public async Task<ActionResult<FairnessAnalysisResponse>> GetFairnessScore(Guid groupId, [FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null)
	{
		var result = await _aiService.CalculateFairnessAsync(groupId, startDate, endDate);
		if (result == null)
		{
			return NotFound(new { message = "Group not found or no analytics data available" });
		}
		return Ok(result);
	}

	/// <summary>
	/// Predict group usage for the next 30 days with patterns and insights.
	/// </summary>
	[HttpGet("usage-predictions/{groupId}")]
	[ProducesResponseType(typeof(UsagePredictionResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public async Task<ActionResult<UsagePredictionResponse>> GetUsagePredictions(Guid groupId)
	{
		var result = await _aiService.GetUsagePredictionsAsync(groupId);
		if (result == null)
		{
			return NotFound(new { message = "Group not found or insufficient data" });
		}
		return Ok(result);
	}

	/// <summary>
	/// Suggest optimal booking time slots to balance usage and fairness.
	/// </summary>
	[HttpPost("suggest-booking-time")]
	[ProducesResponseType(typeof(SuggestBookingResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public async Task<ActionResult<SuggestBookingResponse>> SuggestBookingTime([FromBody] SuggestBookingRequest request)
	{
		var result = await _aiService.SuggestBookingTimesAsync(request);
		if (result == null)
		{
			return NotFound(new { message = "User or group not found or insufficient data" });
		}
		return Ok(result);
	}

	/// <summary>
	/// Get AI-powered cost optimization recommendations, predictions, alerts, and ROI calculations for a group.
	/// </summary>
	[HttpGet("cost-optimization/{groupId}")]
	[ProducesResponseType(typeof(CostOptimizationResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public async Task<ActionResult<CostOptimizationResponse>> GetCostOptimization(Guid groupId)
	{
		var result = await _aiService.GetCostOptimizationAsync(groupId);
		if (result == null)
		{
			return NotFound(new { message = "Group not found or insufficient data" });
		}
		return Ok(result);
	}

	/// <summary>
	/// Get predictive maintenance analysis for a vehicle including health score, predicted issues, and maintenance recommendations.
	/// </summary>
	[HttpGet("predictive-maintenance/{vehicleId}")]
	[ProducesResponseType(typeof(PredictiveMaintenanceResponse), StatusCodes.Status200OK)]
	[ProducesResponseType(StatusCodes.Status404NotFound)]
	[ProducesResponseType(StatusCodes.Status403Forbidden)]
	public async Task<ActionResult<PredictiveMaintenanceResponse>> GetPredictiveMaintenance(Guid vehicleId)
	{
		var result = await _aiService.GetPredictiveMaintenanceAsync(vehicleId);
		if (result == null)
		{
			return NotFound(new { message = "Vehicle not found" });
		}
		return Ok(result);
	}
}


