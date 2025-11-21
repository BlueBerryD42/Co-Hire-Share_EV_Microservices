using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CoOwnershipVehicle.Analytics.Api.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CoOwnershipVehicle.Analytics.Api.Services.HttpClients;

public class GeminiServiceClient : IOpenAIServiceClient
{
	private readonly HttpClient _httpClient;
	private readonly IConfiguration _configuration;
	private readonly ILogger<GeminiServiceClient> _logger;
	private readonly JsonSerializerOptions _jsonOptions;
	private readonly string _apiKey;
	private readonly string _baseUrl;
	private readonly string _model;
	private readonly int _maxTokens;
	private readonly double _temperature;
	private readonly bool _enableFallback;

	public GeminiServiceClient(
		HttpClient httpClient,
		IConfiguration configuration,
		ILogger<GeminiServiceClient> logger)
	{
		_httpClient = httpClient;
		_configuration = configuration;
		_logger = logger;
		_jsonOptions = new JsonSerializerOptions 
		{ 
			PropertyNameCaseInsensitive = true,
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase
		};

		_apiKey = _configuration["Gemini:ApiKey"] ?? string.Empty;
		_baseUrl = _configuration["Gemini:BaseUrl"] ?? "https://generativelanguage.googleapis.com/v1beta";
		_model = _configuration["Gemini:Model"] ?? "gemini-1.5-flash";
		_maxTokens = int.Parse(_configuration["Gemini:MaxTokens"] ?? "2000");
		_temperature = double.Parse(_configuration["Gemini:Temperature"] ?? "0.7");
		_enableFallback = bool.Parse(_configuration["Gemini:EnableFallback"] ?? "true");

		if (string.IsNullOrEmpty(_apiKey))
		{
			_logger.LogWarning("Gemini API key is not configured. AI features will use fallback logic.");
		}
		else
		{
			_httpClient.BaseAddress = new Uri(_baseUrl);
			_httpClient.DefaultRequestHeaders.Add("User-Agent", "CoOwnershipVehicle-Analytics-API");
			_httpClient.Timeout = TimeSpan.FromSeconds(60);
		}
	}

	public async Task<FairnessAnalysisResponse?> AnalyzeFairnessAsync(string prompt, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrEmpty(_apiKey))
		{
			_logger.LogWarning("Gemini API key not configured. Returning null for fairness analysis.");
			return null;
		}

		return await CallGeminiAsync<FairnessAnalysisResponse>(
			prompt,
			"fairness-analysis",
			cancellationToken);
	}

	public async Task<SuggestBookingResponse?> SuggestBookingTimesAsync(string prompt, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrEmpty(_apiKey))
		{
			_logger.LogWarning("Gemini API key not configured. Returning null for booking suggestions.");
			return null;
		}

		return await CallGeminiAsync<SuggestBookingResponse>(
			prompt,
			"booking-suggestions",
			cancellationToken);
	}

	public async Task<UsagePredictionResponse?> PredictUsageAsync(string prompt, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrEmpty(_apiKey))
		{
			_logger.LogWarning("Gemini API key not configured. Returning null for usage predictions.");
			return null;
		}

		return await CallGeminiAsync<UsagePredictionResponse>(
			prompt,
			"usage-predictions",
			cancellationToken);
	}

	public async Task<CostOptimizationResponse?> OptimizeCostsAsync(string prompt, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrEmpty(_apiKey))
		{
			_logger.LogWarning("Gemini API key not configured. Returning null for cost optimization.");
			return null;
		}

		return await CallGeminiAsync<CostOptimizationResponse>(
			prompt,
			"cost-optimization",
			cancellationToken);
	}

	private async Task<T?> CallGeminiAsync<T>(
		string prompt,
		string operationName,
		CancellationToken cancellationToken) where T : class
	{
		const int maxRetries = 3;
		var retryDelay = TimeSpan.FromSeconds(1);

		for (int attempt = 1; attempt <= maxRetries; attempt++)
		{
			try
			{
				// Gemini API request format
				var requestBody = new
				{
					contents = new[]
					{
						new
						{
							parts = new[]
							{
								new
								{
									text = $"You are a helpful AI assistant that analyzes data and returns JSON responses. Always return valid JSON only, no additional text. Do not include markdown code blocks, just return the raw JSON.\n\n{prompt}"
								}
							}
						}
					},
					generationConfig = new
					{
						temperature = _temperature,
						maxOutputTokens = _maxTokens,
						responseMimeType = "application/json"
					}
				};

				var jsonContent = JsonSerializer.Serialize(requestBody);
				var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

				_logger.LogInformation("Calling Gemini API for {Operation} (attempt {Attempt}/{MaxRetries})", operationName, attempt, maxRetries);

				// Gemini API endpoint: /models/{model}:generateContent?key={apiKey}
				var endpoint = $"models/{_model}:generateContent?key={_apiKey}";
				var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);

				if (response.IsSuccessStatusCode)
				{
					var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
					var responseJson = JsonSerializer.Deserialize<GeminiResponse>(responseContent, _jsonOptions);

					if (responseJson?.Candidates != null && responseJson.Candidates.Count > 0)
					{
						var aiResponse = responseJson.Candidates[0].Content?.Parts?[0]?.Text;
						if (!string.IsNullOrEmpty(aiResponse))
						{
							try
							{
								// Try to extract JSON from the response (AI might wrap it in markdown)
								var jsonText = ExtractJsonFromResponse(aiResponse);
								var result = JsonSerializer.Deserialize<T>(jsonText, _jsonOptions);
								
								_logger.LogInformation("Successfully received AI response for {Operation}", operationName);
								return result;
							}
							catch (JsonException ex)
							{
								_logger.LogError(ex, "Failed to parse AI response as JSON for {Operation}. Response: {Response}", operationName, aiResponse);
								if (attempt == maxRetries)
								{
									return null;
								}
							}
						}
					}
				}
				else
				{
					var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
					_logger.LogWarning("Gemini API returned error status {StatusCode} for {Operation}: {Error}", 
						response.StatusCode, operationName, errorContent);

					// Don't retry on client errors (4xx)
					if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
					{
						return null;
					}

					// Retry on server errors (5xx) or rate limits (429)
					if (attempt < maxRetries && ((int)response.StatusCode >= 500 || response.StatusCode == System.Net.HttpStatusCode.TooManyRequests))
					{
						_logger.LogInformation("Retrying {Operation} after delay (attempt {Attempt}/{MaxRetries})", operationName, attempt, maxRetries);
						await Task.Delay(retryDelay, cancellationToken);
						retryDelay = TimeSpan.FromSeconds(retryDelay.TotalSeconds * 2); // Exponential backoff
						continue;
					}

					return null;
				}
			}
			catch (TaskCanceledException ex)
			{
				_logger.LogError(ex, "Gemini API call timeout for {Operation} (attempt {Attempt})", operationName, attempt);
				if (attempt == maxRetries)
				{
					return null;
				}
				await Task.Delay(retryDelay, cancellationToken);
				retryDelay = TimeSpan.FromSeconds(retryDelay.TotalSeconds * 2);
			}
			catch (HttpRequestException ex)
			{
				_logger.LogError(ex, "HTTP error calling Gemini API for {Operation} (attempt {Attempt})", operationName, attempt);
				if (attempt == maxRetries)
				{
					return null;
				}
				await Task.Delay(retryDelay, cancellationToken);
				retryDelay = TimeSpan.FromSeconds(retryDelay.TotalSeconds * 2);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Unexpected error calling Gemini API for {Operation} (attempt {Attempt})", operationName, attempt);
				if (attempt == maxRetries)
				{
					return null;
				}
				await Task.Delay(retryDelay, cancellationToken);
				retryDelay = TimeSpan.FromSeconds(retryDelay.TotalSeconds * 2);
			}
		}

		_logger.LogWarning("Failed to get AI response for {Operation} after {MaxRetries} attempts", operationName, maxRetries);
		return null;
	}

	private string ExtractJsonFromResponse(string response)
	{
		// Remove markdown code blocks if present
		var json = response.Trim();
		if (json.StartsWith("```json"))
		{
			json = json.Substring(7);
		}
		if (json.StartsWith("```"))
		{
			json = json.Substring(3);
		}
		if (json.EndsWith("```"))
		{
			json = json.Substring(0, json.Length - 3);
		}
		return json.Trim();
	}

	// Gemini API response models
	private class GeminiResponse
	{
		public List<GeminiCandidate>? Candidates { get; set; }
	}

	private class GeminiCandidate
	{
		public GeminiContent? Content { get; set; }
	}

	private class GeminiContent
	{
		public List<GeminiPart>? Parts { get; set; }
	}

	private class GeminiPart
	{
		public string? Text { get; set; }
	}
}

