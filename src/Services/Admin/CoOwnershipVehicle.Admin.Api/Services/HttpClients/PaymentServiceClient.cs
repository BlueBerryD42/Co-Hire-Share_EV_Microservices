using System.Net.Http.Headers;
using System.Text.Json;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using Microsoft.AspNetCore.Http;

namespace CoOwnershipVehicle.Admin.Api.Services.HttpClients;

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

    public async Task<List<PaymentDto>> GetPaymentsAsync(DateTime? from = null, DateTime? to = null, PaymentStatus? status = null)
    {
        try
        {
            SetAuthorizationHeader();
            // Note: Payment service may need admin endpoints for getting all payments
            // For now, we'll try to get payments through available endpoints
            var response = await _httpClient.GetAsync("api/Payment/payments");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var payments = JsonSerializer.Deserialize<List<PaymentDto>>(content, _jsonOptions) ?? new List<PaymentDto>();
                
                // Filter by date range and status if provided
                if (from.HasValue)
                    payments = payments.Where(p => p.CreatedAt >= from.Value).ToList();
                if (to.HasValue)
                    payments = payments.Where(p => p.CreatedAt <= to.Value).ToList();
                if (status.HasValue)
                    payments = payments.Where(p => p.Status == status.Value).ToList();
                
                return payments;
            }
            else
            {
                _logger.LogWarning("Failed to get payments. Status: {StatusCode}", response.StatusCode);
                return new List<PaymentDto>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Payment service to get payments");
            return new List<PaymentDto>();
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

    public async Task<decimal> GetTotalRevenueAsync(DateTime? from = null, DateTime? to = null)
    {
        try
        {
            var payments = await GetPaymentsAsync(from, to, PaymentStatus.Completed);
            return payments.Sum(p => p.Amount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating total revenue");
            return 0;
        }
    }

    public async Task<decimal> GetTotalExpensesAsync(Guid? groupId = null, DateTime? from = null, DateTime? to = null)
    {
        try
        {
            var expenses = await GetExpensesAsync(groupId, from, to);
            return expenses.Sum(e => e.Amount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating total expenses");
            return 0;
        }
    }
}

