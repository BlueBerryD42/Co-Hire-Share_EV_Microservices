using MassTransit;
using CoOwnershipVehicle.Shared.Contracts.Events;
using CoOwnershipVehicle.Auth.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace CoOwnershipVehicle.Auth.Api.Consumers;

public class UserProfileUpdatedConsumer : IConsumer<UserProfileUpdatedEvent>
{
    private readonly AuthDbContext _context;
    private readonly UserManager<Domain.Entities.User> _userManager;
    private readonly ILogger<UserProfileUpdatedConsumer> _logger;

    public UserProfileUpdatedConsumer(
        AuthDbContext context, 
        UserManager<Domain.Entities.User> userManager,
        ILogger<UserProfileUpdatedConsumer> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<UserProfileUpdatedEvent> context)
    {
        var message = context.Message;
        
        try
        {
            _logger.LogInformation("Received user profile updated event for user {UserId}", message.UserId);

            // Find the user in the Auth service database
            var user = await _userManager.FindByIdAsync(message.UserId.ToString());
            
            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found in Auth service database during profile update sync", message.UserId);
                return;
            }

            // Auth DB no longer stores profile data - all profile data is in User service database
            // This consumer is kept for backward compatibility but does nothing
            // Profile data will be fetched from User service when needed (e.g., for JWT claims)
            _logger.LogInformation("User profile updated event received for {UserId}. Auth DB no longer stores profile data - skipping sync.", message.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing user profile updated event for user {UserId}", message.UserId);
            throw; // Re-throw to trigger retry mechanism
        }
    }
}
