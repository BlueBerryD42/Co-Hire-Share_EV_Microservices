using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CoOwnershipVehicle.Analytics.Api.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CoOwnershipVehicle.Analytics.Api.Services.HttpClients;

public class OpenAIServiceClient : IOpenAIServiceClient
{
	private readonly HttpClient _httpClient;
	private readonly IConfiguration _configuration;
	private readonly ILogger<OpenAIServiceClient> _logger;
	private readonly JsonSerializerOptions _jsonOptions;
	private readonly string _apiKey;
	private readonly string _baseUrl;
	private readonly string _model;
	private readonly int _maxTokens;
	private readonly double _temperature;
	private readonly bool _enableFallback;

	public OpenAIServiceClient(
		HttpClient httpClient,
		IConfiguration configuration,
		ILogger<OpenAIServiceClient> logger)
	{
		_httpClient = httpClient;
		_configuration = configuration;
		_logger = logger;
		_jsonOptions = new JsonSerializerOptions 
		{ 
			PropertyNameCaseInsensitive = true,
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase
		};

		_apiKey = _configuration["OpenAI:ApiKey"] ?? string.Empty;
		_baseUrl = _configuration["OpenAI:BaseUrl"] ?? "https://api.openai.com/v1";
		_model = _configuration["OpenAI:Model"] ?? "gpt-3.5-turbo";
		_maxTokens = int.Parse(_configuration["OpenAI:MaxTokens"] ?? "2000");
		_temperature = double.Parse(_configuration["OpenAI:Temperature"] ?? "0.7");
		_enableFallback = bool.Parse(_configuration["OpenAI:EnableFallback"] ?? "true");

		if (string.IsNullOrEmpty(_apiKey))
		{
			_logger.LogWarning("OpenAI API key is not configured. AI features will use fallback logic.");
		}
		else
		{
			_httpClient.BaseAddress = new Uri(_baseUrl);
			_httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
			_httpClient.DefaultRequestHeaders.Add("User-Agent", "CoOwnershipVehicle-Analytics-API");
			_httpClient.Timeout = TimeSpan.FromSeconds(60);
		}
	}

	public async Task<FairnessAnalysisResponse?> AnalyzeFairnessAsync(string prompt, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrEmpty(_apiKey))
		{
			_logger.LogWarning("OpenAI API key not configured. Returning null for fairness analysis.");
			return null;
		}

		return await CallOpenAIAsync<FairnessAnalysisResponse>(
			prompt,
			"fairness-analysis",
			cancellationToken);
	}

	public async Task<SuggestBookingResponse?> SuggestBookingTimesAsync(string prompt, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrEmpty(_apiKey))
		{
			_logger.LogWarning("OpenAI API key not configured. Returning null for booking suggestions.");
			return null;
		}

		return await CallOpenAIAsync<SuggestBookingResponse>(
			prompt,
			"booking-suggestions",
			cancellationToken);
	}

	public async Task<UsagePredictionResponse?> PredictUsageAsync(string prompt, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrEmpty(_apiKey))
		{
			_logger.LogWarning("OpenAI API key not configured. Returning null for usage predictions.");
			return null;
		}

		return await CallOpenAIAsync<UsagePredictionResponse>(
			prompt,
			"usage-predictions",
			cancellationToken);
	}

	public async Task<CostOptimizationResponse?> OptimizeCostsAsync(string prompt, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrEmpty(_apiKey))
		{
			_logger.LogWarning("OpenAI API key not configured. Returning null for cost optimization.");
			return null;
		}

		return await CallOpenAIAsync<CostOptimizationResponse>(
			prompt,
			"cost-optimization",
			cancellationToken);
	}

	private async Task<T?> CallOpenAIAsync<T>(
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
				var requestBody = new
				{
					model = _model,
					messages = new[]
					{
						new { role = "system", content = "You are a helpful AI assistant that analyzes data and returns JSON responses. Always return valid JSON only, no additional text. Do not include markdown code blocks, just return the raw JSON." },
						new { role = "user", content = prompt }
					},
					max_tokens = _maxTokens,
					temperature = _temperature,
					response_format = new { type = "json_object" }
				};

				var jsonContent = JsonSerializer.Serialize(requestBody);
				var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

				_logger.LogInformation("Calling OpenAI API for {Operation} (attempt {Attempt}/{MaxRetries})", operationName, attempt, maxRetries);

				var response = await _httpClient.PostAsync("chat/completions", content, cancellationToken);

				if (response.IsSuccessStatusCode)
				{
					var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
					var responseJson = JsonSerializer.Deserialize<OpenAIResponse>(responseContent, _jsonOptions);

					if (responseJson?.Choices != null && responseJson.Choices.Count > 0)
					{
						var aiResponse = responseJson.Choices[0].Message?.Content;
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
					_logger.LogWarning("OpenAI API returned error status {StatusCode} for {Operation}: {Error}", 
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
				_logger.LogError(ex, "OpenAI API call timeout for {Operation} (attempt {Attempt})", operationName, attempt);
				if (attempt == maxRetries)
				{
					return null;
				}
				await Task.Delay(retryDelay, cancellationToken);
				retryDelay = TimeSpan.FromSeconds(retryDelay.TotalSeconds * 2);
			}
			catch (HttpRequestException ex)
			{
				_logger.LogError(ex, "HTTP error calling OpenAI API for {Operation} (attempt {Attempt})", operationName, attempt);
				if (attempt == maxRetries)
				{
					return null;
				}
				await Task.Delay(retryDelay, cancellationToken);
				retryDelay = TimeSpan.FromSeconds(retryDelay.TotalSeconds * 2);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Unexpected error calling OpenAI API for {Operation} (attempt {Attempt})", operationName, attempt);
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

	// OpenAI API response models
	private class OpenAIResponse
	{
		public List<Choice> Choices { get; set; } = new();
	}

	private class Choice
	{
		public Message? Message { get; set; }
	}

	private class Message
	{
		public string? Content { get; set; }
	}
}

