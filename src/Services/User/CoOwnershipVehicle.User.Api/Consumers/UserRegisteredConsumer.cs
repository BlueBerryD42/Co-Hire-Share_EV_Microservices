using MassTransit;
using CoOwnershipVehicle.Shared.Contracts.Events;
using CoOwnershipVehicle.User.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CoOwnershipVehicle.User.Api.Consumers;

public class UserRegisteredConsumer : IConsumer<UserRegisteredEvent>
{
    private readonly UserDbContext _context;
    private readonly ILogger<UserRegisteredConsumer> _logger;

    public UserRegisteredConsumer(UserDbContext context, ILogger<UserRegisteredConsumer> logger)
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
                // Create new user in User service database
                // NOTE: User service should NOT store authentication data (passwords, tokens, etc.)
                // Authentication is handled exclusively by the Auth service
                var newUser = new Domain.Entities.User
                {
                    Id = message.UserId,
                    UserName = message.Email,
                    Email = message.Email,
                    FirstName = message.FirstName,
                    LastName = message.LastName,
                    Role = (Domain.Entities.UserRole)message.Role,
                    KycStatus = Domain.Entities.KycStatus.Pending,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    // Authentication fields are NOT set - they belong only in Auth service
                    EmailConfirmed = false,
                    PhoneNumberConfirmed = false,
                    TwoFactorEnabled = false,
                    LockoutEnabled = true,
                    AccessFailedCount = 0
                    // PasswordHash, SecurityStamp, etc. are intentionally omitted
                };

                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("User created in User service database for {UserId} - {Email}", 
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
