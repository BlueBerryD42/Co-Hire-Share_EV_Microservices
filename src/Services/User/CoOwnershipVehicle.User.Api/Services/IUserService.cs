using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CoOwnershipVehicle.Data;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Shared.Contracts.Events;
using MassTransit;

namespace CoOwnershipVehicle.User.Api.Services;

public interface IUserService
{
    Task<UserProfileDto?> GetUserProfileAsync(Guid userId);
    Task<UserProfileDto> UpdateUserProfileAsync(Guid userId, UpdateUserProfileDto updateDto);
    Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordDto changePasswordDto);
    Task<KycDocumentDto> UploadKycDocumentAsync(Guid userId, UploadKycDocumentDto uploadDto);
    Task<List<KycDocumentDto>> GetUserKycDocumentsAsync(Guid userId);
    Task<KycDocumentDto> ReviewKycDocumentAsync(Guid documentId, ReviewKycDocumentDto reviewDto, Guid reviewerId);
    Task<List<KycDocumentDto>> GetPendingKycDocumentsAsync();
    Task<bool> UpdateKycStatusAsync(Guid userId, KycStatus status, string? reason = null);
}

public class UserService : IUserService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<Domain.Entities.User> _userManager;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<UserService> _logger;

    public UserService(
        ApplicationDbContext context,
        UserManager<Domain.Entities.User> userManager,
        IPublishEndpoint publishEndpoint,
        ILogger<UserService> logger)
    {
        _context = context;
        _userManager = userManager;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task<UserProfileDto?> GetUserProfileAsync(Guid userId)
    {
        var user = await _context.Users
            .Include(u => u.KycDocuments)
            .FirstOrDefaultAsync(u => u.Id == userId);

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
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            throw new NotFoundException("User not found");

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

        return (await GetUserProfileAsync(userId))!;
    }

    public async Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordDto changePasswordDto)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
            return false;

        var result = await _userManager.ChangePasswordAsync(user, changePasswordDto.CurrentPassword, changePasswordDto.NewPassword);
        
        if (result.Succeeded)
        {
            _logger.LogInformation("Password changed successfully for user {UserId}", userId);
            return true;
        }

        _logger.LogWarning("Password change failed for user {UserId}: {Errors}", userId, string.Join(", ", result.Errors.Select(e => e.Description)));
        return false;
    }

    public async Task<KycDocumentDto> UploadKycDocumentAsync(Guid userId, UploadKycDocumentDto uploadDto)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
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
            .Include(d => d.User)
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        return documents.Select(d => new KycDocumentDto
        {
            Id = d.Id,
            UserId = d.UserId,
            UserName = $"{d.User.FirstName} {d.User.LastName}",
            DocumentType = (KycDocumentType)d.DocumentType,
            FileName = d.FileName,
            StorageUrl = d.StorageUrl,
            Status = (KycDocumentStatus)d.Status,
            ReviewNotes = d.ReviewNotes,
            ReviewedBy = d.ReviewedBy,
            ReviewedAt = d.ReviewedAt,
            UploadedAt = d.CreatedAt
        }).ToList();
    }

    public async Task<KycDocumentDto> ReviewKycDocumentAsync(Guid documentId, ReviewKycDocumentDto reviewDto, Guid reviewerId)
    {
        var document = await _context.KycDocuments
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document == null)
            throw new NotFoundException("KYC document not found");

        var reviewer = await _context.Users.FirstOrDefaultAsync(u => u.Id == reviewerId);
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

        return new KycDocumentDto
        {
            Id = document.Id,
            UserId = document.UserId,
            UserName = $"{document.User.FirstName} {document.User.LastName}",
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
            .Include(d => d.User)
            .Where(d => d.Status == Domain.Entities.KycDocumentStatus.Pending || 
                       d.Status == Domain.Entities.KycDocumentStatus.UnderReview)
            .OrderBy(d => d.CreatedAt)
            .ToListAsync();

        return documents.Select(d => new KycDocumentDto
        {
            Id = d.Id,
            UserId = d.UserId,
            UserName = $"{d.User.FirstName} {d.User.LastName}",
            DocumentType = (KycDocumentType)d.DocumentType,
            FileName = d.FileName,
            StorageUrl = d.StorageUrl,
            Status = (KycDocumentStatus)d.Status,
            UploadedAt = d.CreatedAt
        }).ToList();
    }

    public async Task<bool> UpdateKycStatusAsync(Guid userId, KycStatus status, string? reason = null)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
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
        // In production, implement actual cloud storage upload
        // For demo purposes, we'll simulate a storage URL
        var fileId = Guid.NewGuid().ToString();
        var storageUrl = $"https://storage.coownership.com/kyc/{fileId}/{fileName}";
        
        // Simulate async upload
        await Task.Delay(100);
        
        return storageUrl;
    }

    private async Task UpdateOverallKycStatusAsync(Guid userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
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
}

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}
