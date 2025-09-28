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

    public AuthController(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        IJwtTokenService tokenService,
        IPublishEndpoint publishEndpoint,
        IEmailService emailService,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _publishEndpoint = publishEndpoint;
        _emailService = emailService;
        _logger = logger;
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
                Role = (Shared.Contracts.DTOs.UserRole)user.Role
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

            var loginResponse = await _tokenService.GenerateTokenAsync(user);

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
                KycStatus = (Shared.Contracts.DTOs.KycStatus)user.KycStatus,
                Role = (Shared.Contracts.DTOs.UserRole)user.Role,
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

    private string GenerateConfirmationLink(Guid userId, string token)
    {
        var frontendUrl = Environment.GetEnvironmentVariable("FRONTEND_URL") ?? "https://localhost:3000";
        var encodedToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(token));
        return $"{frontendUrl}/confirm-email?userId={userId}&token={encodedToken}";
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
