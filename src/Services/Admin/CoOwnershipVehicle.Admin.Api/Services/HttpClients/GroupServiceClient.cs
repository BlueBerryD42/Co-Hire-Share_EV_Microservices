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

    public async Task<List<GroupDto>> GetGroupsAsync(GroupListRequestDto? request = null)
    {
        try
        {
            SetAuthorizationHeader();
            // Note: Group service may not have admin endpoints yet, so we'll need to call the regular endpoint
            // For admin access, we might need to add admin-specific endpoints to Group service
            var response = await _httpClient.GetAsync("api/Group");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<GroupDto>>(content, _jsonOptions) ?? new List<GroupDto>();
            }
            else
            {
                _logger.LogWarning("Failed to get groups. Status: {StatusCode}", response.StatusCode);
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

