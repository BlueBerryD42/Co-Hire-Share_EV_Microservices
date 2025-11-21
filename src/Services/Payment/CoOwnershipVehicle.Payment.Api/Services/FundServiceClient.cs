using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CoOwnershipVehicle.Payment.Api.Services.Interfaces;
using CoOwnershipVehicle.Shared.Contracts.DTOs;

namespace CoOwnershipVehicle.Payment.Api.Services;

/// <summary>
/// HTTP client implementation for communicating with Group Service's Fund endpoints
/// Handles expense payments from group fund
/// </summary>
public class FundServiceClient : IFundServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FundServiceClient> _logger;

    public FundServiceClient(HttpClient httpClient, ILogger<FundServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<FundBalanceDto?> GetFundBalanceAsync(Guid groupId, string accessToken)
    {
        try
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.GetAsync($"api/Fund/{groupId}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                };
                return JsonSerializer.Deserialize<FundBalanceDto>(content, options);
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Fund not found for group {GroupId}", groupId);
                return null;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to get fund balance for group {GroupId}. Status: {StatusCode}, Response: {Response}",
                    groupId, response.StatusCode, errorContent);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting fund balance for group {GroupId}", groupId);
            return null;
        }
    }

    public async Task<FundTransactionDto?> PayExpenseFromFundAsync(
        Guid groupId,
        Guid expenseId,
        decimal amount,
        string description,
        Guid initiatedBy,
        string accessToken)
    {
        try
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            // Create request payload for expense payment
            var requestPayload = new PayExpenseFromFundDto
            {
                ExpenseId = expenseId,
                Amount = amount,
                Description = description
            };

            var json = JsonSerializer.Serialize(requestPayload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Call Group Service's expense payment endpoint
            // Note: This endpoint needs to be created in Group Service's FundController
            var response = await _httpClient.PostAsync($"api/Fund/{groupId}/pay-expense", content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                };
                return JsonSerializer.Deserialize<FundTransactionDto>(responseContent, options);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to pay expense {ExpenseId} from fund for group {GroupId}. Status: {StatusCode}, Response: {Response}",
                    expenseId, groupId, response.StatusCode, errorContent);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error paying expense {ExpenseId} from fund for group {GroupId}", expenseId, groupId);
            return null;
        }
    }

    public async Task<bool> HasSufficientBalanceAsync(Guid groupId, decimal amount, string accessToken)
    {
        try
        {
            var balance = await GetFundBalanceAsync(groupId, accessToken);
            return balance != null && balance.AvailableBalance >= amount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking fund balance for group {GroupId}", groupId);
            return false;
        }
    }

    public async Task<FundTransactionDto?> CompleteFundDepositAsync(
        Guid groupId,
        decimal amount,
        string description,
        string paymentReference,
        Guid initiatedBy,
        string? reference,
        string accessToken)
    {
        try
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            // Create request payload for completing deposit
            var requestPayload = new CompleteFundDepositDto
            {
                GroupId = groupId,
                Amount = amount,
                Description = description,
                PaymentReference = paymentReference,
                InitiatedBy = initiatedBy,
                Reference = reference
            };

            var json = JsonSerializer.Serialize(requestPayload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Call Group Service's complete deposit endpoint
            var response = await _httpClient.PostAsync($"api/Fund/{groupId}/complete-deposit", content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                };
                return JsonSerializer.Deserialize<FundTransactionDto>(responseContent, options);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to complete fund deposit for group {GroupId}. Status: {StatusCode}, Response: {Response}",
                    groupId, response.StatusCode, errorContent);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing fund deposit for group {GroupId}", groupId);
            return null;
        }
    }
}

