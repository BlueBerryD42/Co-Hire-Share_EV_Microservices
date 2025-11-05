using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoOwnershipVehicle.Group.Api.Controllers;

/// <summary>
/// Base controller for authenticated endpoints that extracts user ID from JWT token
/// </summary>
[Authorize]
public abstract class BaseAuthenticatedController : ControllerBase
{
    protected readonly ILogger Logger;

    protected BaseAuthenticatedController(ILogger logger)
    {
        Logger = logger;
    }

    /// <summary>
    /// Extracts the authenticated user ID from JWT token claims
    /// </summary>
    /// <returns>User ID from JWT token</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when user ID cannot be extracted</exception>
    protected Guid GetUserId()
    {
        // Log all claims for debugging
        Logger.LogInformation("All JWT Claims:");
        foreach (var claim in User.Claims)
        {
            Logger.LogInformation("  Claim Type: {Type}, Value: {Value}", claim.Type, claim.Value);
        }

        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        Logger.LogInformation("NameIdentifier claim value: {Value}", userIdClaim ?? "NULL");

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            Logger.LogError("Unable to extract valid user ID from JWT token claims. UserIdClaim: {UserIdClaim}", userIdClaim);
            throw new UnauthorizedAccessException("Invalid or missing user authentication");
        }

        Logger.LogInformation("Successfully extracted user ID {UserId} from JWT token", userId);
        return userId;
    }

    /// <summary>
    /// Gets the user's email from JWT token claims
    /// </summary>
    protected string? GetUserEmail()
    {
        return User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
    }

    /// <summary>
    /// Gets the user's role from JWT token claims
    /// </summary>
    protected string? GetUserRole()
    {
        return User.FindFirst("role")?.Value;
    }

    /// <summary>
    /// Checks if the current user has a specific role
    /// </summary>
    protected bool HasRole(string role)
    {
        var userRole = GetUserRole();
        return !string.IsNullOrEmpty(userRole) && userRole.Equals(role, StringComparison.OrdinalIgnoreCase);
    }
}
