using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using CoOwnershipVehicle.User.Api.Data;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Shared.Contracts.Events;
using CoOwnershipVehicle.Domain.Entities;
using MassTransit;
using System.IO;

namespace CoOwnershipVehicle.User.Api.Services;

public interface IUserService
{
    Task<UserProfileDto?> GetUserProfileAsync(Guid userId);
    Task<UserProfileDto?> GetUserByEmailAsync(string email);
    Task<UserProfileDto> UpdateUserProfileAsync(Guid userId, UpdateUserProfileDto updateDto);
    // ChangePasswordAsync removed - password changes should be handled by Auth service only
    Task<KycDocumentDto> UploadKycDocumentAsync(Guid userId, UploadKycDocumentDto uploadDto);
    Task<List<KycDocumentDto>> GetUserKycDocumentsAsync(Guid userId);
    Task<KycDocumentDto?> GetKycDocumentByIdAsync(Guid documentId);
    Task<KycDocumentDto> ReviewKycDocumentAsync(Guid documentId, ReviewKycDocumentDto reviewDto, Guid reviewerId);
    Task<List<KycDocumentDto>> GetPendingKycDocumentsAsync();
    Task<bool> UpdateKycStatusAsync(Guid userId, KycStatus status, string? reason = null);
    Task<UserListResponseDto> GetUsersAsync(UserListRequestDto request);
}

public class UserService : IUserService
{
    private readonly UserDbContext _context;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<UserService> _logger;
    private readonly IWebHostEnvironment _environment;

    public UserService(
        UserDbContext context,
        IPublishEndpoint publishEndpoint,
        ILogger<UserService> logger,
        IWebHostEnvironment environment)
    {
        _context = context;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
        _environment = environment;
    }

    public async Task<UserProfileDto?> GetUserProfileAsync(Guid userId)
    {
        _logger.LogInformation("Attempting to get user profile for {UserId}", userId);
        
        // Clear any cached entities to ensure fresh data
        _context.ChangeTracker.Clear();
        
        // Use AsNoTracking to ensure we get fresh data from database
        var user = await _context.UserProfiles
            .Include(u => u.KycDocuments)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        _logger.LogInformation("User profile query result for {UserId}: {UserFound}", 
            userId, user != null ? "FOUND" : "NOT FOUND");

        if (user == null)
            return null;

        return new UserProfileDto
        {
            Id = user.Id,
            Email = user.Email!,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Phone = user.Phone,
            Address = user.Address,
            City = user.City,
            Country = user.Country,
            PostalCode = user.PostalCode,
            DateOfBirth = user.DateOfBirth,
            KycStatus = (KycStatus)user.KycStatus,
            Role = (UserRole)user.Role,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            KycDocuments = user.KycDocuments.Select(d => new KycDocumentDto
            {
                Id = d.Id,
                UserId = d.UserId,
                UserName = $"{user.FirstName} {user.LastName}",
                DocumentType = (KycDocumentType)d.DocumentType,
                FileName = d.FileName,
                StorageUrl = d.StorageUrl,
                Status = (KycDocumentStatus)d.Status,
                ReviewNotes = d.ReviewNotes,
                ReviewedBy = d.ReviewedBy,
                ReviewedAt = d.ReviewedAt,
                UploadedAt = d.CreatedAt
            }).ToList()
        };
    }

    public async Task<UserProfileDto> UpdateUserProfileAsync(Guid userId, UpdateUserProfileDto updateDto)
    {
        var user = await _context.UserProfiles.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            throw new NotFoundException("User not found");

        // Store old values for comparison
        var oldFirstName = user.FirstName;
        var oldLastName = user.LastName;
        var oldPhone = user.Phone;
        var oldAddress = user.Address;
        var oldCity = user.City;
        var oldCountry = user.Country;
        var oldPostalCode = user.PostalCode;
        var oldDateOfBirth = user.DateOfBirth;

        // Update properties if provided
        if (!string.IsNullOrEmpty(updateDto.FirstName))
            user.FirstName = updateDto.FirstName;
        
        if (!string.IsNullOrEmpty(updateDto.LastName))
            user.LastName = updateDto.LastName;
        
        if (!string.IsNullOrEmpty(updateDto.Phone))
            user.Phone = updateDto.Phone;
        
        if (!string.IsNullOrEmpty(updateDto.Address))
            user.Address = updateDto.Address;
        
        if (!string.IsNullOrEmpty(updateDto.City))
            user.City = updateDto.City;
        
        if (!string.IsNullOrEmpty(updateDto.Country))
            user.Country = updateDto.Country;
        
        if (!string.IsNullOrEmpty(updateDto.PostalCode))
            user.PostalCode = updateDto.PostalCode;
        
        if (updateDto.DateOfBirth.HasValue)
            user.DateOfBirth = updateDto.DateOfBirth.Value;

        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Check if any profile data changed and publish event
        var profileChanged = oldFirstName != user.FirstName ||
                           oldLastName != user.LastName ||
                           oldPhone != user.Phone ||
                           oldAddress != user.Address ||
                           oldCity != user.City ||
                           oldCountry != user.Country ||
                           oldPostalCode != user.PostalCode ||
                           oldDateOfBirth != user.DateOfBirth;

        if (profileChanged)
        {
            // Publish user profile updated event to sync with Auth service
            await _publishEndpoint.Publish(new UserProfileUpdatedEvent
            {
                UserId = user.Id,
                Email = user.Email!,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Phone = user.Phone,
                Address = user.Address,
                City = user.City,
                Country = user.Country,
                PostalCode = user.PostalCode,
                DateOfBirth = user.DateOfBirth,
                Role = (UserRole)user.Role,
                KycStatus = (KycStatus)user.KycStatus,
                UpdatedAt = user.UpdatedAt
            });

            _logger.LogInformation("User profile updated event published for user {UserId}", userId);
        }

        return (await GetUserProfileAsync(userId))!;
    }

    public async Task<UserProfileDto?> GetUserByEmailAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var normalizedEmail = email.ToUpperInvariant();
        var user = await _context.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail || u.Email == email);

        if (user == null)
            return null;

        return new UserProfileDto
        {
            Id = user.Id,
            Email = user.Email!,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Phone = user.Phone,
            Address = user.Address,
            City = user.City,
            Country = user.Country,
            PostalCode = user.PostalCode,
            DateOfBirth = user.DateOfBirth,
            KycStatus = (KycStatus)user.KycStatus,
            Role = (UserRole)user.Role,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt,
            KycDocuments = new List<KycDocumentDto>() // Don't load documents for search
        };
    }

    // ChangePasswordAsync method removed - password changes should be handled by Auth service only

    public async Task<KycDocumentDto> UploadKycDocumentAsync(Guid userId, UploadKycDocumentDto uploadDto)
    {
        var user = await _context.UserProfiles.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            throw new NotFoundException("User not found");

        // In production, upload to blob storage (S3, Azure Blob, etc.)
        var storageUrl = await SaveDocumentToStorageAsync(uploadDto.Base64Content, uploadDto.FileName);

        var kycDocument = new Domain.Entities.KycDocument
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DocumentType = (Domain.Entities.KycDocumentType)uploadDto.DocumentType,
            FileName = uploadDto.FileName,
            StorageUrl = storageUrl,
            Status = Domain.Entities.KycDocumentStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.KycDocuments.Add(kycDocument);
        await _context.SaveChangesAsync();

        // Update user KYC status to InReview if it was Pending
        if (user.KycStatus == Domain.Entities.KycStatus.Pending)
        {
            await UpdateKycStatusAsync(userId, KycStatus.InReview, "KYC documents uploaded");
        }

        _logger.LogInformation("KYC document uploaded for user {UserId}: {DocumentType}", userId, uploadDto.DocumentType);

        return new KycDocumentDto
        {
            Id = kycDocument.Id,
            UserId = kycDocument.UserId,
            UserName = $"{user.FirstName} {user.LastName}",
            DocumentType = (KycDocumentType)kycDocument.DocumentType,
            FileName = kycDocument.FileName,
            StorageUrl = kycDocument.StorageUrl,
            Status = (KycDocumentStatus)kycDocument.Status,
            UploadedAt = kycDocument.CreatedAt
        };
    }

    public async Task<List<KycDocumentDto>> GetUserKycDocumentsAsync(Guid userId)
    {
        var documents = await _context.KycDocuments
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        var userIds = documents.Select(d => d.UserId).Distinct().ToList();
        var users = await _context.UserProfiles
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

        return documents.Select(d => 
        {
            var user = users.GetValueOrDefault(d.UserId);
            return new KycDocumentDto
            {
                Id = d.Id,
                UserId = d.UserId,
                UserName = user != null ? $"{user.FirstName} {user.LastName}" : "Unknown",
                DocumentType = (KycDocumentType)d.DocumentType,
                FileName = d.FileName,
                StorageUrl = d.StorageUrl,
                Status = (KycDocumentStatus)d.Status,
                ReviewNotes = d.ReviewNotes,
                ReviewedBy = d.ReviewedBy,
                ReviewedAt = d.ReviewedAt,
                UploadedAt = d.CreatedAt
            };
        }).ToList();
    }

    public async Task<KycDocumentDto?> GetKycDocumentByIdAsync(Guid documentId)
    {
        var document = await _context.KycDocuments
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document == null)
            return null;

        var user = await _context.UserProfiles
            .FirstOrDefaultAsync(u => u.Id == document.UserId);

        return new KycDocumentDto
        {
            Id = document.Id,
            UserId = document.UserId,
            UserName = user != null ? $"{user.FirstName} {user.LastName}" : "Unknown",
            DocumentType = (KycDocumentType)document.DocumentType,
            FileName = document.FileName,
            StorageUrl = document.StorageUrl,
            Status = (KycDocumentStatus)document.Status,
            ReviewNotes = document.ReviewNotes,
            ReviewedBy = document.ReviewedBy,
            ReviewedAt = document.ReviewedAt,
            UploadedAt = document.CreatedAt
        };
    }

    public async Task<KycDocumentDto> ReviewKycDocumentAsync(Guid documentId, ReviewKycDocumentDto reviewDto, Guid reviewerId)
    {
        var document = await _context.KycDocuments
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document == null)
            throw new NotFoundException("KYC document not found");

        var reviewer = await _context.UserProfiles.FirstOrDefaultAsync(u => u.Id == reviewerId);
        if (reviewer == null)
            throw new NotFoundException("Reviewer not found");

        document.Status = (Domain.Entities.KycDocumentStatus)reviewDto.Status;
        document.ReviewNotes = reviewDto.ReviewNotes;
        document.ReviewedBy = reviewerId;
        document.ReviewedAt = DateTime.UtcNow;
        document.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Update overall KYC status based on document reviews
        await UpdateOverallKycStatusAsync(document.UserId);

        _logger.LogInformation("KYC document {DocumentId} reviewed by {ReviewerId} with status {Status}", 
            documentId, reviewerId, reviewDto.Status);

        var documentUser = await _context.UserProfiles.FirstOrDefaultAsync(u => u.Id == document.UserId);
        return new KycDocumentDto
        {
            Id = document.Id,
            UserId = document.UserId,
            UserName = documentUser != null ? $"{documentUser.FirstName} {documentUser.LastName}" : "Unknown",
            DocumentType = (KycDocumentType)document.DocumentType,
            FileName = document.FileName,
            StorageUrl = document.StorageUrl,
            Status = (KycDocumentStatus)document.Status,
            ReviewNotes = document.ReviewNotes,
            ReviewedBy = document.ReviewedBy,
            ReviewerName = $"{reviewer.FirstName} {reviewer.LastName}",
            ReviewedAt = document.ReviewedAt,
            UploadedAt = document.CreatedAt
        };
    }

    public async Task<List<KycDocumentDto>> GetPendingKycDocumentsAsync()
    {
        var documents = await _context.KycDocuments
            .Where(d => d.Status == Domain.Entities.KycDocumentStatus.Pending || 
                       d.Status == Domain.Entities.KycDocumentStatus.UnderReview)
            .OrderBy(d => d.CreatedAt)
            .ToListAsync();

        var userIds = documents.Select(d => d.UserId).Distinct().ToList();
        var users = await _context.UserProfiles
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

        return documents.Select(d => 
        {
            var user = users.GetValueOrDefault(d.UserId);
            return new KycDocumentDto
            {
                Id = d.Id,
                UserId = d.UserId,
                UserName = user != null ? $"{user.FirstName} {user.LastName}" : "Unknown",
                DocumentType = (KycDocumentType)d.DocumentType,
                FileName = d.FileName,
                StorageUrl = d.StorageUrl,
                Status = (KycDocumentStatus)d.Status,
                UploadedAt = d.CreatedAt
            };
        }).ToList();
    }

    public async Task<bool> UpdateKycStatusAsync(Guid userId, KycStatus status, string? reason = null)
    {
        var user = await _context.UserProfiles.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            return false;

        var oldStatus = user.KycStatus;
        user.KycStatus = (Domain.Entities.KycStatus)status;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Publish KYC status change event
        await _publishEndpoint.Publish(new UserKycStatusChangedEvent
        {
            UserId = userId,
            OldStatus = (KycStatus)oldStatus,
            NewStatus = status,
            Reason = reason
        });

        _logger.LogInformation("KYC status updated for user {UserId}: {OldStatus} -> {NewStatus}", 
            userId, oldStatus, status);

        return true;
    }

    private async Task<string> SaveDocumentToStorageAsync(string base64Content, string fileName)
    {
        try
        {
            // Decode base64 content
            var fileBytes = Convert.FromBase64String(base64Content);
            
            // Use ContentRootPath for consistent path in both local and Docker environments
            var contentRootPath = _environment.ContentRootPath;
            var kycFilesPath = Path.Combine(contentRootPath, "wwwroot", "files", "kyc");
            
            // Create directory if it doesn't exist
            if (!Directory.Exists(kycFilesPath))
            {
                Directory.CreateDirectory(kycFilesPath);
                _logger.LogInformation("Created KYC files directory: {KycFilesPath}", kycFilesPath);
            }
            
            // Generate unique file name
            var fileId = Guid.NewGuid();
            var fileExtension = Path.GetExtension(fileName);
            var uniqueFileName = $"{fileId}{fileExtension}";
            var filePath = Path.Combine(kycFilesPath, uniqueFileName);
            
            // Save file to disk
            await System.IO.File.WriteAllBytesAsync(filePath, fileBytes);
            
            // Return relative URL path for serving via API
            var storageUrl = $"/api/User/kyc/files/{fileId}{fileExtension}";
            
            _logger.LogInformation("KYC document saved successfully. FilePath: {FilePath}, StorageUrl: {StorageUrl}, FileSize: {FileSize} bytes", 
                filePath, storageUrl, fileBytes.Length);
            
            return storageUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving KYC document to storage");
            // Fallback to mock URL if file save fails
            var fileId = Guid.NewGuid().ToString();
            return $"https://storage.coownership.com/kyc/{fileId}/{fileName}";
        }
    }

    private async Task UpdateOverallKycStatusAsync(Guid userId)
    {
        var user = await _context.UserProfiles.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return;

        var documents = await _context.KycDocuments
            .Where(d => d.UserId == userId)
            .ToListAsync();

        if (!documents.Any())
        {
            await UpdateKycStatusAsync(userId, KycStatus.Pending, "No documents uploaded");
            return;
        }

        var hasRejected = documents.Any(d => d.Status == Domain.Entities.KycDocumentStatus.Rejected);
        var hasRequiresUpdate = documents.Any(d => d.Status == Domain.Entities.KycDocumentStatus.RequiresUpdate);
        var hasPending = documents.Any(d => d.Status == Domain.Entities.KycDocumentStatus.Pending || 
                                           d.Status == Domain.Entities.KycDocumentStatus.UnderReview);
        var allApproved = documents.All(d => d.Status == Domain.Entities.KycDocumentStatus.Approved);

        if (hasRejected)
        {
            await UpdateKycStatusAsync(userId, KycStatus.Rejected, "One or more documents rejected");
        }
        else if (hasRequiresUpdate)
        {
            await UpdateKycStatusAsync(userId, KycStatus.Pending, "Documents require updates");
        }
        else if (hasPending)
        {
            await UpdateKycStatusAsync(userId, KycStatus.InReview, "Documents under review");
        }
        else if (allApproved && documents.Count >= 2) // Require at least 2 documents
        {
            await UpdateKycStatusAsync(userId, KycStatus.Approved, "All documents approved");
        }
        else
        {
            await UpdateKycStatusAsync(userId, KycStatus.InReview, "Insufficient documents");
        }
    }

    public async Task<UserListResponseDto> GetUsersAsync(UserListRequestDto request)
    {
        var query = _context.UserProfiles.AsQueryable();

        // Apply search filter
        if (!string.IsNullOrEmpty(request.Search))
        {
            var searchTerm = request.Search.ToLower();
            query = query.Where(u =>
                (u.FirstName != null && u.FirstName.ToLower().Contains(searchTerm)) ||
                (u.LastName != null && u.LastName.ToLower().Contains(searchTerm)) ||
                (u.Email != null && u.Email.ToLower().Contains(searchTerm)) ||
                (u.Phone != null && u.Phone.Contains(searchTerm)));
        }

        // Apply role filter
        if (request.Role.HasValue)
        {
            query = query.Where(u => u.Role == (Domain.Entities.UserRole)request.Role.Value);
        }

        // Apply KYC status filter
        if (request.KycStatus.HasValue)
        {
            query = query.Where(u => u.KycStatus == (Domain.Entities.KycStatus)request.KycStatus.Value);
        }

        // Apply sorting
        query = request.SortBy?.ToLower() switch
        {
            "email" => request.SortDirection?.ToLower() == "asc"
                ? query.OrderBy(u => u.Email)
                : query.OrderByDescending(u => u.Email),
            "firstname" => request.SortDirection?.ToLower() == "asc"
                ? query.OrderBy(u => u.FirstName)
                : query.OrderByDescending(u => u.FirstName),
            "lastname" => request.SortDirection?.ToLower() == "asc"
                ? query.OrderBy(u => u.LastName)
                : query.OrderByDescending(u => u.LastName),
            "role" => request.SortDirection?.ToLower() == "asc"
                ? query.OrderBy(u => u.Role)
                : query.OrderByDescending(u => u.Role),
            "kycstatus" => request.SortDirection?.ToLower() == "asc"
                ? query.OrderBy(u => u.KycStatus)
                : query.OrderByDescending(u => u.KycStatus),
            _ => request.SortDirection?.ToLower() == "asc"
                ? query.OrderBy(u => u.CreatedAt)
                : query.OrderByDescending(u => u.CreatedAt)
        };

        // Get total count before pagination
        var totalCount = await query.CountAsync();

        // Apply pagination
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Max(1, Math.Min(100, request.PageSize));
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var users = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserSummaryDto
            {
                Id = u.Id,
                Email = u.Email ?? string.Empty,
                FirstName = u.FirstName ?? string.Empty,
                LastName = u.LastName ?? string.Empty,
                Phone = u.Phone,
                Role = (UserRole)u.Role,
                KycStatus = (KycStatus)u.KycStatus,
                AccountStatus = UserAccountStatus.Active, // UserProfiles doesn't have LockoutEnd, default to Active
                CreatedAt = u.CreatedAt,
                LastLoginAt = null // Not tracked in UserProfiles
            })
            .ToListAsync();

        return new UserListResponseDto
        {
            Users = users,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages
        };
    }
}

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}
