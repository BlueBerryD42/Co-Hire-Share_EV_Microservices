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
        
        _logger.LogInformation("UserRegisteredEvent received - UserId: {UserId}, Email: {Email}, FirstName: {FirstName}, LastName: {LastName}, Phone: {Phone}, Role: {Role}", 
            message.UserId, message.Email, message.FirstName, message.LastName, message.Phone, message.Role);
        
        try
        {
            // Verify database connection and table exists
            _logger.LogInformation("Checking database connection and UserProfiles table...");
            
            // Check if user already exists in our local database
            // Use explicit table name to avoid any EF Core caching issues
            var existingUser = await _context.UserProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == message.UserId);
            
            _logger.LogInformation("Checking for existing user profile - UserId: {UserId}, Found: {Found}", 
                message.UserId, existingUser != null);
            
            if (existingUser == null)
            {
                // Create new user profile in User service database
                // NOTE: User service should NOT store authentication data (passwords, tokens, etc.)
                // Authentication is handled exclusively by the Auth service
                var newUserProfile = new Domain.Entities.UserProfile
                {
                    Id = message.UserId,
                    UserName = message.Email,
                    Email = message.Email,
                    NormalizedEmail = message.Email?.ToUpperInvariant(),
                    NormalizedUserName = message.Email?.ToUpperInvariant(),
                    FirstName = message.FirstName,
                    LastName = message.LastName,
                    Phone = message.Phone, // Phone from registration event (profile field)
                    Role = (Domain.Entities.UserRole)message.Role,
                    KycStatus = Domain.Entities.KycStatus.Pending,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    // Authentication fields are NOT set - they belong only in Auth service
                    EmailConfirmed = false,
                    TwoFactorEnabled = false,
                    LockoutEnabled = true,
                    AccessFailedCount = 0
                    // PasswordHash, SecurityStamp, PhoneNumber, PhoneNumberConfirmed are intentionally omitted
                };

                _context.UserProfiles.Add(newUserProfile);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("User profile created in User service database for {UserId} - {Email}", 
                    message.UserId, message.Email);
            }
            else
            {
                // Update existing user profile if needed
                existingUser.Email = message.Email;
                existingUser.FirstName = message.FirstName;
                existingUser.LastName = message.LastName;
                existingUser.Phone = message.Phone ?? existingUser.Phone;
                existingUser.Role = (Domain.Entities.UserRole)message.Role;
                existingUser.UpdatedAt = DateTime.UtcNow;
                
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("User profile data synchronized for existing user {UserId}", message.UserId);
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
