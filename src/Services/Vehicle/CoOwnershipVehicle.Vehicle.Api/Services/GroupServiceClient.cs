using System.Net.Http.Headers;
using System.Text.Json;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Vehicle.Api.DTOs;

namespace CoOwnershipVehicle.Vehicle.Api.Services;

public class GroupServiceClient : IGroupServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GroupServiceClient> _logger;

    public GroupServiceClient(HttpClient httpClient, ILogger<GroupServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<GroupServiceGroupDto>> GetUserGroups(string accessToken)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.GetAsync("api/Group"); // Assuming the endpoint is /api/Group

        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            // The Group service returns a list of GroupDto, which is more comprehensive.
            // We need to deserialize it and then select only the Id.
            // For simplicity, I'm assuming the GroupDto from Group service has an 'Id' property.
            // If the GroupDto in Group service is different, this deserialization might need adjustment.
            var groupDtos = JsonSerializer.Deserialize<List<GroupDto>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (groupDtos != null)
            {
                return groupDtos.Select(g => new GroupServiceGroupDto { Id = g.Id }).ToList();
            }
        }
        else
        {
            _logger.LogError("Failed to retrieve user groups from Group service. Status code: {StatusCode}", response.StatusCode);
        }

        return new List<GroupServiceGroupDto>();
    }

    public async Task<bool> IsUserInGroupAsync(Guid groupId, Guid userId, string accessToken)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            // Get user's groups
            var response = await _httpClient.GetAsync("api/Group");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var groupDtos = JsonSerializer.Deserialize<List<GroupDto>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                // Check if the specified groupId is in the user's groups
                return groupDtos?.Any(g => g.Id == groupId) ?? false;
            }
            else
            {
                _logger.LogWarning("Failed to check group membership. Status code: {StatusCode}", response.StatusCode);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking group membership for user {UserId} in group {GroupId}", userId, groupId);
            return false;
        }
    }

    public async Task<GroupDetailsDto?> GetGroupDetailsAsync(Guid groupId, string accessToken)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            var url = $"api/Group/{groupId}/details";
            _logger.LogInformation("Requesting group details from Group Service: {Url}", url);

            var response = await _httpClient.GetAsync(url);

            _logger.LogInformation("Group Service response status: {StatusCode} for GroupId: {GroupId}",
                response.StatusCode, groupId);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Group Service returned content length: {Length} bytes", content.Length);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var result = JsonSerializer.Deserialize<GroupDetailsDto>(content, options);

                if (result != null)
                {
                    _logger.LogInformation("Successfully deserialized group details. Members count: {Count}",
                        result.Members?.Count ?? 0);
                }

                return result;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Group {GroupId} not found in Group Service", groupId);
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Group Service 404 response: {Response}", errorContent);
                return null;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to get group details for {GroupId}. Status code: {StatusCode}, Response: {Response}",
                    groupId, response.StatusCode, errorContent);
                return null;
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Group Service is not available at {BaseUrl}. Group ID: {GroupId}",
                _httpClient.BaseAddress, groupId);
            // Graceful degradation - return default data
            return new GroupDetailsDto
            {
                GroupId = groupId,
                GroupName = "Unknown Group",
                Members = new List<GroupMemberWithOwnership>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception getting group details for {GroupId}", groupId);
            return null;
        }
    }
}
