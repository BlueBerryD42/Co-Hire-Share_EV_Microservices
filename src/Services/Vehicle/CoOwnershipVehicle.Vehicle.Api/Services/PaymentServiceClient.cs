using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CoOwnershipVehicle.Vehicle.Api.Services
{
    /// <summary>
    /// HTTP client for communicating with Payment Service
    /// </summary>
    public class PaymentServiceClient : IPaymentServiceClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<PaymentServiceClient> _logger;

        public PaymentServiceClient(HttpClient httpClient, IConfiguration configuration, ILogger<PaymentServiceClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;

            var paymentServiceUrl = configuration["ServiceUrls:PaymentService"];
            if (!string.IsNullOrEmpty(paymentServiceUrl))
            {
                _httpClient.BaseAddress = new Uri(paymentServiceUrl);
            }
            else
            {
                _logger.LogWarning("PaymentService URL is not configured. Using default: https://localhost:61605");
                _httpClient.BaseAddress = new Uri("https://localhost:61605");
            }
        }

        public async Task<VehicleExpensesResponse?> GetVehicleExpensesAsync(
            Guid vehicleId,
            DateTime startDate,
            DateTime endDate,
            string accessToken)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var url = $"/api/payment/vehicle/{vehicleId}/expenses?startDate={startDate:o}&endDate={endDate:o}";
                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                    };
                    options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
                    return JsonSerializer.Deserialize<VehicleExpensesResponse>(content, options);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning("No expenses found for vehicle {VehicleId}", vehicleId);
                    return new VehicleExpensesResponse
                    {
                        VehicleId = vehicleId,
                        StartDate = startDate,
                        EndDate = endDate,
                        Expenses = new(),
                        TotalAmount = 0
                    };
                }
                else
                {
                    _logger.LogError("Failed to get expenses for vehicle {VehicleId}. Status code: {StatusCode}",
                        vehicleId, response.StatusCode);
                    return null;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Payment Service is not available. Returning empty expenses for vehicle {VehicleId}", vehicleId);
                // Return empty data if service is unavailable (graceful degradation)
                return new VehicleExpensesResponse
                {
                    VehicleId = vehicleId,
                    StartDate = startDate,
                    EndDate = endDate,
                    Expenses = new(),
                    TotalAmount = 0
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while getting expenses for vehicle {VehicleId}", vehicleId);
                return null;
            }
        }

        public async Task<VehicleBudgetResponse?> GetVehicleBudgetAsync(Guid vehicleId, string accessToken)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var response = await _httpClient.GetAsync($"/api/payment/vehicle/{vehicleId}/budget");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                    };
                    return JsonSerializer.Deserialize<VehicleBudgetResponse>(content, options);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogInformation("No budget set for vehicle {VehicleId}", vehicleId);
                    return new VehicleBudgetResponse
                    {
                        VehicleId = vehicleId,
                        HasBudget = false,
                        MonthlyBudget = 0
                    };
                }
                else
                {
                    _logger.LogError("Failed to get budget for vehicle {VehicleId}. Status code: {StatusCode}",
                        vehicleId, response.StatusCode);
                    return null;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Payment Service is not available. Returning no budget for vehicle {VehicleId}", vehicleId);
                return new VehicleBudgetResponse
                {
                    VehicleId = vehicleId,
                    HasBudget = false,
                    MonthlyBudget = 0
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred while getting budget for vehicle {VehicleId}", vehicleId);
                return null;
            }
        }
    }
}
