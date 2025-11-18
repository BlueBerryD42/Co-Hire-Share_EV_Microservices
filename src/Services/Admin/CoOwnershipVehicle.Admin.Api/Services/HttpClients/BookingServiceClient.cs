using System.Net.Http.Headers;
using System.Text.Json;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Domain.Entities;
using Microsoft.AspNetCore.Http;

namespace CoOwnershipVehicle.Admin.Api.Services.HttpClients;

public class BookingServiceClient : IBookingServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<BookingServiceClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public BookingServiceClient(
        HttpClient httpClient,
        IHttpContextAccessor httpContextAccessor,
        ILogger<BookingServiceClient> logger)
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

    public async Task<List<BookingDto>> GetBookingsAsync(DateTime? from = null, DateTime? to = null, Guid? userId = null, Guid? groupId = null)
    {
        try
        {
            SetAuthorizationHeader();
            var queryParams = new List<string>();
            
            if (from.HasValue)
                queryParams.Add($"from={from.Value:yyyy-MM-ddTHH:mm:ssZ}");
            if (to.HasValue)
                queryParams.Add($"to={to.Value:yyyy-MM-ddTHH:mm:ssZ}");
            if (userId.HasValue)
                queryParams.Add($"userId={userId.Value}");
            if (groupId.HasValue)
                queryParams.Add($"groupId={groupId.Value}");

            var queryString = queryParams.Any() ? "?" + string.Join("&", queryParams) : "";
            // Use admin endpoint to get all bookings
            var response = await _httpClient.GetAsync($"api/Booking/all{queryString}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<BookingDto>>(content, _jsonOptions) ?? new List<BookingDto>();
            }
            else
            {
                _logger.LogWarning("Failed to get bookings. Status: {StatusCode}", response.StatusCode);
                return new List<BookingDto>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Booking service to get bookings");
            return new List<BookingDto>();
        }
    }

    public async Task<int> GetBookingCountAsync(BookingStatus? status = null)
    {
        try
        {
            var bookings = await GetBookingsAsync();
            if (status.HasValue)
            {
                return bookings.Count(b => b.Status == status.Value);
            }
            return bookings.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting booking count");
            return 0;
        }
    }

    public async Task<BookingDto?> GetBookingAsync(Guid bookingId)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.GetAsync($"api/Booking/{bookingId}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<BookingDto>(content, _jsonOptions);
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
            else
            {
                _logger.LogWarning("Failed to get booking {BookingId}. Status: {StatusCode}", bookingId, response.StatusCode);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Booking service to get booking {BookingId}", bookingId);
            return null;
        }
    }

    public async Task<List<CheckInDto>> GetCheckInsAsync(DateTime? from = null, DateTime? to = null, Guid? userId = null, Guid? vehicleId = null, Guid? bookingId = null)
    {
        try
        {
            SetAuthorizationHeader();
            var queryParams = new List<string>();
            
            if (from.HasValue)
                queryParams.Add($"from={from.Value:yyyy-MM-ddTHH:mm:ssZ}");
            if (to.HasValue)
                queryParams.Add($"to={to.Value:yyyy-MM-ddTHH:mm:ssZ}");
            if (userId.HasValue)
                queryParams.Add($"userId={userId.Value}");
            if (vehicleId.HasValue)
                queryParams.Add($"vehicleId={vehicleId.Value}");
            if (bookingId.HasValue)
                queryParams.Add($"bookingId={bookingId.Value}");

            var queryString = queryParams.Any() ? "?" + string.Join("&", queryParams) : "";
            var response = await _httpClient.GetAsync($"api/CheckIn{queryString}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<CheckInDto>>(content, _jsonOptions) ?? new List<CheckInDto>();
            }
            else
            {
                _logger.LogWarning("Failed to get check-ins. Status: {StatusCode}", response.StatusCode);
                return new List<CheckInDto>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Booking service to get check-ins");
            return new List<CheckInDto>();
        }
    }

    public async Task<CheckInDto?> GetCheckInAsync(Guid checkInId)
    {
        try
        {
            SetAuthorizationHeader();
            var response = await _httpClient.GetAsync($"api/CheckIn/{checkInId}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<CheckInDto>(content, _jsonOptions);
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
            else
            {
                _logger.LogWarning("Failed to get check-in {CheckInId}. Status: {StatusCode}", checkInId, response.StatusCode);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Booking service to get check-in {CheckInId}", checkInId);
            return null;
        }
    }

    public async Task<bool> ApproveCheckInAsync(Guid checkInId, ApproveCheckInDto request)
    {
        try
        {
            SetAuthorizationHeader();
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"api/CheckIn/{checkInId}/approve", content);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Booking service to approve check-in {CheckInId}", checkInId);
            return false;
        }
    }

    public async Task<bool> RejectCheckInAsync(Guid checkInId, RejectCheckInDto request)
    {
        try
        {
            SetAuthorizationHeader();
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"api/CheckIn/{checkInId}/reject", content);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Booking service to reject check-in {CheckInId}", checkInId);
            return false;
        }
    }
}

