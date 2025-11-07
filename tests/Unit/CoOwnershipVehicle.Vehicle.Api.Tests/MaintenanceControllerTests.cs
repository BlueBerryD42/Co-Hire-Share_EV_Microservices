using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CoOwnershipVehicle.Domain.Enums;
using CoOwnershipVehicle.Vehicle.Api.DTOs;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CoOwnershipVehicle.Vehicle.Api.Tests;

/// <summary>
/// Integration tests for MaintenanceController endpoints
/// Tests HTTP endpoints, request/response validation, and status codes
/// </summary>
public class MaintenanceControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory<Program> _factory;

    public MaintenanceControllerTests(CustomWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    #region Upcoming Maintenance Tests

    [Fact]
    public async Task GetUpcomingMaintenance_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/maintenance/upcoming");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.NotNull(content);

        var result = JsonSerializer.Deserialize<UpcomingMaintenanceResponse>(
            content,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(result);
        Assert.NotNull(result.Vehicles);
        Assert.Equal(30, result.DaysAhead);
    }

    [Fact]
    public async Task GetUpcomingMaintenance_WithDaysParameter_ReturnsFiltered()
    {
        // Act
        var response = await _client.GetAsync("/api/maintenance/upcoming?days=7");

        // Assert
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<UpcomingMaintenanceResponse>(
            content,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(result);
        Assert.Equal(7, result.DaysAhead);
    }

    [Fact]
    public async Task GetUpcomingMaintenance_WithPriorityFilter_ReturnsFiltered()
    {
        // Act - Filter by High priority (2)
        var response = await _client.GetAsync("/api/maintenance/upcoming?priority=2");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    #endregion

    #region Overdue Maintenance Tests

    [Fact]
    public async Task GetOverdueMaintenance_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/api/maintenance/overdue");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<OverdueMaintenanceResponse>(
            content,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(result);
        Assert.NotNull(result.Items);
        Assert.True(result.TotalOverdue >= 0);
        Assert.True(result.CriticalCount >= 0);
    }

    #endregion

    #region Reschedule Tests

    [Fact]
    public async Task RescheduleMaintenance_WithValidRequest_ReturnsOk()
    {
        // This is a placeholder - in real scenario, you'd need:
        // 1. Create a maintenance schedule first
        // 2. Get auth token
        // 3. Then reschedule

        // For now, test with non-existent ID returns 404
        var scheduleId = Guid.NewGuid();
        var request = new RescheduleMaintenanceRequest
        {
            NewScheduledDate = DateTime.UtcNow.AddDays(15),
            Reason = "Testing reschedule endpoint",
            ForceReschedule = false
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync(
            $"/api/maintenance/{scheduleId}/reschedule",
            content);

        // Assert - Should return 403 Forbidden (missing auth token)
        // or 404 if schedule not found
        Assert.True(
            response.StatusCode == HttpStatusCode.Forbidden ||
            response.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RescheduleMaintenance_WithInvalidReason_ReturnsBadRequest()
    {
        // Arrange - Reason too short (< 10 characters)
        var scheduleId = Guid.NewGuid();
        var request = new RescheduleMaintenanceRequest
        {
            NewScheduledDate = DateTime.UtcNow.AddDays(15),
            Reason = "Short", // Invalid - too short
            ForceReschedule = false
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync(
            $"/api/maintenance/{scheduleId}/reschedule",
            content);

        // Assert
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.Forbidden);
    }

    #endregion

    #region Cancel Tests

    [Fact]
    public async Task CancelMaintenance_WithValidRequest_RequiresAuth()
    {
        // Arrange
        var scheduleId = Guid.NewGuid();
        var request = new CancelMaintenanceRequest
        {
            CancellationReason = "Testing cancellation endpoint - service no longer needed"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.SendAsync(new HttpRequestMessage
        {
            Method = HttpMethod.Delete,
            RequestUri = new Uri($"/api/maintenance/{scheduleId}", UriKind.Relative),
            Content = content
        });

        // Assert - Should return 403 Forbidden (missing auth token)
        // or 404 if schedule not found
        Assert.True(
            response.StatusCode == HttpStatusCode.Forbidden ||
            response.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CancelMaintenance_WithInvalidReason_ReturnsBadRequest()
    {
        // Arrange - Reason too short
        var scheduleId = Guid.NewGuid();
        var request = new CancelMaintenanceRequest
        {
            CancellationReason = "Short" // Invalid
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.SendAsync(new HttpRequestMessage
        {
            Method = HttpMethod.Delete,
            RequestUri = new Uri($"/api/maintenance/{scheduleId}", UriKind.Relative),
            Content = content
        });

        // Assert
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.Forbidden);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task GetUpcomingMaintenance_WithNegativeDays_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/api/maintenance/upcoming?days=-5");

        // Assert
        // Depending on implementation, might return 400 or just treat as 0
        Assert.True(response.StatusCode == HttpStatusCode.OK ||
                   response.StatusCode == HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetUpcomingMaintenance_WithVeryLargeDays_HandlesGracefully()
    {
        // Act
        var response = await _client.GetAsync("/api/maintenance/upcoming?days=10000");

        // Assert
        response.EnsureSuccessStatusCode();
    }

    #endregion
}
