using MassTransit;
using CoOwnershipVehicle.Shared.Contracts.Events;
using CoOwnershipVehicle.Data;
using Microsoft.EntityFrameworkCore;

namespace CoOwnershipVehicle.User.Api.Consumers;

public class UserRegisteredConsumer : IConsumer<UserRegisteredEvent>
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<UserRegisteredConsumer> _logger;

    public UserRegisteredConsumer(ApplicationDbContext context, ILogger<UserRegisteredConsumer> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<UserRegisteredEvent> context)
    {
        var message = context.Message;
        
        try
        {
            // Check if user already exists in our local database
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == message.UserId);
            
            if (existingUser == null)
            {
                // User doesn't exist in our local database, which is expected for microservices
                // We'll sync the user data when they access our service
                _logger.LogInformation("User registration event received for new user {UserId} - {Email}", 
                    message.UserId, message.Email);
            }
            else
            {
                // Update existing user if needed
                existingUser.Email = message.Email;
                existingUser.FirstName = message.FirstName;
                existingUser.LastName = message.LastName;
                existingUser.Role = (Domain.Entities.UserRole)message.Role;
                existingUser.UpdatedAt = DateTime.UtcNow;
                
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("User data synchronized for existing user {UserId}", message.UserId);
            }

            // In a real microservices setup, we might:
            // 1. Create a local user profile entry
            // 2. Send welcome email
            // 3. Initialize default settings
            // 4. Create user workspace
            
            _logger.LogInformation("Successfully processed UserRegisteredEvent for user {UserId}", message.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing UserRegisteredEvent for user {UserId}", message.UserId);
            throw; // This will trigger retry logic in MassTransit
        }
    }
}
