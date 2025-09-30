using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.User.Api.Services;

namespace CoOwnershipVehicle.User.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IUserSyncService _userSyncService;
    private readonly ILogger<UserController> _logger;

    public UserController(IUserService userService, IUserSyncService userSyncService, ILogger<UserController> logger)
    {
        _userService = userService;
        _userSyncService = userSyncService;
        _logger = logger;
    }

    /// <summary>
    /// Get current user profile
    /// </summary>
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        try
        {
            var userId = GetCurrentUserId();
            
            // First, ensure user is synced from Auth service
            var syncedUser = await _userSyncService.SyncUserAsync(userId);
            
            if (syncedUser == null)
            {
                _logger.LogWarning("User sync returned null for user {UserId}", userId);
                return NotFound(new { message = "User profile not found" });
            }
            
            // Small delay to ensure database transaction is committed
            await Task.Delay(100);
            
            // Then get the full profile with KYC documents
            var profile = await _userService.GetUserProfileAsync(userId);
            
            if (profile == null)
            {
                _logger.LogWarning("User profile not found after sync for user {UserId}", userId);
                return NotFound(new { message = "User profile not found" });
            }

            return Ok(profile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user profile");
            return StatusCode(500, new { message = "An error occurred while retrieving profile" });
        }
    }

    /// <summary>
    /// Get user profile by ID (admin/staff only)
    /// </summary>
    [HttpGet("profile/{userId:guid}")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    public async Task<IActionResult> GetProfileById(Guid userId)
    {
        try
        {
            var profile = await _userService.GetUserProfileAsync(userId);
            
            if (profile == null)
                return NotFound(new { message = "User profile not found" });

            return Ok(profile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user profile {UserId}", userId);
            return StatusCode(500, new { message = "An error occurred while retrieving profile" });
        }
    }

    /// <summary>
    /// Update current user profile
    /// </summary>
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateUserProfileDto updateDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetCurrentUserId();
            var updatedProfile = await _userService.UpdateUserProfileAsync(userId, updateDto);

            return Ok(updatedProfile);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user profile");
            return StatusCode(500, new { message = "An error occurred while updating profile" });
        }
    }

    // NOTE: Password change endpoint removed - this should be handled by Auth service only
    // Password changes must be done through the Auth service API for security reasons

    /// <summary>
    /// Upload KYC document
    /// </summary>
    [HttpPost("kyc/upload")]
    public async Task<IActionResult> UploadKycDocument([FromBody] UploadKycDocumentDto uploadDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetCurrentUserId();
            var document = await _userService.UploadKycDocumentAsync(userId, uploadDto);

            return Ok(document);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading KYC document");
            return StatusCode(500, new { message = "An error occurred while uploading document" });
        }
    }

    /// <summary>
    /// Get current user's KYC documents
    /// </summary>
    [HttpGet("kyc/documents")]
    public async Task<IActionResult> GetKycDocuments()
    {
        try
        {
            var userId = GetCurrentUserId();
            var documents = await _userService.GetUserKycDocumentsAsync(userId);

            return Ok(documents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting KYC documents");
            return StatusCode(500, new { message = "An error occurred while retrieving documents" });
        }
    }

    /// <summary>
    /// Get user's KYC documents by user ID (admin/staff only)
    /// </summary>
    [HttpGet("kyc/documents/{userId:guid}")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    public async Task<IActionResult> GetKycDocumentsByUserId(Guid userId)
    {
        try
        {
            var documents = await _userService.GetUserKycDocumentsAsync(userId);
            return Ok(documents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting KYC documents for user {UserId}", userId);
            return StatusCode(500, new { message = "An error occurred while retrieving documents" });
        }
    }

    /// <summary>
    /// Get pending KYC documents for review (admin/staff only)
    /// </summary>
    [HttpGet("kyc/pending")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    public async Task<IActionResult> GetPendingKycDocuments()
    {
        try
        {
            var documents = await _userService.GetPendingKycDocumentsAsync();
            return Ok(documents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending KYC documents");
            return StatusCode(500, new { message = "An error occurred while retrieving pending documents" });
        }
    }

    /// <summary>
    /// Review KYC document (admin/staff only)
    /// </summary>
    [HttpPost("kyc/review/{documentId:guid}")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    public async Task<IActionResult> ReviewKycDocument(Guid documentId, [FromBody] ReviewKycDocumentDto reviewDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var reviewerId = GetCurrentUserId();
            var reviewedDocument = await _userService.ReviewKycDocumentAsync(documentId, reviewDto, reviewerId);

            return Ok(reviewedDocument);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reviewing KYC document {DocumentId}", documentId);
            return StatusCode(500, new { message = "An error occurred while reviewing document" });
        }
    }

    /// <summary>
    /// Update user KYC status (admin only)
    /// </summary>
    [HttpPost("kyc/status/{userId:guid}")]
    [Authorize(Roles = "SystemAdmin")]
    public async Task<IActionResult> UpdateKycStatus(Guid userId, [FromBody] UpdateKycStatusDto statusDto)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var success = await _userService.UpdateKycStatusAsync(userId, statusDto.Status, statusDto.Reason);

            if (!success)
                return NotFound(new { message = "User not found" });

            return Ok(new { message = "KYC status updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating KYC status for user {UserId}", userId);
            return StatusCode(500, new { message = "An error occurred while updating KYC status" });
        }
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid user ID in token");
        }
        return userId;
    }
}

public class UpdateKycStatusDto
{
    public KycStatus Status { get; set; }
    public string? Reason { get; set; }
}
