using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.User.Api.Services;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace CoOwnershipVehicle.User.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IUserSyncService _userSyncService;
    private readonly ILogger<UserController> _logger;
    private readonly IWebHostEnvironment _environment;

    public UserController(
        IUserService userService, 
        IUserSyncService userSyncService, 
        ILogger<UserController> logger,
        IWebHostEnvironment environment)
    {
        _userService = userService;
        _userSyncService = userSyncService;
        _logger = logger;
        _environment = environment;
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
            
            // Sync user from Auth service (HTTP pattern, consistent with other services)
            var syncedUser = await _userSyncService.SyncUserAsync(userId);
            
            if (syncedUser == null)
            {
                _logger.LogWarning("User sync returned null for user {UserId}", userId);
                return NotFound(new { message = "User profile not found" });
            }
            
            // Get the full profile with KYC documents
            var profile = await _userService.GetUserProfileAsync(userId);
            
            if (profile == null)
            {
                _logger.LogWarning("User profile not found for user {UserId}", userId);
                return NotFound(new { message = "User profile not found" });
            }

            return Ok(profile);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access attempt to get profile");
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user profile: {Message}", ex.Message);
            return StatusCode(500, new { message = "An error occurred while retrieving profile", details = ex.Message });
        }
    }

    /// <summary>
    /// Internal endpoint: Get user role for JWT token generation (Auth service only)
    /// This endpoint is called by Auth service during token generation to get the user's role.
    /// Uses service key authentication for internal service-to-service communication.
    /// </summary>
    [HttpGet("internal/role/{userId:guid}")]
    [AllowAnonymous]  // Bypass JWT auth - use service key instead
    public async Task<IActionResult> GetUserRoleForToken(Guid userId, [FromHeader(Name = "X-Service-Key")] string? serviceKey)
    {
        try
        {
            // Verify service key for internal service calls
            var expectedServiceKey = HttpContext.RequestServices.GetRequiredService<IConfiguration>()["ServiceKeys:Internal"] 
                ?? Environment.GetEnvironmentVariable("SERVICE_KEY_INTERNAL");
            
            if (string.IsNullOrEmpty(expectedServiceKey) || serviceKey != expectedServiceKey)
            {
                _logger.LogWarning("Unauthorized internal service call attempt for user role lookup");
                return Unauthorized(new { message = "Invalid service key" });
            }

            var profile = await _userService.GetUserProfileAsync(userId);
            
            if (profile == null)
                return NotFound(new { message = "User not found" });

            // Return only role information needed for JWT token
            return Ok(new
            {
                UserId = profile.Id,
                Role = profile.Role.ToString(),
                KycStatus = profile.KycStatus.ToString(),
                FirstName = profile.FirstName,
                LastName = profile.LastName
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user role for token generation {UserId}", userId);
            return StatusCode(500, new { message = "An error occurred while retrieving user role" });
        }
    }

    /// <summary>
    /// Search user by email to get user ID (for adding members to groups)
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> SearchUserByEmail([FromQuery] string email)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest(new { message = "Email is required" });

            // Search in UserProfiles first
            var userProfile = await _userService.GetUserByEmailAsync(email);
            
            if (userProfile == null)
                return NotFound(new { message = "User not found" });

            return Ok(new
            {
                Id = userProfile.Id,
                Email = userProfile.Email,
                FirstName = userProfile.FirstName,
                LastName = userProfile.LastName
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching user by email {Email}", email);
            return StatusCode(500, new { message = "An error occurred while searching for user" });
        }
    }

    /// <summary>
    /// Get basic user information by ID (any authenticated user)
    /// This endpoint allows users to get basic info (name, email) for other users,
    /// typically used for displaying member names in groups, proposals, etc.
    /// </summary>
    [HttpGet("basic/{userId:guid}")]
    public async Task<IActionResult> GetBasicUserInfo(Guid userId)
    {
        try
        {
            var profile = await _userService.GetUserProfileAsync(userId);
            
            if (profile == null)
                return NotFound(new { message = "User not found" });

            // Return only basic information (no sensitive data like KYC documents)
            var basicInfo = new
            {
                Id = profile.Id,
                Email = profile.Email,
                FirstName = profile.FirstName,
                LastName = profile.LastName,
                Phone = profile.Phone
            };

            return Ok(basicInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting basic user info {UserId}", userId);
            return StatusCode(500, new { message = "An error occurred while retrieving user information" });
        }
    }

    /// <summary>
    /// Get paginated list of users (admin/staff only)
    /// </summary>
    [HttpGet("users")]
    [Authorize(Roles = "SystemAdmin,Staff")]
    public async Task<IActionResult> GetUsers([FromQuery] UserListRequestDto request)
    {
        try
        {
            var result = await _userService.GetUsersAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users list");
            return StatusCode(500, new { message = "An error occurred while retrieving users" });
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
    /// Get KYC document by ID
    /// </summary>
    [HttpGet("kyc/document/{documentId:guid}")]
    [Authorize]
    public async Task<IActionResult> GetKycDocument(Guid documentId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var documents = await _userService.GetUserKycDocumentsAsync(userId);
            var document = documents.FirstOrDefault(d => d.Id == documentId);
            
            // Also allow admin/staff to get any document
            if (document == null && (User.IsInRole("SystemAdmin") || User.IsInRole("Staff")))
            {
                document = await _userService.GetKycDocumentByIdAsync(documentId);
            }
            
            if (document == null)
                return NotFound(new { message = "KYC document not found" });
            
            return Ok(document);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting KYC document {DocumentId}", documentId);
            return StatusCode(500, new { message = "An error occurred while retrieving document" });
        }
    }

    /// <summary>
    /// Download KYC document file
    /// </summary>
    [HttpGet("kyc/documents/{documentId:guid}/download")]
    [Authorize]
    public async Task<IActionResult> DownloadKycDocument(Guid documentId)
    {
        try
        {
            KycDocumentDto? document = null;
            var userId = GetCurrentUserId();
            
            // Try to get document - first check if user owns it
            var userDocuments = await _userService.GetUserKycDocumentsAsync(userId);
            document = userDocuments.FirstOrDefault(d => d.Id == documentId);
            
            // If not found and user is admin/staff, get by ID directly
            if (document == null && (User.IsInRole("SystemAdmin") || User.IsInRole("Staff")))
            {
                document = await _userService.GetKycDocumentByIdAsync(documentId);
            }
            
            if (document == null)
            {
                _logger.LogWarning("KYC document not found. DocumentId: {DocumentId}, UserId: {UserId}", documentId, userId);
                return NotFound(new { message = "KYC document not found" });
            }
            
            // Extract file name from storage URL
            // Support both formats:
            // 1. New format: /api/User/kyc/files/{fileId}.{ext}
            // 2. Old format: https://storage.coownership.com/kyc/{fileId}/{fileName}
            var storageUrl = document.StorageUrl;
            string? fileName = null;
            
            if (string.IsNullOrEmpty(storageUrl))
            {
                _logger.LogWarning("Storage URL is empty for document {DocumentId}", documentId);
                return NotFound(new { message = "Document file not found - storage URL is empty" });
            }
            
            // Try new format first: /api/User/kyc/files/{fileId}.{ext}
            if (storageUrl.Contains("/kyc/files/"))
            {
                fileName = storageUrl.Substring(storageUrl.LastIndexOf('/') + 1);
            }
            // Try old format: https://storage.coownership.com/kyc/{fileId}/{fileName}
            else if (storageUrl.Contains("storage.coownership.com/kyc/"))
            {
                var parts = storageUrl.Split('/');
                if (parts.Length > 0)
                {
                    fileName = parts[parts.Length - 1]; // Last part is the filename
                }
            }
            // Fallback: use document.FileName if available
            else if (!string.IsNullOrEmpty(document.FileName))
            {
                // Try to extract GUID from storageUrl and use it with extension from FileName
                var guidMatch = System.Text.RegularExpressions.Regex.Match(storageUrl, @"([a-f0-9-]{36})", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (guidMatch.Success)
                {
                    var fileExtension = Path.GetExtension(document.FileName);
                    fileName = $"{guidMatch.Value}{fileExtension}";
                }
                else
                {
                    fileName = document.FileName;
                }
            }
            
            if (string.IsNullOrEmpty(fileName))
            {
                _logger.LogWarning("Could not extract file name from storage URL: {StorageUrl}", storageUrl);
                return NotFound(new { message = "Document file not found - could not determine file name" });
            }
            
            var filePath = Path.Combine(_environment.ContentRootPath, "wwwroot", "files", "kyc", fileName);
            
            _logger.LogInformation("Attempting to download KYC document. DocumentId: {DocumentId}, StorageUrl: {StorageUrl}, FileName: {FileName}, FilePath: {FilePath}", 
                documentId, storageUrl, fileName, filePath);
            
            if (!System.IO.File.Exists(filePath))
            {
                _logger.LogWarning("KYC document file not found at path: {FilePath}. ContentRootPath: {ContentRootPath}. Listing directory contents...", 
                    filePath, _environment.ContentRootPath);
                
                // List files in directory for debugging
                var kycDir = Path.Combine(_environment.ContentRootPath, "wwwroot", "files", "kyc");
                if (Directory.Exists(kycDir))
                {
                    var files = Directory.GetFiles(kycDir);
                    _logger.LogInformation("Files in KYC directory ({KycDir}): {Files}", kycDir, string.Join(", ", files.Select(f => Path.GetFileName(f))));
                }
                else
                {
                    _logger.LogWarning("KYC directory does not exist: {KycDir}", kycDir);
                }
                
                return NotFound(new { message = "Document file not found on server. The file may not have been uploaded yet or was uploaded before the file storage system was implemented." });
            }
            
            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            var contentType = GetContentType(document.FileName);
            
            _logger.LogInformation("Successfully serving KYC document. DocumentId: {DocumentId}, FileSize: {FileSize} bytes", 
                documentId, fileBytes.Length);
            
            return File(fileBytes, contentType, document.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading KYC document {DocumentId}", documentId);
            return StatusCode(500, new { message = "An error occurred while downloading document" });
        }
    }

    /// <summary>
    /// Serve KYC file directly (for img src tags)
    /// </summary>
    [HttpGet("kyc/files/{fileName}")]
    [Authorize]
    public async Task<IActionResult> GetKycFile(string fileName)
    {
        try
        {
            var filePath = Path.Combine(_environment.ContentRootPath, "wwwroot", "files", "kyc", fileName);
            
            _logger.LogInformation("Attempting to serve KYC file. FileName: {FileName}, FilePath: {FilePath}", fileName, filePath);
            
            if (!System.IO.File.Exists(filePath))
            {
                _logger.LogWarning("KYC file not found at path: {FilePath}. ContentRootPath: {ContentRootPath}", 
                    filePath, _environment.ContentRootPath);
                return NotFound();
            }
            
            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            var contentType = GetContentType(fileName);
            
            return File(fileBytes, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error serving KYC file {FileName}", fileName);
            return StatusCode(500);
        }
    }

    private string GetContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".pdf" => "application/pdf",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
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
