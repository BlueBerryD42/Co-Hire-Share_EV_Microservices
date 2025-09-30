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

            // Update user profile data in Auth service
            user.FirstName = message.FirstName;
            user.LastName = message.LastName;
            user.Phone = message.Phone;
            user.Address = message.Address;
            user.City = message.City;
            user.Country = message.Country;
            user.PostalCode = message.PostalCode;
            user.DateOfBirth = message.DateOfBirth;
            user.Role = (Domain.Entities.UserRole)message.Role;
            user.KycStatus = (Domain.Entities.KycStatus)message.KycStatus;
            user.UpdatedAt = message.UpdatedAt;

            // Save changes using UserManager to ensure proper Identity handling
            var result = await _userManager.UpdateAsync(user);
            
            if (result.Succeeded)
            {
                _logger.LogInformation("Successfully synchronized user profile data for user {UserId}", message.UserId);
            }
            else
            {
                _logger.LogError("Failed to update user profile in Auth service for user {UserId}. Errors: {Errors}", 
                    message.UserId, string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing user profile updated event for user {UserId}", message.UserId);
            throw; // Re-throw to trigger retry mechanism
        }
    }
}
