using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using Microsoft.AspNetCore.Http;

namespace CoOwnershipVehicle.Admin.Api.Services.HttpClients;

public class GroupServiceClient : IGroupServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<GroupServiceClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public GroupServiceClient(
        HttpClient httpClient,
        IHttpContextAccessor httpContextAccessor,
        ILogger<GroupServiceClient> logger)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true,
            // Allow both string and numeric enum values (matching Group service)
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

    public async Task<List<GroupDto>> GetGroupsAsync(GroupListRequestDto? request = null)
    {
        try
        {
            SetAuthorizationHeader();
            var queryParams = new List<string>();
            
            if (request != null)
            {
                if (!string.IsNullOrEmpty(request.Search))
                    queryParams.Add($"search={Uri.EscapeDataString(request.Search)}");
                if (request.Status.HasValue)
                    queryParams.Add($"status={request.Status.Value}");
            }

            var queryString = queryParams.Any() ? "?" + string.Join("&", queryParams) : "";
            var requestUrl = $"api/Group/all{queryString}";
            var fullUrl = new Uri(_httpClient.BaseAddress ?? new Uri("http://localhost"), requestUrl).ToString();
            
            // Log authorization header status
            var hasAuth = _httpClient.DefaultRequestHeaders.Authorization != null;
            var authHeader = _httpClient.DefaultRequestHeaders.Authorization;
            _logger.LogInformation("Calling Group service: {RequestUrl}, BaseAddress: {BaseAddress}, FullUrl: {FullUrl}, HasAuth: {HasAuth}", 
                requestUrl, _httpClient.BaseAddress?.ToString() ?? "NULL", fullUrl, hasAuth);
            
            if (hasAuth && authHeader != null)
            {
                _logger.LogInformation("Authorization scheme: {Scheme}, Token length: {Length}", 
                    authHeader.Scheme,
                    authHeader.Parameter?.Length ?? 0);
            }
            else
            {
                _logger.LogWarning("No authorization header set before calling Group service!");
            }
            
            var response = await _httpClient.GetAsync(requestUrl);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Group service response received. Status: {StatusCode}, Content length: {Length}", 
                    response.StatusCode, content.Length);
                
                // Log first 500 chars of content to debug
                var contentPreview = content.Length > 500 ? content.Substring(0, 500) + "..." : content;
                _logger.LogInformation("Group service response content preview: {Content}", contentPreview);
                
                try
                {
                    var groups = JsonSerializer.Deserialize<List<GroupDto>>(content, _jsonOptions) ?? new List<GroupDto>();
                    
                    _logger.LogInformation("Deserialized groups successfully. Count: {Count}", groups.Count);
                    
                    if (groups.Count > 0)
                    {
                        _logger.LogInformation("First group: Id={Id}, Name={Name}", 
                            groups[0].Id, groups[0].Name);
                    }
                    
                    return groups;
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Failed to deserialize Group service response. Content: {Content}", content);
                    return new List<GroupDto>();
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to get groups. Status: {StatusCode}, Response: {Response}, RequestUrl: {RequestUrl}", 
                    response.StatusCode, errorContent, fullUrl);
                
                // If 403, log authorization details
                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    _logger.LogError("403 Forbidden - Authorization failed. HasAuthHeader: {HasAuth}, Scheme: {Scheme}, TokenLength: {TokenLength}",
                        hasAuth,
                        authHeader?.Scheme ?? "N/A",
                        authHeader?.Parameter?.Length ?? 0);
                }
                
                return new List<GroupDto>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Group service to get groups");
            return new List<GroupDto>();
        }
    }

    public async Task<GroupDetailsDto?> GetGroupDetailsAsync(Guid groupId)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.GetAsync($"api/Group/{groupId}/details");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<GroupDetailsDto>(content, _jsonOptions);
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
            else
            {
                _logger.LogWarning("Failed to get group details {GroupId}. Status: {StatusCode}", groupId, response.StatusCode);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Group service to get group details for {GroupId}", groupId);
            return null;
        }
    }

    public async Task<bool> UpdateGroupStatusAsync(Guid groupId, UpdateGroupStatusDto request)
    {
        try
        {
            SetAuthorizationHeader();
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PutAsync($"api/Group/{groupId}/status", content);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Group service to update group status for {GroupId}", groupId);
            return false;
        }
    }
}

