using MassTransit;
using CoOwnershipVehicle.Shared.Contracts.Events;
using CoOwnershipVehicle.Group.Api.Services.Interfaces;
using CoOwnershipVehicle.Group.Api.Data;
using CoOwnershipVehicle.Group.Api.Services;
using Microsoft.EntityFrameworkCore;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Group.Api.Consumers;

public class ProposalCreatedEventConsumer : IConsumer<ProposalCreatedEvent>
{
    private readonly INotificationService _notificationService;
    private readonly GroupDbContext _context;
    private readonly IUserServiceClient _userServiceClient;
    private readonly ILogger<ProposalCreatedEventConsumer> _logger;
    private readonly IConfiguration _configuration;

    public ProposalCreatedEventConsumer(
        INotificationService notificationService,
        GroupDbContext context,
        IUserServiceClient userServiceClient,
        ILogger<ProposalCreatedEventConsumer> logger,
        IConfiguration configuration)
    {
        _notificationService = notificationService;
        _context = context;
        _userServiceClient = userServiceClient;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task Consume(ConsumeContext<ProposalCreatedEvent> context)
    {
        var message = context.Message;
        
        _logger.LogInformation("Processing ProposalCreatedEvent - ProposalId: {ProposalId}, GroupId: {GroupId}", 
            message.ProposalId, message.GroupId);

        try
        {
            // Get group information
            var group = await _context.OwnershipGroups
                .FirstOrDefaultAsync(g => g.Id == message.GroupId);

            if (group == null)
            {
                _logger.LogWarning("Group {GroupId} not found for proposal {ProposalId}", 
                    message.GroupId, message.ProposalId);
                return;
            }

            // Get all group members
            var groupMembers = await _context.GroupMembers
                .Where(m => m.GroupId == message.GroupId)
                .Select(m => m.UserId)
                .ToListAsync();

            if (!groupMembers.Any())
            {
                _logger.LogWarning("No members found for group {GroupId}", message.GroupId);
                return;
            }

            // Fetch user data
            var users = await _userServiceClient.GetUsersAsync(groupMembers, string.Empty);
            var memberDtos = groupMembers
                .Where(uid => users.ContainsKey(uid))
                .Select(uid => users[uid])
                .ToList();

            if (!memberDtos.Any())
            {
                _logger.LogWarning("No valid user data found for group {GroupId} members", message.GroupId);
                return;
            }

            // Generate proposal URL
            var frontendUrl = _configuration["FrontendSettings:BaseUrl"] ?? "http://localhost:5173";
            var proposalUrl = $"{frontendUrl}/groups/{message.GroupId}/proposals/{message.ProposalId}";

            // Send notification to all group members
            var success = await _notificationService.SendProposalStartedNotificationAsync(
                memberDtos,
                message.Title,
                message.ProposalId,
                message.GroupId,
                group.Name,
                message.VotingEndDate,
                proposalUrl);

            if (success)
            {
                _logger.LogInformation("Successfully sent proposal started notifications for proposal {ProposalId} to {Count} members",
                    message.ProposalId, memberDtos.Count);
            }
            else
            {
                _logger.LogWarning("Failed to send some proposal started notifications for proposal {ProposalId}",
                    message.ProposalId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing ProposalCreatedEvent for proposal {ProposalId}", message.ProposalId);
            throw;
        }
    }
}

