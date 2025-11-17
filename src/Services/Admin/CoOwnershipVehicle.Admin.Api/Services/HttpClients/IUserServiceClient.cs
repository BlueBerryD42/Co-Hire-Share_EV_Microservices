using CoOwnershipVehicle.Shared.Contracts.DTOs;

namespace CoOwnershipVehicle.Admin.Api.Services.HttpClients;

public interface IUserServiceClient
{
    Task<UserProfileDto?> GetUserProfileAsync(Guid userId);
    Task<List<UserProfileDto>> GetUsersAsync(UserListRequestDto? request = null);
    Task<List<KycDocumentDto>> GetPendingKycDocumentsAsync();
    Task<KycDocumentDto> ReviewKycDocumentAsync(Guid documentId, ReviewKycDocumentDto reviewDto);
    Task<bool> UpdateKycStatusAsync(Guid userId, KycStatus status, string? reason = null);
}

