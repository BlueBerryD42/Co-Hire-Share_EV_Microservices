using System.Net.Http.Headers;
using System.Text.Json;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using Microsoft.AspNetCore.Http;

namespace CoOwnershipVehicle.Analytics.Api.Services.HttpClients;

public class VehicleServiceClient : IVehicleServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<VehicleServiceClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public VehicleServiceClient(
        HttpClient httpClient,
        IHttpContextAccessor httpContextAccessor,
        ILogger<VehicleServiceClient> logger)
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

    public async Task<List<VehicleDto>> GetVehiclesAsync()
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.GetAsync("api/Vehicle");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                // Vehicle service might return VehicleListItemDto, need to check
                // For now, try to deserialize as VehicleDto list
                var vehicles = JsonSerializer.Deserialize<List<VehicleDto>>(content, _jsonOptions);
                if (vehicles == null)
                {
                    // Try alternative DTO structure
                    var vehicleList = JsonSerializer.Deserialize<List<dynamic>>(content, _jsonOptions);
                    return new List<VehicleDto>(); // Return empty if can't parse
                }
                return vehicles;
            }
            else
            {
                _logger.LogWarning("Failed to get vehicles. Status: {StatusCode}", response.StatusCode);
                return new List<VehicleDto>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Vehicle service to get vehicles");
            return new List<VehicleDto>();
        }
    }

    public async Task<int> GetVehicleCountAsync()
    {
        var vehicles = await GetVehiclesAsync();
        return vehicles.Count;
    }

    public async Task<VehicleDto?> GetVehicleAsync(Guid vehicleId)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.GetAsync($"api/Vehicle/{vehicleId}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<VehicleDto>(content, _jsonOptions);
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
            else
            {
                _logger.LogWarning("Failed to get vehicle {VehicleId}. Status: {StatusCode}", vehicleId, response.StatusCode);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Vehicle service to get vehicle {VehicleId}", vehicleId);
            return null;
        }
    }
}

