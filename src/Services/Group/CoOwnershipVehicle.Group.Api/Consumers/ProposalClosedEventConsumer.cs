using MassTransit;
using CoOwnershipVehicle.Shared.Contracts.Events;
using CoOwnershipVehicle.Group.Api.Services.Interfaces;
using CoOwnershipVehicle.Group.Api.Data;
using CoOwnershipVehicle.Group.Api.Services;
using Microsoft.EntityFrameworkCore;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Group.Api.Consumers;

public class ProposalClosedEventConsumer : IConsumer<ProposalClosedEvent>
{
    private readonly INotificationService _notificationService;
    private readonly GroupDbContext _context;
    private readonly IUserServiceClient _userServiceClient;
    private readonly ILogger<ProposalClosedEventConsumer> _logger;
    private readonly IConfiguration _configuration;

    public ProposalClosedEventConsumer(
        INotificationService notificationService,
        GroupDbContext context,
        IUserServiceClient userServiceClient,
        ILogger<ProposalClosedEventConsumer> logger,
        IConfiguration configuration)
    {
        _notificationService = notificationService;
        _context = context;
        _userServiceClient = userServiceClient;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task Consume(ConsumeContext<ProposalClosedEvent> context)
    {
        var message = context.Message;
        
        _logger.LogInformation("Processing ProposalClosedEvent - ProposalId: {ProposalId}, GroupId: {GroupId}, Passed: {Passed}", 
            message.ProposalId, message.GroupId, message.Passed);

        // Only send notification if proposal passed
        if (!message.Passed)
        {
            _logger.LogInformation("Proposal {ProposalId} was rejected, skipping admin notification", message.ProposalId);
            return;
        }

        try
        {
            // Get proposal details
            var proposal = await _context.Proposals
                .FirstOrDefaultAsync(p => p.Id == message.ProposalId);

            if (proposal == null)
            {
                _logger.LogWarning("Proposal {ProposalId} not found", message.ProposalId);
                return;
            }

            // Get group information
            var group = await _context.OwnershipGroups
                .FirstOrDefaultAsync(g => g.Id == message.GroupId);

            if (group == null)
            {
                _logger.LogWarning("Group {GroupId} not found for proposal {ProposalId}", 
                    message.GroupId, message.ProposalId);
                return;
            }

            // Get group admins only
            var adminMembers = await _context.GroupMembers
                .Where(m => m.GroupId == message.GroupId && m.RoleInGroup == GroupRole.Admin)
                .Select(m => m.UserId)
                .ToListAsync();

            if (!adminMembers.Any())
            {
                _logger.LogWarning("No admin members found for group {GroupId}", message.GroupId);
                return;
            }

            // Fetch user data for admins
            var users = await _userServiceClient.GetUsersAsync(adminMembers, string.Empty);
            var adminDtos = adminMembers
                .Where(uid => users.ContainsKey(uid))
                .Select(uid => users[uid])
                .ToList();

            if (!adminDtos.Any())
            {
                _logger.LogWarning("No valid user data found for group {GroupId} admins", message.GroupId);
                return;
            }

            // Get proposal type label
            var proposalTypeLabel = proposal.Type switch
            {
                ProposalType.MaintenanceBudget => "Ngân sách bảo trì",
                ProposalType.VehicleUpgrade => "Nâng cấp xe",
                ProposalType.VehicleSale => "Bán xe",
                ProposalType.PolicyChange => "Thay đổi quy tắc",
                ProposalType.MembershipChange => "Thành viên",
                ProposalType.Other => "Khác",
                _ => proposal.Type.ToString()
            };

            // Generate proposal URL
            var frontendUrl = _configuration["FrontendSettings:BaseUrl"] ?? "http://localhost:5173";
            var proposalUrl = $"{frontendUrl}/groups/{message.GroupId}/proposals/{message.ProposalId}";

            // Send notification to group admins
            var success = await _notificationService.SendProposalPassedNotificationAsync(
                adminDtos,
                proposal.Title,
                message.ProposalId,
                message.GroupId,
                group.Name,
                proposalTypeLabel,
                proposal.Amount,
                proposalUrl);

            if (success)
            {
                _logger.LogInformation("Successfully sent proposal passed notifications for proposal {ProposalId} to {Count} admins",
                    message.ProposalId, adminDtos.Count);
            }
            else
            {
                _logger.LogWarning("Failed to send some proposal passed notifications for proposal {ProposalId}",
                    message.ProposalId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing ProposalClosedEvent for proposal {ProposalId}", message.ProposalId);
            throw;
        }
    }
}

