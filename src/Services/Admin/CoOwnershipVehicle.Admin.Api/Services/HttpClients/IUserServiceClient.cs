using CoOwnershipVehicle.Shared.Contracts.DTOs;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Admin.Api.Services.HttpClients;

public interface IUserServiceClient
{
    Task<UserProfileDto?> GetUserProfileAsync(Guid userId);
    Task<List<UserProfileDto>> GetUsersAsync(UserListRequestDto? request = null);
    Task<List<KycDocumentDto>> GetPendingKycDocumentsAsync();
    Task<List<KycDocumentDto>> GetUserKycDocumentsAsync(Guid userId);
    Task<KycDocumentDto?> GetKycDocumentAsync(Guid documentId);
    Task<byte[]?> DownloadKycDocumentAsync(Guid documentId);
    Task<KycDocumentDto> ReviewKycDocumentAsync(Guid documentId, ReviewKycDocumentDto reviewDto);
    Task<bool> UpdateKycStatusAsync(Guid userId, KycStatus status, string? reason = null);
}

