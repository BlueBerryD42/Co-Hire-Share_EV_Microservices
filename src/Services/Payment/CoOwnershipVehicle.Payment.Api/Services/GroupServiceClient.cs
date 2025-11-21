using System.Net.Http.Headers;
using System.Text.Json;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Payment.Api.Services.Interfaces;
using CoOwnershipVehicle.Shared.Contracts.DTOs;

namespace CoOwnershipVehicle.Payment.Api.Services;

/// <summary>
/// HTTP client implementation for communicating with Group Service
/// Fetches group and member data via HTTP since Payment service uses microservices architecture
/// </summary>
public class GroupServiceClient : IGroupServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GroupServiceClient> _logger;

    public GroupServiceClient(HttpClient httpClient, ILogger<GroupServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<GroupDto>> GetUserGroups(string accessToken)
    {
        try
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.GetAsync("api/Group");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                };
                var groupDtos = JsonSerializer.Deserialize<List<GroupDto>>(content, options);
                return groupDtos ?? new List<GroupDto>();
            }
            else
            {
                _logger.LogError("Failed to retrieve user groups from Group service. Status code: {StatusCode}", response.StatusCode);
                return new List<GroupDto>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user groups from Group service");
            return new List<GroupDto>();
        }
    }

    public async Task<bool> IsUserInGroupAsync(Guid groupId, Guid userId, string accessToken)
    {
        try
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            // Get user's groups and check if the target group is in the list
            var response = await _httpClient.GetAsync("api/Group");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                };
                var groupDtos = JsonSerializer.Deserialize<List<GroupDto>>(content, options);

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
        try
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var url = $"api/Group/{groupId}/details";
            _logger.LogInformation("Requesting group details from Group Service: {Url}", url);

            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                };

                // The /details endpoint returns: { GroupId, GroupName, Status, Members: [...] }
                var groupData = JsonSerializer.Deserialize<JsonElement>(content, options);

                if (groupData.ValueKind == JsonValueKind.Object)
                {
                    // Map the response to GroupDetailsDto
                    var groupDetails = new GroupDetailsDto
                    {
                        Id = groupId,
                        Name = groupData.TryGetProperty("GroupName", out var nameProp) ? nameProp.GetString() ?? string.Empty :
                               (groupData.TryGetProperty("groupName", out var nameLower) ? nameLower.GetString() ?? string.Empty : string.Empty),
                        Status = groupData.TryGetProperty("Status", out var statusProp) ? 
                                (GroupStatus)Enum.Parse(typeof(GroupStatus), statusProp.GetString() ?? "PendingApproval", true) :
                                GroupStatus.PendingApproval,
                        Members = new List<GroupMemberDetailsDto>()
                    };

                    // Parse members array
                    if (groupData.TryGetProperty("Members", out var membersProp) && membersProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var memberElement in membersProp.EnumerateArray())
                        {
                            var userIdProp = memberElement.TryGetProperty("UserId", out var uidUpper) ? uidUpper :
                                           (memberElement.TryGetProperty("userId", out var uidLower) ? uidLower : default);
                            var ownershipProp = memberElement.TryGetProperty("OwnershipPercentage", out var ownUpper) ? ownUpper :
                                               (memberElement.TryGetProperty("ownershipPercentage", out var ownLower) ? ownLower : default);
                            var roleProp = memberElement.TryGetProperty("Role", out var roleUpper) ? roleUpper :
                                          (memberElement.TryGetProperty("role", out var roleLower) ? roleLower : default);

                            if (userIdProp.ValueKind != JsonValueKind.Null && Guid.TryParse(userIdProp.GetString(), out var memberUserId))
                            {
                                groupDetails.Members.Add(new GroupMemberDetailsDto
                                {
                                    UserId = memberUserId,
                                    SharePercentage = ownershipProp.ValueKind != JsonValueKind.Null ? ownershipProp.GetDecimal() / 100m : 0m,
                                    Role = roleProp.ValueKind != JsonValueKind.Null ? 
                                          Enum.Parse<GroupRole>(roleProp.GetString() ?? "Member", true) : GroupRole.Member
                                });
                            }
                        }
                    }

                    return groupDetails;
                }
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Group {GroupId} not found in Group Service", groupId);
                return null;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to get group details for {GroupId}. Status code: {StatusCode}, Response: {Response}",
                    groupId, response.StatusCode, errorContent);
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception getting group details for {GroupId}", groupId);
            return null;
        }
    }
}

