using Microsoft.EntityFrameworkCore;
using CoOwnershipVehicle.Admin.Api.Data;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace CoOwnershipVehicle.Admin.Api.Services;

public class SystemMetricsService : ISystemMetricsService
{
    private readonly AdminDbContext _context;
    private readonly ILogger<SystemMetricsService> _logger;
    
    // In-memory metrics storage (in production, use a proper metrics store like Prometheus)
    private static readonly ConcurrentDictionary<string, ConcurrentQueue<MetricRecord>> _serviceMetrics = new();
    private static readonly object _lockObject = new();

    public SystemMetricsService(AdminDbContext context, ILogger<SystemMetricsService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<SystemMetricsDto> GetSystemMetricsAsync(TimeSpan? period = null)
    {
        period ??= TimeSpan.FromMinutes(15);
        var startTime = DateTime.UtcNow - period.Value;

        var metrics = new SystemMetricsDto
        {
            GeneratedAt = DateTime.UtcNow,
            CollectionPeriod = period.Value,
            ServiceMetrics = new List<ServiceMetricsDto>(),
            SystemResources = await GetSystemResourceMetricsAsync(),
            DatabaseMetrics = await GetDatabaseMetricsAsync(startTime),
            MessageQueueMetrics = await GetMessageQueueMetricsAsync()
        };

        // Collect metrics for each service
        var serviceNames = _serviceMetrics.Keys.ToList();
        foreach (var serviceName in serviceNames)
        {
            var serviceMetric = await GetServiceMetricsAsync(serviceName, period);
            if (serviceMetric != null)
                metrics.ServiceMetrics.Add(serviceMetric);
        }

        return metrics;
    }

    public Task RecordRequestAsync(string serviceName, string endpoint, double responseTimeMs, bool isSuccess)
    {
        var record = new MetricRecord
        {
            ServiceName = serviceName,
            Endpoint = endpoint,
            ResponseTimeMs = responseTimeMs,
            IsSuccess = isSuccess,
            Timestamp = DateTime.UtcNow
        };

        var queue = _serviceMetrics.GetOrAdd(serviceName, _ => new ConcurrentQueue<MetricRecord>());
        queue.Enqueue(record);

        // Keep only last 10000 records per service
        while (queue.Count > 10000)
        {
            queue.TryDequeue(out _);
        }

        return Task.CompletedTask;
    }

    public async Task<ServiceMetricsDto> GetServiceMetricsAsync(string serviceName, TimeSpan? period = null)
    {
        period ??= TimeSpan.FromMinutes(15);
        var cutoffTime = DateTime.UtcNow - period.Value;

        if (!_serviceMetrics.TryGetValue(serviceName, out var queue))
        {
            return new ServiceMetricsDto { ServiceName = serviceName };
        }

        var records = queue.Where(r => r.Timestamp >= cutoffTime).ToList();

        if (records.Count == 0)
        {
            return new ServiceMetricsDto { ServiceName = serviceName };
        }

        var totalRequests = records.Count;
        var successCount = records.Count(r => r.IsSuccess);
        var errorCount = totalRequests - successCount;
        var responseTimes = records.Select(r => r.ResponseTimeMs).OrderBy(rt => rt).ToList();

        var metrics = new ServiceMetricsDto
        {
            ServiceName = serviceName,
            RequestCount = totalRequests,
            SuccessCount = successCount,
            ErrorCount = errorCount,
            ErrorRate = totalRequests > 0 ? (double)errorCount / totalRequests * 100 : 0,
            AverageResponseTimeMs = responseTimes.Average(),
            P95ResponseTimeMs = responseTimes.Count > 0 ? responseTimes[(int)(responseTimes.Count * 0.95)] : 0,
            P99ResponseTimeMs = responseTimes.Count > 0 ? responseTimes[(int)(responseTimes.Count * 0.99)] : 0,
            EndpointRequestCounts = new Dictionary<string, long>(),
            EndpointAverageResponseTimes = new Dictionary<string, double>(),
            EndpointErrorRates = new Dictionary<string, double>()
        };

        // Group by endpoint
        var endpointGroups = records.GroupBy(r => r.Endpoint);
        foreach (var group in endpointGroups)
        {
            var endpointRecords = group.ToList();
            var endpointTotal = endpointRecords.Count;
            var endpointErrors = endpointRecords.Count(r => !r.IsSuccess);
            
            metrics.EndpointRequestCounts[group.Key] = endpointTotal;
            metrics.EndpointAverageResponseTimes[group.Key] = endpointRecords.Average(r => r.ResponseTimeMs);
            metrics.EndpointErrorRates[group.Key] = endpointTotal > 0 ? (double)endpointErrors / endpointTotal * 100 : 0;
        }

        return await Task.FromResult(metrics);
    }

    private async Task<SystemResourceMetricsDto> GetSystemResourceMetricsAsync()
    {
        return await Task.Run(() =>
        {
            var process = Process.GetCurrentProcess();
            
            // CPU usage (simplified - in production use PerformanceCounter)
            var cpuUsage = 0.0;
            try
            {
                var totalProcessorTime = process.TotalProcessorTime;
                cpuUsage = Math.Min(100, totalProcessorTime.TotalMilliseconds / 1000); // Simplified
            }
            catch
            {
                // Ignore errors
            }

            // Memory usage
            var memoryUsage = process.WorkingSet64;
            var memoryTotal = GC.GetTotalMemory(false);

            // Disk usage
            var drive = new DriveInfo(Path.GetPathRoot(Environment.CurrentDirectory) ?? Environment.CurrentDirectory);
            var diskUsage = drive.TotalSize - drive.AvailableFreeSpace;
            var diskTotal = drive.TotalSize;

            return new SystemResourceMetricsDto
            {
                CpuUsagePercent = cpuUsage,
                MemoryUsageBytes = memoryUsage,
                MemoryTotalBytes = memoryTotal,
                MemoryUsagePercent = memoryTotal > 0 ? (double)memoryUsage / memoryTotal * 100 : 0,
                DiskUsageBytes = diskUsage,
                DiskTotalBytes = diskTotal,
                DiskUsagePercent = diskTotal > 0 ? (double)diskUsage / diskTotal * 100 : 0,
                ActiveConnections = 0, // Would need to track connections
                ThreadCount = process.Threads.Count
            };
        });
    }

    private async Task<DatabaseMetricsDto> GetDatabaseMetricsAsync(DateTime since)
    {
        try
        {
            // Get connection info
            var connection = _context.Database.GetDbConnection();
            var activeConnections = 0L;

            try
            {
                // Try to get active connections (SQL Server specific)
                var connectionString = connection.ConnectionString;
                if (connectionString.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
                {
                    var command = connection.CreateCommand();
                    command.CommandText = @"
                        SELECT COUNT(*) 
                        FROM sys.dm_exec_sessions 
                        WHERE database_id = DB_ID()";
                    
                    if (connection.State != System.Data.ConnectionState.Open)
                        await connection.OpenAsync();
                    
                    var result = await command.ExecuteScalarAsync();
                    activeConnections = Convert.ToInt64(result ?? 0);
                }
            }
            catch
            {
                // Ignore errors getting connection count
            }

            // Get query metrics from audit logs (simplified)
            var totalQueries = await _context.AuditLogs
                .Where(a => a.Timestamp >= since && a.Action.Contains("Query", StringComparison.OrdinalIgnoreCase))
                .CountAsync();

            return new DatabaseMetricsDto
            {
                TotalQueries = totalQueries,
                AverageQueryTimeMs = 0, // Would need query timing middleware
                P95QueryTimeMs = 0,
                SlowQueries = 0,
                ActiveConnections = activeConnections,
                ConnectionPoolSize = 100, // Default pool size
                QueryCountsByEntity = new Dictionary<string, long>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting database metrics");
            return new DatabaseMetricsDto();
        }
    }

    private async Task<MessageQueueMetricsDto> GetMessageQueueMetricsAsync()
    {
        // In production, query RabbitMQ management API or use MassTransit metrics
        await Task.CompletedTask;
        return new MessageQueueMetricsDto
        {
            QueueName = "default",
            QueueDepth = 0,
            MessagesProcessed = 0,
            AverageProcessingTimeMs = 0,
            FailedMessages = 0,
            FailureRate = 0
        };
    }

    private class MetricRecord
    {
        public string ServiceName { get; set; } = string.Empty;
        public string Endpoint { get; set; } = string.Empty;
        public double ResponseTimeMs { get; set; }
        public bool IsSuccess { get; set; }
        public DateTime Timestamp { get; set; }
    }
}

