using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using Microsoft.AspNetCore.Http;

namespace CoOwnershipVehicle.Admin.Api.Services.HttpClients;

public class UserServiceClient : IUserServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<UserServiceClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public UserServiceClient(
        HttpClient httpClient,
        IHttpContextAccessor httpContextAccessor,
        ILogger<UserServiceClient> logger)
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

    public async Task<UserProfileDto?> GetUserProfileAsync(Guid userId)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.GetAsync($"api/User/profile/{userId}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<UserProfileDto>(content, _jsonOptions);
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
            else
            {
                _logger.LogWarning("Failed to get user profile {UserId}. Status: {StatusCode}", userId, response.StatusCode);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling User service to get profile for {UserId}", userId);
            return null;
        }
    }

    public async Task<List<UserProfileDto>> GetUsersAsync(UserListRequestDto? request = null)
    {
        try
        {
            SetAuthorizationHeader();
            var queryParams = new List<string>();
            
            if (request != null)
            {
                if (!string.IsNullOrEmpty(request.Search))
                    queryParams.Add($"search={Uri.EscapeDataString(request.Search)}");
                if (request.Role.HasValue)
                    queryParams.Add($"role={request.Role.Value}");
                if (request.KycStatus.HasValue)
                    queryParams.Add($"kycStatus={request.KycStatus.Value}");
                if (request.AccountStatus.HasValue)
                    queryParams.Add($"accountStatus={request.AccountStatus.Value}");
                if (!string.IsNullOrEmpty(request.SortBy))
                    queryParams.Add($"sortBy={request.SortBy}");
                if (!string.IsNullOrEmpty(request.SortDirection))
                    queryParams.Add($"sortDirection={request.SortDirection}");
                queryParams.Add($"page={request.Page}");
                queryParams.Add($"pageSize={request.PageSize}");
            }

            var queryString = queryParams.Any() ? "?" + string.Join("&", queryParams) : "";
            var response = await _httpClient.GetAsync($"api/User/users{queryString}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<UserListResponseDto>(content, _jsonOptions);
                return result?.Users?.Select(u => new UserProfileDto
                {
                    Id = u.Id,
                    Email = u.Email,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Phone = u.Phone,
                    Role = u.Role,
                    KycStatus = u.KycStatus,
                    CreatedAt = u.CreatedAt
                }).ToList() ?? new List<UserProfileDto>();
            }
            else
            {
                _logger.LogWarning("Failed to get users. Status: {StatusCode}", response.StatusCode);
                return new List<UserProfileDto>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling User service to get users");
            return new List<UserProfileDto>();
        }
    }

    public async Task<List<KycDocumentDto>> GetPendingKycDocumentsAsync()
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.GetAsync("api/User/kyc/pending");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<KycDocumentDto>>(content, _jsonOptions) ?? new List<KycDocumentDto>();
            }
            else
            {
                _logger.LogWarning("Failed to get pending KYC documents. Status: {StatusCode}", response.StatusCode);
                return new List<KycDocumentDto>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling User service to get pending KYC documents");
            return new List<KycDocumentDto>();
        }
    }

    public async Task<KycDocumentDto> ReviewKycDocumentAsync(Guid documentId, ReviewKycDocumentDto reviewDto)
    {
        try
        {
            SetAuthorizationHeader();
            var json = JsonSerializer.Serialize(reviewDto);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"api/User/kyc/review/{documentId}", content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<KycDocumentDto>(responseContent, _jsonOptions) ?? throw new Exception("Failed to deserialize response");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to review KYC document {DocumentId}. Status: {StatusCode}, Response: {Response}", 
                    documentId, response.StatusCode, errorContent);
                throw new HttpRequestException($"Failed to review KYC document: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling User service to review KYC document {DocumentId}", documentId);
            throw;
        }
    }

    public async Task<bool> UpdateKycStatusAsync(Guid userId, KycStatus status, string? reason = null)
    {
        try
        {
            SetAuthorizationHeader();
            var request = new { Status = status, Reason = reason };
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PutAsync($"api/User/users/{userId}/kyc-status", content);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling User service to update KYC status for {UserId}", userId);
            return false;
        }
    }
}

