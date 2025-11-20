using System.Net.Http.Headers;
using System.Text.Json;
using System.Linq;
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
        _jsonOptions = new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true,
            // Allow both string and numeric enum values (matching Vehicle service)
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };
    }

    private void SetAuthorizationHeader()
    {
        // Clear existing authorization header first
        _httpClient.DefaultRequestHeaders.Authorization = null;
        
        var httpContext = _httpContextAccessor.HttpContext;
        var token = httpContext?.Request.Headers["Authorization"].ToString();
        
        _logger.LogInformation("SetAuthorizationHeader called. HttpContext is null: {IsNull}, Token is empty: {IsEmpty}", 
            httpContext == null, string.IsNullOrEmpty(token));
        
        if (!string.IsNullOrEmpty(token))
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(token);
                var authHeader = _httpClient.DefaultRequestHeaders.Authorization;
                _logger.LogInformation("Authorization header set successfully. Scheme: {Scheme}, Token length: {Length}", 
                    authHeader?.Scheme ?? "NULL",
                    authHeader?.Parameter?.Length ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse authorization header. Token value: {TokenPrefix}", 
                    token.Length > 50 ? token.Substring(0, 50) + "..." : token);
            }
        }
        else
        {
            _logger.LogWarning("Authorization token not found in request headers. Available headers: {Headers}", 
                string.Join(", ", httpContext?.Request.Headers.Keys ?? Enumerable.Empty<string>()));
        }
    }

    public async Task<List<VehicleDto>> GetVehiclesAsync()
    {
        try
        {
            SetAuthorizationHeader();
            var requestUrl = "api/Vehicle/all";
            var fullUrl = new Uri(_httpClient.BaseAddress ?? new Uri("http://localhost"), requestUrl).ToString();
            
            // Log authorization header status
            var hasAuth = _httpClient.DefaultRequestHeaders.Authorization != null;
            var authHeader = _httpClient.DefaultRequestHeaders.Authorization;
            _logger.LogInformation("Calling Vehicle service: {RequestUrl}, BaseAddress: {BaseAddress}, FullUrl: {FullUrl}, HasAuth: {HasAuth}", 
                requestUrl, _httpClient.BaseAddress?.ToString() ?? "NULL", fullUrl, hasAuth);
            
            if (hasAuth && authHeader != null)
            {
                _logger.LogInformation("Authorization scheme: {Scheme}, Token length: {Length}", 
                    authHeader.Scheme,
                    authHeader.Parameter?.Length ?? 0);
            }
            else
            {
                _logger.LogWarning("No authorization header set before calling Vehicle service!");
            }
            
            var response = await _httpClient.GetAsync(requestUrl);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Vehicle service response received. Status: {StatusCode}, Content length: {Length}", 
                    response.StatusCode, content.Length);
                
                // Log first 500 chars of content to debug
                var contentPreview = content.Length > 500 ? content.Substring(0, 500) + "..." : content;
                _logger.LogInformation("Vehicle service response content preview: {Content}", contentPreview);
                
                try
                {
                    var vehicles = JsonSerializer.Deserialize<List<VehicleDto>>(content, _jsonOptions) ?? new List<VehicleDto>();
                    
                    _logger.LogInformation("Deserialized vehicles successfully. Count: {Count}", vehicles.Count);
                    
                    if (vehicles.Count > 0)
                    {
                        _logger.LogInformation("First vehicle: Id={Id}, Model={Model}", 
                            vehicles[0].Id, vehicles[0].Model);
                    }
                    
                    return vehicles;
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Failed to deserialize Vehicle service response. Content: {Content}", content);
                    return new List<VehicleDto>();
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to get vehicles. Status: {StatusCode}, Response: {Response}, RequestUrl: {RequestUrl}", 
                    response.StatusCode, errorContent, fullUrl);
                
                // If 403, log authorization details
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    _logger.LogError("403 Forbidden - Authorization failed. HasAuthHeader: {HasAuth}, Scheme: {Scheme}, TokenLength: {TokenLength}",
                        hasAuth,
                        authHeader?.Scheme ?? "N/A",
                        authHeader?.Parameter?.Length ?? 0);
                }
                
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

