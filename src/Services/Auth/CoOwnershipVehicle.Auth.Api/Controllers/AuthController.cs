using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Auth.Api.Services;
using CoOwnershipVehicle.Shared.Contracts.Events;
using MassTransit;
using System.Text;

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

            // Create new user
            var user = new User
            {
                Id = Guid.NewGuid(),
                UserName = request.Email,
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Phone = request.Phone,
                EmailConfirmed = false, // Will be confirmed via email verification
                Role = Domain.Entities.UserRole.CoOwner, // Default role
                KycStatus = Domain.Entities.KycStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
            {
                return BadRequest(new { message = "Failed to create user", errors = result.Errors });
            }

            // Add user to default role
            await _userManager.AddToRoleAsync(user, "CoOwner");

            // Publish user registered event
            await _publishEndpoint.Publish(new UserRegisteredEvent
            {
                UserId = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Role = user.Role
            });

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
                _logger.LogError(tokenEx, "Error generating token for user {Email}. User details - Id: {UserId}, FirstName: {FirstName}, LastName: {LastName}",
                    request.Email, user.Id, user.FirstName ?? "null", user.LastName ?? "null");
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

            var userDto = new UserDto
            {
                Id = user.Id,
                Email = user.Email!,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Phone = user.Phone,
                KycStatus = user.KycStatus,
                Role = user.Role,
                CreatedAt = user.CreatedAt
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
            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user == null)
            {
                return BadRequest(new { message = "Invalid user" });
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

            var result = await _userManager.ConfirmEmailAsync(user, decodedToken);
            if (result.Succeeded)
            {
                // Send welcome email
                await _emailService.SendWelcomeEmailAsync(user.Email!, user.FirstName!);
                
                _logger.LogInformation("Email confirmed for user {Email}", user.Email);
                return Ok(new { message = "Email confirmed successfully" });
            }

            return BadRequest(new { message = "Invalid confirmation token" });
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

            _logger.LogInformation("DEBUG: User found - Id: {Id}, FirstName: {FirstName}, LastName: {LastName}, EmailConfirmed: {EmailConfirmed}",
                user.Id, user.FirstName ?? "NULL", user.LastName ?? "NULL", user.EmailConfirmed);

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

            var userData = new
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Phone = user.Phone,
                Role = (int)user.Role,
                KycStatus = (int)user.KycStatus,
                CreatedAt = user.CreatedAt
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

    //private string GenerateConfirmationLink(Guid userId, string token)
    //{
    //    var frontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL") ?? "https://localhost:3000";
    //    var encodedToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(token));
    //    return $"{frontendUrl}/confirm-email?userId={userId}&token={encodedToken}";
    //}

    //private string GeneratePasswordResetLink(Guid userId, string token)
    //{
    //    var frontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL") ?? "https://localhost:3000";
    //    var encodedToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(token));
    //    return $"{frontendUrl}/reset-password?userId={userId}&token={encodedToken}";
    //}

    private string GenerateConfirmationLink(Guid userId, string token)
    {
        // Hard-code tạm thời để test
        var frontendUrl = "http://localhost:5173";

        var encodedToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(token));
        var link = $"{frontendUrl}/confirm-email?userId={userId}&token={encodedToken}";

        _logger.LogInformation("📧 Generated confirmation link: {Link}", link);

        return link;
    }

    private string GeneratePasswordResetLink(Guid userId, string token)
    {
        // Hard-code tạm thời để test
        var frontendUrl = "http://localhost:5173";

        var encodedToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(token));
        return $"{frontendUrl}/reset-password?userId={userId}&token={encodedToken}";
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
