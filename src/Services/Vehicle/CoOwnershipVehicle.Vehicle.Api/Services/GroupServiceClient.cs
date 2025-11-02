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
}
