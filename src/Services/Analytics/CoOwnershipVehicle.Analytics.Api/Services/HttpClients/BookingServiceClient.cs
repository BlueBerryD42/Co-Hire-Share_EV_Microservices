using System.Net.Http.Headers;
using System.Text.Json;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using Microsoft.AspNetCore.Http;

namespace CoOwnershipVehicle.Analytics.Api.Services.HttpClients;

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

    public async Task<List<BookingDto>> GetBookingsAsync(DateTime? from = null, DateTime? to = null, Guid? groupId = null)
    {
        try
        {
            SetAuthorizationHeader();
            var queryParams = new List<string>();
            
            if (from.HasValue)
                queryParams.Add($"from={from.Value:yyyy-MM-ddTHH:mm:ssZ}");
            if (to.HasValue)
                queryParams.Add($"to={to.Value:yyyy-MM-ddTHH:mm:ssZ}");

            var queryString = queryParams.Any() ? "?" + string.Join("&", queryParams) : "";
            var response = await _httpClient.GetAsync($"api/Booking/my-bookings{queryString}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var bookings = JsonSerializer.Deserialize<List<BookingDto>>(content, _jsonOptions) ?? new List<BookingDto>();
                
                // Filter by groupId if provided
                if (groupId.HasValue)
                    bookings = bookings.Where(b => b.GroupId == groupId.Value).ToList();
                
                return bookings;
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

    public async Task<List<CheckInDto>> GetBookingCheckInsAsync(Guid bookingId)
    {
        try
        {
            SetAuthorizationHeader();
            // Note: This assumes Booking service has an endpoint to get check-ins for a booking
            // If not available, we'll need to use an alternative approach
            var response = await _httpClient.GetAsync($"api/Booking/{bookingId}/check-ins");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<CheckInDto>>(content, _jsonOptions) ?? new List<CheckInDto>();
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new List<CheckInDto>();
            }
            else
            {
                _logger.LogWarning("Failed to get check-ins for booking {BookingId}. Status: {StatusCode}", bookingId, response.StatusCode);
                return new List<CheckInDto>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Booking service to get check-ins for booking {BookingId}", bookingId);
            return new List<CheckInDto>();
        }
    }
}

