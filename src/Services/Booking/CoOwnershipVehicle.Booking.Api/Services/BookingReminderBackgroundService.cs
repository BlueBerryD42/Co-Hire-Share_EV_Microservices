using System.Net.Http.Json;
using CoOwnershipVehicle.Booking.Api.Data;
using CoOwnershipVehicle.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoOwnershipVehicle.Booking.Api.Services;

/// <summary>
/// Periodically scans bookings nearing EndAt and triggers reminder emails via Auth service.
/// </summary>
public class BookingReminderBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BookingReminderBackgroundService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    private const int ScanIntervalMinutes = 5;
    private const int ReminderWindowMinutes = 30;

    public BookingReminderBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<BookingReminderBackgroundService> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessRemindersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing booking ending reminders");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(ScanIntervalMinutes), stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // ignore on shutdown
            }
        }
    }

    private async Task ProcessRemindersAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

        var now = DateTime.UtcNow;
        var windowEnd = now.AddMinutes(ReminderWindowMinutes);

        var candidates = await dbContext.Bookings
            .Where(b =>
                b.EndAt <= windowEnd &&
                b.EndAt > now &&
                b.PreCheckoutReminderSentAt == null &&
                b.Status != BookingStatus.Completed &&
                b.Status != BookingStatus.Cancelled &&
                b.Status != BookingStatus.NoShow)
            .Take(50) // safety limit per scan
            .ToListAsync(cancellationToken);

        if (!candidates.Any())
        {
            return;
        }

        var authBaseUrl = _configuration["AuthService:BaseUrl"]
            ?? Environment.GetEnvironmentVariable("AUTH_SERVICE_BASE_URL");

        if (string.IsNullOrWhiteSpace(authBaseUrl))
        {
            _logger.LogWarning("AuthService:BaseUrl not configured; skipping booking ending reminders.");
            return;
        }

        var client = _httpClientFactory.CreateClient("booking-reminders");
        client.BaseAddress = new Uri(authBaseUrl.TrimEnd('/'));

        foreach (var booking in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var minutesLeft = Math.Max(1, (int)Math.Round((booking.EndAt - now).TotalMinutes));
            var payload = new
            {
                userId = booking.UserId,
                email = (string?)null,
                endAt = booking.EndAt,
                vehicleModel = $"Vehicle {booking.VehicleId}",
                minutesLeft
            };

            try
            {
                var response = await client.PostAsJsonAsync("/api/Auth/booking-ending-reminder", payload, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    booking.PreCheckoutReminderSentAt = now;
                    _logger.LogInformation("Sent ending reminder for booking {BookingId} (EndAt {EndAt})", booking.Id, booking.EndAt);
                }
                else
                {
                    _logger.LogWarning("Failed to send ending reminder for booking {BookingId}. Status {StatusCode}", booking.Id, response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending ending reminder for booking {BookingId}", booking.Id);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
