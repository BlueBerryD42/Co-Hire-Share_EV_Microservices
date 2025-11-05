using System.Diagnostics;
using CoOwnershipVehicle.Admin.Api.Services;

namespace CoOwnershipVehicle.Admin.Api.Middleware;

public class PerformanceTrackingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PerformanceTrackingMiddleware> _logger;

    public PerformanceTrackingMiddleware(RequestDelegate next, ILogger<PerformanceTrackingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ISystemMetricsService metricsService)
    {
        // Skip tracking for health/status endpoints to avoid recursion
        var path = context.Request.Path.Value ?? "";
        if (path.Contains("/health") || path.Contains("/status") || path.Contains("/metrics"))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var method = context.Request.Method;
        var endpoint = $"{method} {path}";
        var isSuccess = false;

        try
        {
            await _next(context);
            
            isSuccess = context.Response.StatusCode < 400;
            stopwatch.Stop();

            // Record metrics
            var serviceName = "Admin";
            await metricsService.RecordRequestAsync(serviceName, endpoint, stopwatch.Elapsed.TotalMilliseconds, isSuccess);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error processing request: {Method} {Path}", method, path);
            
            // Record as error
            var serviceName = "Admin";
            await metricsService.RecordRequestAsync(serviceName, endpoint, stopwatch.Elapsed.TotalMilliseconds, false);
            
            throw;
        }
        finally
        {
            if (stopwatch.Elapsed.TotalMilliseconds > 1000)
            {
                _logger.LogWarning("Slow request detected: {Method} {Path} took {ElapsedMs}ms", 
                    method, path, stopwatch.Elapsed.TotalMilliseconds);
            }
        }
    }
}

public static class PerformanceTrackingMiddlewareExtensions
{
    public static IApplicationBuilder UsePerformanceTracking(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<PerformanceTrackingMiddleware>();
    }
}

