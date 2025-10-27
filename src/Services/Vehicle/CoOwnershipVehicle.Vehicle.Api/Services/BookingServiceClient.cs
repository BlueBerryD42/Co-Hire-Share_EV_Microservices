
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;

namespace CoOwnershipVehicle.Vehicle.Api.Services
{
    public class BookingServiceClient : IBookingServiceClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<BookingServiceClient> _logger;

        public BookingServiceClient(HttpClient httpClient, ILogger<BookingServiceClient> logger, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _logger = logger;
            _httpClient.BaseAddress = new Uri(configuration["ServiceUrls:BookingApi"]);
        }

        public async Task<BookingConflictDto> CheckAvailabilityAsync(Guid vehicleId, DateTime from, DateTime to, string accessToken)
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var response = await _httpClient.GetAsync($"/api/booking/conflicts?vehicleId={vehicleId}&startAt={from:o}&endAt={to:o}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                };
                options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
                return JsonSerializer.Deserialize<BookingConflictDto>(content, options);
            }
            else
            {
                _logger.LogError("Failed to check availability. Status code: {StatusCode}", response.StatusCode);
                return null;
            }
        }
    }
}
