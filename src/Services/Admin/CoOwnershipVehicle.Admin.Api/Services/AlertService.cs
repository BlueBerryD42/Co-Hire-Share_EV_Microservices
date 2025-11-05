using Microsoft.EntityFrameworkCore;
using CoOwnershipVehicle.Admin.Api.Data;
using CoOwnershipVehicle.Admin.Api.Services;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using System.Diagnostics;

namespace CoOwnershipVehicle.Admin.Api.Services;

public class AlertService : IAlertService
{
    private readonly AdminDbContext _context;
    private readonly ISystemHealthService _healthService;
    private readonly ISystemMetricsService _metricsService;
    private readonly ILogger<AlertService> _logger;
    private readonly IConfiguration _configuration;

    // Alert thresholds
    private readonly double _errorRateThreshold = 5.0; // 5%
    private readonly double _responseTimeThresholdMs = 2000; // 2 seconds
    private readonly double _diskUsageThreshold = 90.0; // 90%

    public AlertService(
        AdminDbContext context,
        ISystemHealthService healthService,
        ISystemMetricsService metricsService,
        ILogger<AlertService> logger,
        IConfiguration configuration)
    {
        _context = context;
        _healthService = healthService;
        _metricsService = metricsService;
        _logger = logger;
        _configuration = configuration;

        _errorRateThreshold = double.Parse(_configuration["Alerts:ErrorRateThreshold"] ?? "5.0");
        _responseTimeThresholdMs = double.Parse(_configuration["Alerts:ResponseTimeThresholdMs"] ?? "2000");
        _diskUsageThreshold = double.Parse(_configuration["Alerts:DiskUsageThreshold"] ?? "90.0");
    }

    public async Task<List<AlertDto>> GetActiveAlertsAsync()
    {
        var alerts = new List<AlertDto>();
        var now = DateTime.UtcNow;

        // Check system health
        var healthCheck = await _healthService.CheckSystemHealthAsync();

        // Service down alerts
        foreach (var service in healthCheck.Services.Where(s => s.Status == HealthStatus.Unhealthy))
        {
            alerts.Add(new AlertDto
            {
                Type = "Service",
                Title = $"Service Down: {service.ServiceName}",
                Message = $"{service.ServiceName} is currently unhealthy. {service.ErrorMessage}",
                Severity = "Critical",
                CreatedAt = service.LastIncidentTimestamp ?? now,
                IsRead = false
            });
        }

        // Degraded services
        foreach (var service in healthCheck.Services.Where(s => s.Status == HealthStatus.Degraded))
        {
            alerts.Add(new AlertDto
            {
                Type = "Service",
                Title = $"Service Degraded: {service.ServiceName}",
                Message = $"{service.ServiceName} is experiencing degraded performance. Response time: {service.ResponseTimeMs:F0}ms",
                Severity = "Warning",
                CreatedAt = service.CheckTime,
                IsRead = false
            });
        }

        // Dependency alerts
        foreach (var dependency in healthCheck.Dependencies.Where(d => d.Status == HealthStatus.Unhealthy))
        {
            alerts.Add(new AlertDto
            {
                Type = "Dependency",
                Title = $"Dependency Unhealthy: {dependency.DependencyName}",
                Message = $"{dependency.DependencyName} is currently unhealthy. {dependency.ErrorMessage}",
                Severity = dependency.Type == DependencyType.Database ? "Critical" : "Warning",
                CreatedAt = dependency.LastIncidentTimestamp ?? now,
                IsRead = false
            });
        }

        // Disk space alerts
        var fileStorageHealth = healthCheck.Dependencies
            .FirstOrDefault(d => d.Type == DependencyType.FileStorage);
        if (fileStorageHealth?.AdditionalInfo != null &&
            fileStorageHealth.AdditionalInfo.ContainsKey("UsagePercent"))
        {
            var usagePercent = (double)fileStorageHealth.AdditionalInfo["UsagePercent"];
            if (usagePercent >= _diskUsageThreshold)
            {
                alerts.Add(new AlertDto
                {
                    Type = "Resource",
                    Title = "Disk Space Low",
                    Message = $"Disk usage is at {usagePercent:F1}%. Consider freeing up space.",
                    Severity = usagePercent >= 95 ? "Critical" : "Warning",
                    CreatedAt = now,
                    IsRead = false
                });
            }
        }

        // Metrics-based alerts
        var metrics = await _metricsService.GetSystemMetricsAsync(TimeSpan.FromMinutes(15));

        // Error rate alerts
        foreach (var serviceMetrics in metrics.ServiceMetrics)
        {
            if (serviceMetrics.ErrorRate > _errorRateThreshold)
            {
                alerts.Add(new AlertDto
                {
                    Type = "Performance",
                    Title = $"High Error Rate: {serviceMetrics.ServiceName}",
                    Message = $"{serviceMetrics.ServiceName} has an error rate of {serviceMetrics.ErrorRate:F2}% (threshold: {_errorRateThreshold}%)",
                    Severity = serviceMetrics.ErrorRate > _errorRateThreshold * 2 ? "Critical" : "Warning",
                    CreatedAt = now,
                    IsRead = false
                });
            }

            // Response time alerts
            if (serviceMetrics.AverageResponseTimeMs > _responseTimeThresholdMs)
            {
                alerts.Add(new AlertDto
                {
                    Type = "Performance",
                    Title = $"Slow Response Time: {serviceMetrics.ServiceName}",
                    Message = $"{serviceMetrics.ServiceName} has an average response time of {serviceMetrics.AverageResponseTimeMs:F0}ms (threshold: {_responseTimeThresholdMs}ms)",
                    Severity = serviceMetrics.AverageResponseTimeMs > _responseTimeThresholdMs * 2 ? "Critical" : "Warning",
                    CreatedAt = now,
                    IsRead = false
                });
            }
        }

        // Database slow queries
        if (metrics.DatabaseMetrics.SlowQueries > 10)
        {
            alerts.Add(new AlertDto
            {
                Type = "Database",
                Title = "Slow Database Queries",
                Message = $"There are {metrics.DatabaseMetrics.SlowQueries} slow queries detected in the last 15 minutes.",
                Severity = "Warning",
                CreatedAt = now,
                IsRead = false
            });
        }

        // High CPU usage
        if (metrics.SystemResources.CpuUsagePercent > 80)
        {
            alerts.Add(new AlertDto
            {
                Type = "Resource",
                Title = "High CPU Usage",
                Message = $"CPU usage is at {metrics.SystemResources.CpuUsagePercent:F1}%",
                Severity = metrics.SystemResources.CpuUsagePercent > 90 ? "Critical" : "Warning",
                CreatedAt = now,
                IsRead = false
            });
        }

        // High memory usage
        if (metrics.SystemResources.MemoryUsagePercent > 80)
        {
            alerts.Add(new AlertDto
            {
                Type = "Resource",
                Title = "High Memory Usage",
                Message = $"Memory usage is at {metrics.SystemResources.MemoryUsagePercent:F1}%",
                Severity = metrics.SystemResources.MemoryUsagePercent > 90 ? "Critical" : "Warning",
                CreatedAt = now,
                IsRead = false
            });
        }

        return alerts.OrderByDescending(a => a.Severity == "Critical" ? 3 : a.Severity == "Warning" ? 2 : 1)
                    .ThenByDescending(a => a.CreatedAt)
                    .ToList();
    }

    public async Task CheckAndTriggerAlertsAsync()
    {
        try
        {
            var alerts = await GetActiveAlertsAsync();

            foreach (var alert in alerts)
            {
                // In production, you would:
                // 1. Store alerts in database
                // 2. Send email/SMS notifications
                // 3. Integrate with notification service
                
                _logger.LogWarning("Alert: {Type} - {Title} - {Message}", 
                    alert.Type, alert.Title, alert.Message);

                // TODO: Send notifications to admins
                // TODO: Persist alerts to database
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking and triggering alerts");
        }
    }

    public async Task<bool> CreateAlertAsync(string type, string title, string message, string severity)
    {
        try
        {
            var alert = new AlertDto
            {
                Type = type,
                Title = title,
                Message = message,
                Severity = severity,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };

            // In production, persist to database
            _logger.LogWarning("Manual Alert Created: {Type} - {Title}", type, title);

            return await Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating alert");
            return false;
        }
    }
}

