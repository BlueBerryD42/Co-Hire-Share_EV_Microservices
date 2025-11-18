using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Auth.Api.Services;
using CoOwnershipVehicle.Shared.Contracts.Events;
using MassTransit;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace CoOwnershipVehicle.Auth.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly IJwtTokenService _tokenService;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IEmailService _emailService;
    private readonly ILogger<AuthController> _logger;
    private readonly IConfiguration _configuration;

    public AuthController(
         UserManager<User> userManager,
         SignInManager<User> signInManager,
         IJwtTokenService tokenService,
         IPublishEndpoint publishEndpoint,
         IEmailService emailService,
         ILogger<AuthController> logger,
         IConfiguration configuration) 
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _publishEndpoint = publishEndpoint;
        _emailService = emailService;
        _logger = logger;
        _configuration = configuration; 
    }

    /// <summary>
    /// Register a new user
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] CreateUserDto request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var frontendUrl = _configuration["EmailSettings:FrontendUrl"]
                        ?? Environment.GetEnvironmentVariable("FRONTEND_URL");
            _logger.LogInformation("FRONTEND_URL is: {FrontendUrl}", frontendUrl);

            // Check if user already exists
            var existingUser = await _userManager.FindByEmailAsync(request.Email);
            if (existingUser != null)
            {
                return BadRequest(new { message = "User with this email already exists" });
            }

            // Create new user in Auth database
            // IMPORTANT: Auth DB stores AUTHENTICATION DATA ONLY:
            // - Required for authentication: Email, UserName, PasswordHash, SecurityStamp, EmailConfirmed
            // - NO profile data stored here - ALL profile data is stored in User service database via UserRegisteredEvent
            var user = new User
            {
                Id = Guid.NewGuid(),
                UserName = request.Email,
                Email = request.Email,
                PhoneNumber = request.Phone, // Identity's PhoneNumber field (for 2FA, not profile)
                EmailConfirmed = false // Will be confirmed via email verification
                // Profile fields (FirstName, LastName, Phone, Role, KycStatus, etc.) are NOT stored in Auth DB
                // They are stored ONLY in User service database
            };

            // UserManager.CreateAsync will automatically:
            // - Hash the password and set PasswordHash
            // - Normalize UserName and Email (set NormalizedUserName, NormalizedEmail)
            // - Generate SecurityStamp
            // - Generate ConcurrencyStamp
            var result = await _userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
            {
                return BadRequest(new { message = "Failed to create user", errors = result.Errors });
            }

            // Reload user from database to ensure all Identity fields are populated
            user = await _userManager.FindByIdAsync(user.Id.ToString());
            if (user == null)
            {
                _logger.LogError("User was created but could not be reloaded from database");
                return StatusCode(500, new { message = "User created but could not be verified" });
            }

            // Log field population for debugging
            _logger.LogInformation("User created - Id: {UserId}, Email: {Email}, NormalizedEmail: {NormalizedEmail}, NormalizedUserName: {NormalizedUserName}, HasPasswordHash: {HasPasswordHash}, HasSecurityStamp: {HasSecurityStamp}, PhoneNumber: {PhoneNumber}",
                user.Id, user.Email, user.NormalizedEmail, user.NormalizedUserName, 
                !string.IsNullOrEmpty(user.PasswordHash), !string.IsNullOrEmpty(user.SecurityStamp),
                user.PhoneNumber);

            // Add user to default role
            await _userManager.AddToRoleAsync(user, "CoOwner");

            // Publish user registered event
            // Profile data comes from registration request, not from Auth DB (which doesn't store it)
            var userRegisteredEvent = new UserRegisteredEvent
            {
                UserId = user.Id,
                Email = user.Email!,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Phone = request.Phone, // Phone number from registration request
                Role = Domain.Entities.UserRole.CoOwner // Default role, User service will store it
            };
            
            _logger.LogInformation("Publishing UserRegisteredEvent - UserId: {UserId}, Email: {Email}, FirstName: {FirstName}, LastName: {LastName}, Phone: {Phone}", 
                userRegisteredEvent.UserId, userRegisteredEvent.Email, userRegisteredEvent.FirstName, userRegisteredEvent.LastName, userRegisteredEvent.Phone);
            
            try
            {
                await _publishEndpoint.Publish(userRegisteredEvent);
                _logger.LogInformation("UserRegisteredEvent published successfully for user {Email}", user.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish UserRegisteredEvent for user {Email}. Event will not be processed by User service.", user.Email);
                // Don't fail registration if event publishing fails - user is already created in Auth DB
            }

            _logger.LogInformation("User {Email} registered successfully", user.Email);

            // Send email confirmation
            try
            {
                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                var confirmationLink = GenerateConfirmationLink(user.Id, token);
                var emailSent = await _emailService.SendEmailConfirmationAsync(user.Email, confirmationLink);
                
                if (emailSent)
                {
                    _logger.LogInformation("Email confirmation sent to {Email}", user.Email);
                }
                else
                {
                    _logger.LogWarning("Failed to send email confirmation to {Email}", user.Email);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email confirmation to {Email}", user.Email);
            }

            return Ok(new
            {
                message = "User registered successfully. Please check your email to confirm your account.",
                email = user.Email,
                emailConfirmationRequired = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering user {Email}", request.Email);
            return StatusCode(500, new { message = "An error occurred during registration" });
        }
    }

    /// <summary>
    /// Login with email and password
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
            {
                return Unauthorized(new { message = "Invalid email or password" });
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, false);
            if (!result.Succeeded)
            {
                _logger.LogWarning("Failed login attempt for user {Email}", request.Email);
                return Unauthorized(new { message = "Invalid email or password" });
            }

            if (!user.EmailConfirmed)
            {
                return BadRequest(new { message = "Email not confirmed. Please check your email." });
            }

            LoginResponseDto loginResponse;
            try
            {
                loginResponse = await _tokenService.GenerateTokenAsync(user);
            }
            catch (Exception tokenEx)
            {
                _logger.LogError(tokenEx, "Error generating token for user {Email}. User details - Id: {UserId}",
                    request.Email, user.Id);
                throw;
            }

            _logger.LogInformation("User {Email} logged in successfully", user.Email);

            return Ok(loginResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for user {Email}", request.Email);
            return StatusCode(500, new { message = "An error occurred during login" });
        }
    }

    /// <summary>
    /// Refresh access token using refresh token
    /// </summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenDto request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var loginResponse = await _tokenService.RefreshTokenAsync(request.RefreshToken);
            return Ok(loginResponse);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Invalid refresh token attempt: {Message}", ex.Message);
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing token");
            return StatusCode(500, new { message = "An error occurred while refreshing token" });
        }
    }

    /// <summary>
    /// Logout and revoke refresh token
    /// </summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenDto request)
    {
        try
        {
            await _tokenService.RevokeTokenAsync(request.RefreshToken);
            return Ok(new { message = "Logged out successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return StatusCode(500, new { message = "An error occurred during logout" });
        }
    }

    /// <summary>
    /// Validate token
    /// </summary>
    [HttpPost("validate")]
    public async Task<IActionResult> ValidateToken([FromBody] string token)
    {
        try
        {
            var isValid = await _tokenService.ValidateTokenAsync(token);
            return Ok(new { isValid });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating token");
            return StatusCode(500, new { message = "An error occurred while validating token" });
        }
    }

    /// <summary>
    /// Get current user info from token
    /// </summary>
    [HttpGet("me")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> GetCurrentUser()
    {
        try
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            // Profile fields are NOT stored in Auth DB - return minimal info
            // Full profile should be fetched from User service
            var userDto = new UserDto
            {
                Id = user.Id,
                Email = user.Email!,
                FirstName = string.Empty, // Not stored in Auth DB
                LastName = string.Empty, // Not stored in Auth DB
                Phone = null, // Not stored in Auth DB
                KycStatus = Domain.Entities.KycStatus.Pending, // Not stored in Auth DB
                Role = Domain.Entities.UserRole.CoOwner, // Not stored in Auth DB
                CreatedAt = DateTime.UtcNow // Not stored in Auth DB
            };

            return Ok(userDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current user");
            return StatusCode(500, new { message = "An error occurred while getting user information" });
        }
    }

    /// <summary>
    /// Confirm email address
    /// </summary>
    [HttpPost("confirm-email")]
    public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.UserId))
            {
                _logger.LogWarning("Email confirmation request received with empty UserId");
                return BadRequest(new { message = "UserId is required" });
            }

            if (string.IsNullOrEmpty(request.Token))
            {
                _logger.LogWarning("Email confirmation request received with empty Token");
                return BadRequest(new { message = "Token is required" });
            }

            _logger.LogInformation("Email confirmation request received - UserId: {UserId}, Token length: {TokenLength}", 
                request.UserId, request.Token?.Length ?? 0);

            // Try to parse UserId as Guid
            if (!Guid.TryParse(request.UserId, out var userIdGuid))
            {
                _logger.LogWarning("Invalid UserId format for email confirmation - UserId: {UserId}", request.UserId);
                return BadRequest(new { message = "Invalid user ID format" });
            }

            _logger.LogInformation("Parsed UserId as Guid: {UserIdGuid}", userIdGuid);

            // Try FindByIdAsync with Guid string
            var user = await _userManager.FindByIdAsync(userIdGuid.ToString());
            if (user == null)
            {
                // Also try FindByEmailAsync as fallback (in case UserId format is wrong)
                _logger.LogWarning("User not found by Id, checking if user exists in database...");
                
                // Log all users with similar email for debugging
                var allUsers = _userManager.Users.ToList();
                _logger.LogWarning("Total users in database: {Count}", allUsers.Count);
                foreach (var u in allUsers.Take(10))
                {
                    _logger.LogWarning("User in DB - Id: {Id}, Email: {Email}", u.Id, u.Email);
                }
                
                _logger.LogWarning("User not found for email confirmation - UserId: {UserId}, ParsedGuid: {UserIdGuid}", 
                    request.UserId, userIdGuid);
                return BadRequest(new { message = "Invalid user" });
            }

            _logger.LogInformation("User found - Email: {Email}, EmailConfirmed before: {EmailConfirmed}", 
                user.Email, user.EmailConfirmed);

            // Decode the base64 encoded token
            // Token may be URL-encoded in the query string, so decode it first
            string decodedToken;
            try
            {
                // First, URL-decode the token (in case it was URL-encoded in the query string)
                var urlDecodedToken = Uri.UnescapeDataString(request.Token);
                _logger.LogInformation("Token URL-decoded. Original length: {OriginalLength}, URL-decoded length: {UrlDecodedLength}", 
                    request.Token.Length, urlDecodedToken.Length);
                
                // Then, base64 decode the token
                decodedToken = Encoding.UTF8.GetString(Convert.FromBase64String(urlDecodedToken));
                _logger.LogInformation("Token decoded successfully from base64. Decoded length: {DecodedLength}", decodedToken.Length);
            }
            catch (FormatException ex)
            {
                // If base64 decoding fails, try URL-decoding first, then base64 decode again
                try
                {
                    var urlDecodedToken = Uri.UnescapeDataString(request.Token);
                    decodedToken = Encoding.UTF8.GetString(Convert.FromBase64String(urlDecodedToken));
                    _logger.LogInformation("Token decoded successfully after URL decode");
                }
                catch
                {
                    // If both fail, try using the token as-is (might be already decoded)
                    decodedToken = request.Token;
                    _logger.LogWarning("Token base64 decode failed, using token as-is. Error: {Error}", ex.Message);
                }
            }

            _logger.LogInformation("Attempting to confirm email with decoded token. Token length: {TokenLength}, UserId: {UserId}, Email: {Email}", 
                decodedToken.Length, user.Id, user.Email);
            
            var result = await _userManager.ConfirmEmailAsync(user, decodedToken);
            
            if (result.Succeeded)
            {
                // Reload user to verify EmailConfirmed was updated
                user = await _userManager.FindByIdAsync(user.Id.ToString());
                if (user == null)
                {
                    _logger.LogError("User disappeared after email confirmation - UserId: {UserId}", request.UserId);
                    return StatusCode(500, new { message = "Email confirmed but user could not be verified" });
                }

                _logger.LogInformation("Email confirmation succeeded - Email: {Email}, EmailConfirmed after: {EmailConfirmed}", 
                    user.Email, user.EmailConfirmed);

                if (!user.EmailConfirmed)
                {
                    _logger.LogError("EmailConfirmed is still false after ConfirmEmailAsync succeeded! UserId: {UserId}", user.Id);
                    return StatusCode(500, new { message = "Email confirmation failed - please contact support" });
                }

                // Send welcome email
                try
                {
                    // Profile data not available in Auth DB - use email only or fetch from User service
                    await _emailService.SendWelcomeEmailAsync(user.Email!, "User");
                }
                catch (Exception emailEx)
                {
                    _logger.LogWarning(emailEx, "Failed to send welcome email to {Email}", user.Email);
                    // Don't fail the request if welcome email fails
                }
                
                _logger.LogInformation("Email confirmed successfully for user {Email}", user.Email);
                return Ok(new { message = "Email confirmed successfully", emailConfirmed = user.EmailConfirmed });
            }
            else
            {
                var errors = string.Join(", ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
                _logger.LogWarning("Email confirmation failed - UserId: {UserId}, Email: {Email}, Token length: {TokenLength}, Errors: {Errors}", 
                    request.UserId, user.Email, decodedToken.Length, errors);
                
                // Log each error separately for better debugging
                foreach (var error in result.Errors)
                {
                    _logger.LogWarning("Email confirmation error - Code: {Code}, Description: {Description}", 
                        error.Code, error.Description);
                }
                
                return BadRequest(new { message = "Invalid confirmation token", errors = result.Errors.Select(e => e.Description) });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming email for user {UserId}", request.UserId);
            return StatusCode(500, new { message = "An error occurred during email confirmation" });
        }
    }

    /// <summary>
    /// Debug endpoint to test user and token generation
    /// </summary>
    [HttpPost("debug-login")]
    public async Task<IActionResult> DebugLogin([FromBody] LoginDto request)
    {
        try
        {
            _logger.LogInformation("DEBUG: Starting login for {Email}", request.Email);

            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
            {
                _logger.LogWarning("DEBUG: User not found");
                return NotFound(new { message = "User not found", email = request.Email });
            }

            _logger.LogInformation("DEBUG: User found - Id: {Id}, EmailConfirmed: {EmailConfirmed}",
                user.Id, user.EmailConfirmed);

            var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, false);
            _logger.LogInformation("DEBUG: Password check - Succeeded: {Succeeded}", result.Succeeded);

            if (!result.Succeeded)
            {
                return Unauthorized(new { message = "Invalid password" });
            }

            _logger.LogInformation("DEBUG: About to generate token");

            try
            {
                var loginResponse = await _tokenService.GenerateTokenAsync(user);
                _logger.LogInformation("DEBUG: Token generated successfully");
                return Ok(new { message = "Success", data = loginResponse });
            }
            catch (Exception tokenEx)
            {
                _logger.LogError(tokenEx, "DEBUG: Token generation failed - Message: {Message}, StackTrace: {StackTrace}",
                    tokenEx.Message, tokenEx.StackTrace);
                return StatusCode(500, new {
                    message = "Token generation failed",
                    error = tokenEx.Message,
                    innerError = tokenEx.InnerException?.Message,
                    type = tokenEx.GetType().Name
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DEBUG: General error - Message: {Message}, StackTrace: {StackTrace}",
                ex.Message, ex.StackTrace);
            return StatusCode(500, new {
                message = "An error occurred",
                error = ex.Message,
                innerError = ex.InnerException?.Message,
                type = ex.GetType().Name
            });
        }
    }

    /// <summary>
    /// Get user data by ID (for other services)
    /// </summary>
    [HttpGet("user/{userId:guid}")]
    public async Task<IActionResult> GetUser(Guid userId)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            // Profile fields are NOT stored in Auth DB - return minimal info
            var userData = new
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = string.Empty, // Not stored in Auth DB
                LastName = string.Empty, // Not stored in Auth DB
                Phone = (string?)null, // Not stored in Auth DB
                Role = (int)Domain.Entities.UserRole.CoOwner, // Not stored in Auth DB
                KycStatus = (int)Domain.Entities.KycStatus.Pending, // Not stored in Auth DB
                CreatedAt = DateTime.UtcNow // Not stored in Auth DB
            };

            return Ok(userData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user data for {UserId}", userId);
            return StatusCode(500, new { message = "An error occurred while retrieving user data" });
        }
    }

    /// <summary>
    /// Resend email confirmation
    /// </summary>
    [HttpPost("resend-confirmation")]
    public async Task<IActionResult> ResendConfirmation([FromBody] ResendConfirmationRequest request)
    {
        try
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
            {
                return BadRequest(new { message = "User not found" });
            }

            if (user.EmailConfirmed)
            {
                return BadRequest(new { message = "Email already confirmed" });
            }

            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var confirmationLink = GenerateConfirmationLink(user.Id, token);
            var emailSent = await _emailService.SendEmailConfirmationAsync(user.Email!, confirmationLink);

            if (emailSent)
            {
                _logger.LogInformation("Confirmation email resent to {Email}", user.Email);
                return Ok(new { message = "Confirmation email sent" });
            }

            return StatusCode(500, new { message = "Failed to send confirmation email" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resending confirmation email to {Email}", request.Email);
            return StatusCode(500, new { message = "An error occurred while resending confirmation email" });
        }
    }

    /// <summary>
    /// Correct email address during verification process
    /// Allows users to fix their email if they registered with an incorrect address
    /// Only works for unconfirmed email accounts
    /// </summary>
    [HttpPost("correct-email")]
    public async Task<IActionResult> CorrectEmail([FromBody] CorrectEmailRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Validate that current and new emails are different
            if (request.CurrentEmail.Equals(request.NewEmail, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "New email must be different from current email" });
            }

            // Find user by current email
            var user = await _userManager.FindByEmailAsync(request.CurrentEmail);
            if (user == null)
            {
                _logger.LogWarning("Email correction attempt for non-existent user: {Email}", request.CurrentEmail);
                return BadRequest(new { message = "User not found with the provided email address" });
            }

            // Check if email is already confirmed
            if (user.EmailConfirmed)
            {
                _logger.LogWarning("Email correction attempt for already confirmed email: {Email}", request.CurrentEmail);
                return BadRequest(new { message = "Cannot change email for already verified accounts. Please use the account settings to update your email." });
            }

            // Check if new email is already in use
            var existingUserWithNewEmail = await _userManager.FindByEmailAsync(request.NewEmail);
            if (existingUserWithNewEmail != null)
            {
                _logger.LogWarning("Email correction attempt with email that already exists: {NewEmail}", request.NewEmail);
                return BadRequest(new { message = "An account with this email already exists" });
            }

            _logger.LogInformation("Correcting email for user {UserId} from {OldEmail} to {NewEmail}",
                user.Id, request.CurrentEmail, request.NewEmail);

            // Update email and username
            user.Email = request.NewEmail;
            user.UserName = request.NewEmail;
            user.NormalizedEmail = request.NewEmail.ToUpperInvariant();
            user.NormalizedUserName = request.NewEmail.ToUpperInvariant();

            // Update user in database
            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                _logger.LogError("Failed to update user email. Errors: {Errors}",
                    string.Join(", ", updateResult.Errors.Select(e => e.Description)));
                return StatusCode(500, new { message = "Failed to update email address", errors = updateResult.Errors });
            }

            // Generate new confirmation token for the new email
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var confirmationLink = GenerateConfirmationLink(user.Id, token);

            // Send confirmation email to the new email address
            var emailSent = await _emailService.SendEmailConfirmationAsync(request.NewEmail, confirmationLink);

            if (emailSent)
            {
                _logger.LogInformation("Email successfully corrected and confirmation email sent to {NewEmail}", request.NewEmail);
                return Ok(new
                {
                    message = "Email address updated successfully. A new confirmation email has been sent to your new email address.",
                    newEmail = request.NewEmail
                });
            }

            // If email sending failed, we still updated the email in database
            _logger.LogWarning("Email was updated but confirmation email failed to send to {NewEmail}", request.NewEmail);
            return Ok(new
            {
                message = "Email address updated but failed to send confirmation email. Please use the resend confirmation endpoint.",
                newEmail = request.NewEmail
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error correcting email from {CurrentEmail} to {NewEmail}",
                request.CurrentEmail, request.NewEmail);
            return StatusCode(500, new { message = "An error occurred while correcting email address" });
        }
    }

    /// <summary>
    /// Change user password (requires authentication)
    /// </summary>
    [HttpPost("change-password")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description);
                return BadRequest(new { message = "Failed to change password", errors });
            }

            _logger.LogInformation("Password changed successfully for user {Email}", user.Email);

            return Ok(new { message = "Password changed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password");
            return StatusCode(500, new { message = "An error occurred while changing password" });
        }
    }

    /// <summary>
    /// Request password reset (forgot password)
    /// </summary>
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
            {
                // Don't reveal if user exists or not for security
                return Ok(new { message = "If the email exists, a password reset link has been sent." });
            }

            // Generate password reset token
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetLink = GeneratePasswordResetLink(user.Id, token);

            // Send password reset email
            var emailSent = await _emailService.SendPasswordResetAsync(user.Email!, resetLink);
            
            if (emailSent)
            {
                _logger.LogInformation("Password reset email sent to {Email}", user.Email);
            }
            else
            {
                _logger.LogWarning("Failed to send password reset email to {Email}", user.Email);
            }

            // Always return success message for security (don't reveal if email exists)
            return Ok(new { message = "If the email exists, a password reset link has been sent." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing forgot password request for {Email}", request.Email);
            return StatusCode(500, new { message = "An error occurred while processing your request" });
        }
    }

    /// <summary>
    /// Reset password using token from email
    /// </summary>
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user == null)
            {
                return BadRequest(new { message = "Invalid reset token" });
            }

            // Decode the base64 encoded token
            string decodedToken;
            try
            {
                decodedToken = Encoding.UTF8.GetString(Convert.FromBase64String(request.Token));
            }
            catch (FormatException)
            {
                // If base64 decoding fails, try using the token as-is
                decodedToken = request.Token;
            }

            var result = await _userManager.ResetPasswordAsync(user, decodedToken, request.NewPassword);
            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description);
                return BadRequest(new { message = "Failed to reset password", errors });
            }

            _logger.LogInformation("Password reset successfully for user {Email}", user.Email);

            return Ok(new { message = "Password reset successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password");
            return StatusCode(500, new { message = "An error occurred while resetting password" });
        }
    }

    private string GenerateConfirmationLink(Guid userId, string token)
    {
        var frontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL") ?? "http://localhost:5173";
        // Base64 encode the token
        var base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(token));
        // URL-encode the base64 token to handle special characters (+ / =) in URLs
        var urlEncodedToken = Uri.EscapeDataString(base64Token);
        return $"{frontendUrl}/confirm-email?userId={userId}&token={urlEncodedToken}";
    }

    private string GeneratePasswordResetLink(Guid userId, string token)
    {
        var frontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL") ?? "http://localhost:5173";
        // Base64 encode the token
        var base64Token = Convert.ToBase64String(Encoding.UTF8.GetBytes(token));
        // URL-encode the base64 token to handle special characters (+ / =) in URLs
        var urlEncodedToken = Uri.EscapeDataString(base64Token);
        return $"{frontendUrl}/reset-password?userId={userId}&token={urlEncodedToken}";
    }
}

public class ConfirmEmailRequest
{
    public string UserId { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
}

public class ResendConfirmationRequest
{
    public string Email { get; set; } = string.Empty;
}

public class CorrectEmailRequest
{
    public string CurrentEmail { get; set; } = string.Empty;
    public string NewEmail { get; set; } = string.Empty;
}
