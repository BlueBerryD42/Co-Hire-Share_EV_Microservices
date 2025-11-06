using CoOwnershipVehicle.Group.Api.Contracts;
using CoOwnershipVehicle.Group.Api.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CoOwnershipVehicle.Group.Api.BackgroundServices;

public class ProposalExpirationBackgroundService : BackgroundService
{
    private static readonly TimeSpan ExecutionInterval = TimeSpan.FromHours(1); // Check every hour

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProposalExpirationBackgroundService> _logger;

    public ProposalExpirationBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<ProposalExpirationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Proposal expiration background service starting with interval {Interval}", ExecutionInterval);

        // Wait for the application to fully start before processing
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        using var timer = new PeriodicTimer(ExecutionInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredProposalsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing expired proposals");
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Proposal expiration background service stopping");
    }

    private async Task ProcessExpiredProposalsAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var votingService = scope.ServiceProvider.GetRequiredService<IVotingService>();

        try
        {
            await votingService.ProcessExpiredProposalsAsync();
            _logger.LogDebug("Processed expired proposals successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing expired proposals");
        }
    }
}




