using CoOwnershipVehicle.Data;
using Microsoft.EntityFrameworkCore;

namespace CoOwnershipVehicle.Analytics.Api.Services;

public class AnalyticsProcessorService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AnalyticsProcessorService> _logger;
    private readonly TimeSpan _processingInterval = TimeSpan.FromHours(1);

    public AnalyticsProcessorService(IServiceProvider serviceProvider, ILogger<AnalyticsProcessorService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDailyAnalytics();
                await ProcessWeeklyAnalytics();
                await ProcessMonthlyAnalytics();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in analytics processor service");
            }

            await Task.Delay(_processingInterval, stoppingToken);
        }
    }

    private async Task ProcessDailyAnalytics()
    {
        using var scope = _serviceProvider.CreateScope();
        var analyticsService = scope.ServiceProvider.GetRequiredService<IAnalyticsService>();

        try
        {
            await analyticsService.GeneratePeriodicAnalyticsAsync(Domain.Entities.AnalyticsPeriod.Daily);
            _logger.LogInformation("Daily analytics processed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing daily analytics");
        }
    }

    private async Task ProcessWeeklyAnalytics()
    {
        // Process weekly analytics on Sundays
        if (DateTime.UtcNow.DayOfWeek == DayOfWeek.Sunday)
        {
            using var scope = _serviceProvider.CreateScope();
            var analyticsService = scope.ServiceProvider.GetRequiredService<IAnalyticsService>();

            try
            {
                await analyticsService.GeneratePeriodicAnalyticsAsync(Domain.Entities.AnalyticsPeriod.Weekly);
                _logger.LogInformation("Weekly analytics processed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing weekly analytics");
            }
        }
    }

    private async Task ProcessMonthlyAnalytics()
    {
        // Process monthly analytics on the first day of the month
        if (DateTime.UtcNow.Day == 1)
        {
            using var scope = _serviceProvider.CreateScope();
            var analyticsService = scope.ServiceProvider.GetRequiredService<IAnalyticsService>();

            try
            {
                await analyticsService.GeneratePeriodicAnalyticsAsync(Domain.Entities.AnalyticsPeriod.Monthly);
                _logger.LogInformation("Monthly analytics processed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing monthly analytics");
            }
        }
    }
}
