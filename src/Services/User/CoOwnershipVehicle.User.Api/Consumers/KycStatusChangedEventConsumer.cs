using MassTransit;
using CoOwnershipVehicle.Shared.Contracts.Events;
using CoOwnershipVehicle.User.Api.Services;
using CoOwnershipVehicle.User.Api.Data;
using Microsoft.EntityFrameworkCore;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.User.Api.Consumers;

public class KycStatusChangedEventConsumer : IConsumer<UserKycStatusChangedEvent>
{
    private readonly IEmailService _emailService;
    private readonly UserDbContext _context;
    private readonly ILogger<KycStatusChangedEventConsumer> _logger;
    private readonly IConfiguration _configuration;

    public KycStatusChangedEventConsumer(
        IEmailService emailService,
        UserDbContext context,
        ILogger<KycStatusChangedEventConsumer> logger,
        IConfiguration configuration)
    {
        _emailService = emailService;
        _context = context;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task Consume(ConsumeContext<UserKycStatusChangedEvent> context)
    {
        var message = context.Message;
        
        _logger.LogInformation("Processing UserKycStatusChangedEvent - UserId: {UserId}, OldStatus: {OldStatus}, NewStatus: {NewStatus}", 
            message.UserId, message.OldStatus, message.NewStatus);

        try
        {
            // Get user information
            var user = await _context.UserProfiles
                .FirstOrDefaultAsync(u => u.Id == message.UserId);

            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found for KYC status change notification", message.UserId);
                return;
            }

            if (string.IsNullOrEmpty(user.Email))
            {
                _logger.LogWarning("User {UserId} has no email address, cannot send KYC notification", message.UserId);
                return;
            }

            // Generate KYC URL
            var frontendUrl = _configuration["FrontendSettings:BaseUrl"] ?? "http://localhost:5173";
            var kycUrl = $"{frontendUrl}/kyc-verification";

            var firstName = user.FirstName ?? "Người dùng";

            // Send email based on new status
            if (message.NewStatus == KycStatus.Approved)
            {
                // Only send approved email if status changed TO Approved (not if it was already approved)
                if (message.OldStatus != KycStatus.Approved)
                {
                    var success = await _emailService.SendKycApprovedEmailAsync(
                        user.Email,
                        firstName,
                        kycUrl);

                    if (success)
                    {
                        _logger.LogInformation("Successfully sent KYC approved email to user {UserId} ({Email})",
                            message.UserId, user.Email);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to send KYC approved email to user {UserId} ({Email})",
                            message.UserId, user.Email);
                    }
                }
            }
            else if (message.NewStatus == KycStatus.Rejected)
            {
                // Send rejection email when status changes TO Rejected
                var reason = message.Reason ?? "Tài liệu KYC không đáp ứng yêu cầu. Vui lòng kiểm tra và cập nhật lại.";
                
                var success = await _emailService.SendKycRejectedEmailAsync(
                    user.Email,
                    firstName,
                    reason,
                    kycUrl);

                if (success)
                {
                    _logger.LogInformation("Successfully sent KYC rejection email to user {UserId} ({Email})",
                        message.UserId, user.Email);
                }
                else
                {
                    _logger.LogWarning("Failed to send KYC rejection email to user {UserId} ({Email})",
                        message.UserId, user.Email);
                }
            }
            else if (message.NewStatus == KycStatus.Pending && 
                     !string.IsNullOrEmpty(message.Reason) && 
                     message.Reason.Contains("require", StringComparison.OrdinalIgnoreCase))
            {
                // Send rejection/update email when status changes to Pending with reason indicating requires update
                // (not for "No documents uploaded" which is initial state)
                var reason = message.Reason ?? "Tài liệu KYC cần được cập nhật. Vui lòng kiểm tra và cập nhật lại.";
                
                var success = await _emailService.SendKycRejectedEmailAsync(
                    user.Email,
                    firstName,
                    reason,
                    kycUrl);

                if (success)
                {
                    _logger.LogInformation("Successfully sent KYC update required email to user {UserId} ({Email})",
                        message.UserId, user.Email);
                }
                else
                {
                    _logger.LogWarning("Failed to send KYC update required email to user {UserId} ({Email})",
                        message.UserId, user.Email);
                }
            }
            else
            {
                _logger.LogDebug("KYC status changed from {OldStatus} to {NewStatus}, no email notification needed", 
                    message.OldStatus, message.NewStatus);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing UserKycStatusChangedEvent for user {UserId}", message.UserId);
            throw;
        }
    }
}

