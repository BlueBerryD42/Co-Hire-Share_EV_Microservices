using System.Net.Http.Headers;
using System.Text.Json;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Domain.Entities;
using Microsoft.AspNetCore.Http;

namespace CoOwnershipVehicle.Admin.Api.Services.HttpClients;

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
            // Use admin endpoint to get all vehicles
            var response = await _httpClient.GetAsync("api/Vehicle/all");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<VehicleDto>>(content, _jsonOptions) ?? new List<VehicleDto>();
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

    public async Task<int> GetVehicleCountAsync(VehicleStatus? status = null)
    {
        try
        {
            var vehicles = await GetVehiclesAsync();
            if (status.HasValue)
            {
                return vehicles.Count(v => v.Status == status.Value);
            }
            return vehicles.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting vehicle count");
            return 0;
        }
    }

    public async Task<List<MaintenanceScheduleItemDto>> GetMaintenanceSchedulesAsync(MaintenanceScheduleRequestDto? request = null)
    {
        try
        {
            SetAuthorizationHeader();
            var queryParams = new List<string>();
            
            if (request != null)
            {
                if (request.Status.HasValue)
                    queryParams.Add($"status={(int)request.Status.Value}");
                if (request.Page > 1)
                    queryParams.Add($"page={request.Page}");
                if (request.PageSize != 20)
                    queryParams.Add($"pageSize={request.PageSize}");
            }

            var queryString = queryParams.Any() ? "?" + string.Join("&", queryParams) : "";
            var response = await _httpClient.GetAsync($"api/Maintenance/schedules{queryString}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var pagedResponse = JsonSerializer.Deserialize<PagedResponseDto<MaintenanceScheduleItemDto>>(content, _jsonOptions);
                return pagedResponse?.Items ?? new List<MaintenanceScheduleItemDto>();
            }
            else
            {
                _logger.LogWarning("Failed to get maintenance schedules. Status: {StatusCode}", response.StatusCode);
                return new List<MaintenanceScheduleItemDto>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Vehicle service to get maintenance schedules");
            return new List<MaintenanceScheduleItemDto>();
        }
    }

    public async Task<MaintenanceScheduleItemDto?> GetMaintenanceScheduleAsync(Guid maintenanceId)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.GetAsync($"api/Maintenance/schedules/{maintenanceId}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<MaintenanceScheduleItemDto>(content, _jsonOptions);
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
            else
            {
                _logger.LogWarning("Failed to get maintenance schedule {MaintenanceId}. Status: {StatusCode}", maintenanceId, response.StatusCode);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Vehicle service to get maintenance schedule {MaintenanceId}", maintenanceId);
            return null;
        }
    }

    public async Task<bool> CreateMaintenanceScheduleAsync(CreateMaintenanceDto request)
    {
        try
        {
            SetAuthorizationHeader();
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("api/Maintenance/schedules", content);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Vehicle service to create maintenance schedule");
            return false;
        }
    }

    public async Task<bool> UpdateMaintenanceScheduleAsync(Guid maintenanceId, UpdateMaintenanceDto request)
    {
        try
        {
            SetAuthorizationHeader();
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PutAsync($"api/Maintenance/schedules/{maintenanceId}", content);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Vehicle service to update maintenance schedule {MaintenanceId}", maintenanceId);
            return false;
        }
    }
}

