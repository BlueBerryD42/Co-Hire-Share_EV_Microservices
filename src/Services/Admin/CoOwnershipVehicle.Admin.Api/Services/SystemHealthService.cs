using Microsoft.EntityFrameworkCore;
using CoOwnershipVehicle.Admin.Api.Data;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;

namespace CoOwnershipVehicle.Admin.Api.Services;

public class SystemHealthService : ISystemHealthService
{
    private readonly AdminDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SystemHealthService> _logger;
    private readonly IConfiguration _configuration;

    // Service configurations
    private readonly Dictionary<string, string> _serviceUrls = new();

    public SystemHealthService(
        AdminDbContext context,
        IHttpClientFactory httpClientFactory,
        ILogger<SystemHealthService> logger,
        IConfiguration configuration)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _configuration = configuration;

        // Initialize service URLs from configuration or defaults
        _serviceUrls["Auth"] = _configuration["ServiceUrls:Auth"] ?? "https://localhost:61601";
        _serviceUrls["User"] = _configuration["ServiceUrls:User"] ?? "https://localhost:61602";
        _serviceUrls["Group"] = _configuration["ServiceUrls:Group"] ?? "https://localhost:61603";
        _serviceUrls["Vehicle"] = _configuration["ServiceUrls:Vehicle"] ?? "https://localhost:61604";
        _serviceUrls["Booking"] = _configuration["ServiceUrls:Booking"] ?? "https://localhost:61606";
        _serviceUrls["Payment"] = _configuration["ServiceUrls:Payment"] ?? "https://localhost:61605";
        _serviceUrls["Notification"] = _configuration["ServiceUrls:Notification"] ?? "https://localhost:61609";
        _serviceUrls["Analytics"] = _configuration["ServiceUrls:Analytics"] ?? "https://localhost:61608";
    }

    public async Task<SystemHealthCheckDto> CheckSystemHealthAsync()
    {
        var startTime = DateTime.UtcNow;
        var healthCheck = new SystemHealthCheckDto
        {
            CheckTime = startTime,
            Services = new List<ServiceHealthDto>(),
            Dependencies = new List<DependencyHealthDto>()
        };

        // Check all services in parallel
        var serviceTasks = _serviceUrls.Select(kvp =>
            CheckServiceHealthAsync(kvp.Key, kvp.Value));
        
        var services = await Task.WhenAll(serviceTasks);
        healthCheck.Services = services.ToList();

        // Check dependencies
        var dependencyTasks = new[]
        {
            CheckDatabaseHealthAsync(),
            CheckRabbitMqHealthAsync(),
            CheckRedisHealthAsync(),
            CheckFileStorageHealthAsync()
        };

        var dependencies = await Task.WhenAll(dependencyTasks);
        healthCheck.Dependencies = dependencies.ToList();

        // Determine overall status
        healthCheck.OverallStatus = DetermineOverallStatus(services, dependencies);
        
        healthCheck.TotalResponseTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

        return healthCheck;
    }

    public async Task<ServiceHealthDto> CheckServiceHealthAsync(string serviceName, string baseUrl)
    {
        var health = new ServiceHealthDto
        {
            ServiceName = serviceName,
            BaseUrl = baseUrl,
            CheckTime = DateTime.UtcNow,
            Status = HealthStatus.Unknown
        };

        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(5);

            var stopwatch = Stopwatch.StartNew();
            
            // Try health endpoint first, fallback to root
            var healthEndpoints = new[] { "/health", "/status", "/" };
            HttpResponseMessage? response = null;

            foreach (var endpoint in healthEndpoints)
            {
                try
                {
                    response = await httpClient.GetAsync($"{baseUrl}{endpoint}");
                    if (response.IsSuccessStatusCode)
                        break;
                }
                catch
                {
                    continue;
                }
            }

            stopwatch.Stop();
            health.ResponseTimeMs = stopwatch.Elapsed.TotalMilliseconds;

            if (response == null || !response.IsSuccessStatusCode)
            {
                health.Status = HealthStatus.Unhealthy;
                health.ErrorMessage = $"Service returned status code: {response?.StatusCode}";
                health.LastIncidentTimestamp = DateTime.UtcNow;
                return health;
            }

            // Determine status based on response time
            if (health.ResponseTimeMs < 500)
                health.Status = HealthStatus.Healthy;
            else if (health.ResponseTimeMs < 2000)
                health.Status = HealthStatus.Degraded;
            else
                health.Status = HealthStatus.Unhealthy;

            // Try to parse response for additional info
            try
            {
                var content = await response.Content.ReadAsStringAsync();
                var json = JsonDocument.Parse(content);
                if (json.RootElement.TryGetProperty("status", out var statusElement))
                {
                    var statusStr = statusElement.GetString();
                    if (statusStr?.Contains("unhealthy", StringComparison.OrdinalIgnoreCase) == true)
                        health.Status = HealthStatus.Unhealthy;
                }
            }
            catch
            {
                // Ignore parsing errors
            }
        }
        catch (HttpRequestException ex)
        {
            health.Status = HealthStatus.Unhealthy;
            health.ErrorMessage = $"Connection failed: {ex.Message}";
            health.LastIncidentTimestamp = DateTime.UtcNow;
            health.ResponseTimeMs = 5000; // Timeout assumed
        }
        catch (TaskCanceledException)
        {
            health.Status = HealthStatus.Unhealthy;
            health.ErrorMessage = "Request timeout";
            health.LastIncidentTimestamp = DateTime.UtcNow;
            health.ResponseTimeMs = 5000;
        }
        catch (Exception ex)
        {
            health.Status = HealthStatus.Unhealthy;
            health.ErrorMessage = ex.Message;
            health.LastIncidentTimestamp = DateTime.UtcNow;
            _logger.LogError(ex, "Error checking health for service {ServiceName}", serviceName);
        }

        return health;
    }

    public async Task<DependencyHealthDto> CheckDatabaseHealthAsync()
    {
        var health = new DependencyHealthDto
        {
            DependencyName = "Database",
            Type = DependencyType.Database,
            Status = HealthStatus.Unknown
        };

        try
        {
            var stopwatch = Stopwatch.StartNew();
            var canConnect = await _context.Database.CanConnectAsync();
            stopwatch.Stop();

            health.ResponseTimeMs = stopwatch.Elapsed.TotalMilliseconds;

            if (canConnect)
            {
                // Try a simple query
                try
                {
                    await _context.Database.ExecuteSqlRawAsync("SELECT 1");
                    health.Status = health.ResponseTimeMs < 500 ? HealthStatus.Healthy : HealthStatus.Degraded;
                }
                catch
                {
                    health.Status = HealthStatus.Degraded;
                    health.ErrorMessage = "Database connection established but queries failing";
                }
            }
            else
            {
                health.Status = HealthStatus.Unhealthy;
                health.ErrorMessage = "Cannot connect to database";
                health.LastIncidentTimestamp = DateTime.UtcNow;
            }

            // Add connection info
            health.AdditionalInfo = new Dictionary<string, object>
            {
                { "DatabaseName", _context.Database.GetDbConnection().Database ?? "Unknown" }
            };
        }
        catch (Exception ex)
        {
            health.Status = HealthStatus.Unhealthy;
            health.ErrorMessage = ex.Message;
            health.LastIncidentTimestamp = DateTime.UtcNow;
            _logger.LogError(ex, "Error checking database health");
        }

        return health;
    }

    public async Task<DependencyHealthDto> CheckRabbitMqHealthAsync()
    {
        var health = new DependencyHealthDto
        {
            DependencyName = "RabbitMQ",
            Type = DependencyType.RabbitMQ,
            Status = HealthStatus.Unknown
        };

        try
        {
            var host = _configuration["RabbitMQ:Host"] ?? "localhost";
            var port = int.Parse(_configuration["RabbitMQ:Port"] ?? "5672");

            var stopwatch = Stopwatch.StartNew();
            
            using var tcpClient = new System.Net.Sockets.TcpClient();
            var connectTask = tcpClient.ConnectAsync(host, port);
            var timeoutTask = Task.Delay(3000);
            
            var completedTask = await Task.WhenAny(connectTask, timeoutTask);
            
            stopwatch.Stop();
            health.ResponseTimeMs = stopwatch.Elapsed.TotalMilliseconds;

            if (completedTask == connectTask && tcpClient.Connected)
            {
                tcpClient.Close();
                health.Status = health.ResponseTimeMs < 500 ? HealthStatus.Healthy : HealthStatus.Degraded;
            }
            else
            {
                health.Status = HealthStatus.Unhealthy;
                health.ErrorMessage = "Cannot connect to RabbitMQ";
                health.LastIncidentTimestamp = DateTime.UtcNow;
            }

            health.AdditionalInfo = new Dictionary<string, object>
            {
                { "Host", host },
                { "Port", port }
            };
        }
        catch (Exception ex)
        {
            health.Status = HealthStatus.Unhealthy;
            health.ErrorMessage = ex.Message;
            health.LastIncidentTimestamp = DateTime.UtcNow;
            _logger.LogError(ex, "Error checking RabbitMQ health");
        }

        return health;
    }

    public async Task<DependencyHealthDto> CheckRedisHealthAsync()
    {
        var health = new DependencyHealthDto
        {
            DependencyName = "Redis",
            Type = DependencyType.Redis,
            Status = HealthStatus.Unknown
        };

        try
        {
            // Redis is optional - check if configured
            var redisConnection = _configuration.GetConnectionString("Redis");
            if (string.IsNullOrEmpty(redisConnection))
            {
                health.Status = HealthStatus.Unknown;
                health.ErrorMessage = "Redis not configured";
                return health;
            }

            // Basic Redis health check would go here
            // For now, mark as unknown if not implemented
            health.Status = HealthStatus.Unknown;
            health.ErrorMessage = "Redis health check not implemented";
        }
        catch (Exception ex)
        {
            health.Status = HealthStatus.Unhealthy;
            health.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Error checking Redis health");
        }

        return health;
    }

    public async Task<DependencyHealthDto> CheckFileStorageHealthAsync()
    {
        var health = new DependencyHealthDto
        {
            DependencyName = "File Storage",
            Type = DependencyType.FileStorage,
            Status = HealthStatus.Unknown
        };

        try
        {
            // Check local file system or cloud storage
            var storagePath = _configuration["Storage:Path"] ?? Environment.CurrentDirectory;
            
            var stopwatch = Stopwatch.StartNew();
            var driveInfo = new DriveInfo(Path.GetPathRoot(storagePath) ?? storagePath);
            var availableSpace = driveInfo.AvailableFreeSpace;
            var totalSpace = driveInfo.TotalSize;
            stopwatch.Stop();

            health.ResponseTimeMs = stopwatch.Elapsed.TotalMilliseconds;

            var usagePercent = 100.0 - (availableSpace * 100.0 / totalSpace);
            
            if (usagePercent < 80)
                health.Status = HealthStatus.Healthy;
            else if (usagePercent < 90)
                health.Status = HealthStatus.Degraded;
            else
            {
                health.Status = HealthStatus.Unhealthy;
                health.ErrorMessage = $"Disk usage is at {usagePercent:F1}%";
                health.LastIncidentTimestamp = DateTime.UtcNow;
            }

            health.AdditionalInfo = new Dictionary<string, object>
            {
                { "AvailableSpaceBytes", availableSpace },
                { "TotalSpaceBytes", totalSpace },
                { "UsagePercent", usagePercent }
            };
        }
        catch (Exception ex)
        {
            health.Status = HealthStatus.Unhealthy;
            health.ErrorMessage = ex.Message;
            health.LastIncidentTimestamp = DateTime.UtcNow;
            _logger.LogError(ex, "Error checking file storage health");
        }

        return health;
    }

    private SystemHealthStatus DetermineOverallStatus(
        ServiceHealthDto[] services,
        DependencyHealthDto[] dependencies)
    {
        var unhealthyCount = services.Count(s => s.Status == HealthStatus.Unhealthy) +
                            dependencies.Count(d => d.Status == HealthStatus.Unhealthy);
        var degradedCount = services.Count(s => s.Status == HealthStatus.Degraded) +
                           dependencies.Count(d => d.Status == HealthStatus.Degraded);

        if (unhealthyCount > 0)
            return unhealthyCount >= 2 ? SystemHealthStatus.Critical : SystemHealthStatus.Unhealthy;
        
        if (degradedCount > 0)
            return SystemHealthStatus.Degraded;

        return SystemHealthStatus.Healthy;
    }
}

