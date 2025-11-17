using System.Net.Http.Headers;
using System.Text.Json;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using Microsoft.AspNetCore.Http;

namespace CoOwnershipVehicle.Analytics.Api.Services.HttpClients;

public class PaymentServiceClient : IPaymentServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<PaymentServiceClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public PaymentServiceClient(
        HttpClient httpClient,
        IHttpContextAccessor httpContextAccessor,
        ILogger<PaymentServiceClient> logger)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    }

    private void SetAuthorizationHeader()
    {
        var token = _httpContextAccessor.HttpContext?.Request.Headers["Authorization"].ToString();
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(token);
        }
    }

    public async Task<List<ExpenseDto>> GetExpensesAsync(Guid? groupId = null, DateTime? from = null, DateTime? to = null)
    {
        try
        {
            SetAuthorizationHeader();
            var queryParams = new List<string>();
            
            if (groupId.HasValue)
                queryParams.Add($"groupId={groupId.Value}");

            var queryString = queryParams.Any() ? "?" + string.Join("&", queryParams) : "";
            var response = await _httpClient.GetAsync($"api/Payment/expenses{queryString}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var expenses = JsonSerializer.Deserialize<List<ExpenseDto>>(content, _jsonOptions) ?? new List<ExpenseDto>();
                
                // Filter by date range if provided
                if (from.HasValue)
                    expenses = expenses.Where(e => e.DateIncurred >= from.Value).ToList();
                if (to.HasValue)
                    expenses = expenses.Where(e => e.DateIncurred <= to.Value).ToList();
                
                return expenses;
            }
            else
            {
                _logger.LogWarning("Failed to get expenses. Status: {StatusCode}", response.StatusCode);
                return new List<ExpenseDto>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Payment service to get expenses");
            return new List<ExpenseDto>();
        }
    }
}

