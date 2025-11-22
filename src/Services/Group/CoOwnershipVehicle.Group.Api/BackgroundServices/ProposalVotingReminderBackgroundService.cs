using CoOwnershipVehicle.Group.Api.Data;
using CoOwnershipVehicle.Group.Api.Services.Interfaces;
using CoOwnershipVehicle.Group.Api.Services;
using CoOwnershipVehicle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace CoOwnershipVehicle.Group.Api.BackgroundServices;

public class ProposalVotingReminderBackgroundService : BackgroundService
{
    private static readonly TimeSpan ExecutionInterval = TimeSpan.FromMinutes(30); // Check every 30 minutes
    private static readonly TimeSpan ReminderThreshold = TimeSpan.FromHours(12); // 12 hours before end

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProposalVotingReminderBackgroundService> _logger;

    public ProposalVotingReminderBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<ProposalVotingReminderBackgroundService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Proposal voting reminder background service starting with interval {Interval}", ExecutionInterval);

        // Wait for the application to fully start before processing
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        using var timer = new PeriodicTimer(ExecutionInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessVotingRemindersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing voting reminders");
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

        _logger.LogInformation("Proposal voting reminder background service stopping");
    }

    private async Task ProcessVotingRemindersAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GroupDbContext>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var userServiceClient = scope.ServiceProvider.GetRequiredService<IUserServiceClient>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        try
        {
            var now = DateTime.UtcNow;
            var reminderTime = now.Add(ReminderThreshold);

            // Find active proposals that will end within the next 12 hours (but haven't ended yet)
            var proposalsNeedingReminders = await context.Proposals
                .Include(p => p.Group)
                .Where(p => p.Status == ProposalStatus.Active &&
                           p.VotingEndDate > now &&
                           p.VotingEndDate <= reminderTime)
                .ToListAsync(stoppingToken);

            if (!proposalsNeedingReminders.Any())
            {
                _logger.LogDebug("No proposals need voting reminders at this time");
                return;
            }

            _logger.LogInformation("Found {Count} proposals needing voting reminders", proposalsNeedingReminders.Count);

            foreach (var proposal in proposalsNeedingReminders)
            {
                try
                {
                    // Get all group members
                    var groupMembers = await context.GroupMembers
                        .Where(m => m.GroupId == proposal.GroupId)
                        .Select(m => m.UserId)
                        .ToListAsync(stoppingToken);

                    if (!groupMembers.Any())
                    {
                        _logger.LogWarning("No members found for group {GroupId} in proposal {ProposalId}",
                            proposal.GroupId, proposal.Id);
                        continue;
                    }

                    // Get users who have already voted
                    var votedUserIds = await context.Votes
                        .Where(v => v.ProposalId == proposal.Id)
                        .Select(v => v.VoterId)
                        .Distinct()
                        .ToListAsync(stoppingToken);

                    // Find members who haven't voted yet
                    var nonVoterIds = groupMembers.Except(votedUserIds).ToList();

                    if (!nonVoterIds.Any())
                    {
                        _logger.LogDebug("All members have voted on proposal {ProposalId}", proposal.Id);
                        continue;
                    }

                    // Fetch user data for non-voters
                    var users = await userServiceClient.GetUsersAsync(nonVoterIds, string.Empty);
                    var nonVoterDtos = nonVoterIds
                        .Where(uid => users.ContainsKey(uid))
                        .Select(uid => users[uid])
                        .ToList();

                    if (!nonVoterDtos.Any())
                    {
                        _logger.LogWarning("No valid user data found for non-voters in proposal {ProposalId}", proposal.Id);
                        continue;
                    }

                    // Generate proposal URL
                    var frontendUrl = configuration["FrontendSettings:BaseUrl"] ?? "http://localhost:5173";
                    var proposalUrl = $"{frontendUrl}/groups/{proposal.GroupId}/proposals/{proposal.Id}";

                    // Send reminders to each non-voter
                    var reminderTasks = nonVoterDtos.Select(member =>
                        notificationService.SendProposalVotingReminderAsync(
                            member,
                            proposal.Title,
                            proposal.Id,
                            proposal.GroupId,
                            proposal.Group?.Name ?? "Unknown Group",
                            proposal.VotingEndDate,
                            proposalUrl));

                    var results = await Task.WhenAll(reminderTasks);
                    var successCount = results.Count(r => r);

                    _logger.LogInformation(
                        "Sent voting reminders for proposal {ProposalId} to {SentCount}/{TotalCount} non-voters",
                        proposal.Id, successCount, nonVoterDtos.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing voting reminder for proposal {ProposalId}", proposal.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing voting reminders");
        }
    }
}

