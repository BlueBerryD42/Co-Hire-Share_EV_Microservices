using CoOwnershipVehicle.Group.Api.DTOs;
using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Group.Api.Services.Interfaces;

/// <summary>
/// Service for sharing documents with external parties
/// </summary>
public interface IDocumentShareService
{
    /// <summary>
    /// Create a share link for a document
    /// </summary>
    Task<CreateShareResponse> CreateShareAsync(Guid documentId, CreateShareRequest request, Guid userId, string baseUrl);

    /// <summary>
    /// Access a shared document via token
    /// </summary>
    Task<SharedDocumentResponse> GetSharedDocumentAsync(string shareToken, AccessSharedDocumentRequest? request = null, string? ipAddress = null, string? userAgent = null);

    /// <summary>
    /// Download a shared document
    /// </summary>
    Task<DocumentDownloadResponse> DownloadSharedDocumentAsync(string shareToken, string? password = null, string? ipAddress = null, string? userAgent = null);

    /// <summary>
    /// Preview a shared document
    /// </summary>
    Task<DocumentDownloadResponse> PreviewSharedDocumentAsync(string shareToken, string? password = null, string? ipAddress = null, string? userAgent = null);

    /// <summary>
    /// Get all shares for a document
    /// </summary>
    Task<DocumentShareListResponse> GetDocumentSharesAsync(Guid documentId, Guid userId);

    /// <summary>
    /// Get share analytics
    /// </summary>
    Task<ShareAnalyticsResponse> GetShareAnalyticsAsync(Guid shareId, Guid userId);

    /// <summary>
    /// Revoke a share
    /// </summary>
    Task RevokeShareAsync(Guid documentId, Guid shareId, Guid userId);

    /// <summary>
    /// Update share expiration
    /// </summary>
    Task UpdateShareExpirationAsync(Guid shareId, DateTime? expiresAt, Guid userId);

    /// <summary>
    /// Validate share token and check if it's accessible
    /// </summary>
    Task<(bool IsValid, string? ErrorMessage)> ValidateShareTokenAsync(string shareToken, string? password = null);
}
