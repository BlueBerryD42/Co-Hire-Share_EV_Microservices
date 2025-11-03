using CoOwnershipVehicle.Group.Api.DTOs;
using Microsoft.AspNetCore.Http;

namespace CoOwnershipVehicle.Group.Api.Services.Interfaces;

public interface IDocumentService
{
    Task<DocumentUploadResponse> UploadDocumentAsync(DocumentUploadRequest request, Guid userId);
    Task<List<DocumentListResponse>> GetGroupDocumentsAsync(Guid groupId, Guid userId);
    Task<PaginatedDocumentResponse> GetGroupDocumentsPaginatedAsync(Guid groupId, Guid userId, DocumentQueryParameters parameters);
    Task<DocumentDetailResponse> GetDocumentByIdAsync(Guid documentId, Guid userId);
    Task DeleteDocumentAsync(Guid documentId, Guid userId);
    Task<string> GetDocumentDownloadUrlAsync(Guid documentId, Guid userId);
    Task<DocumentDownloadResponse> DownloadDocumentAsync(Guid documentId, Guid userId, string ipAddress, string? userAgent);
    Task<DocumentDownloadResponse> PreviewDocumentAsync(Guid documentId, Guid userId);
    Task<DownloadTrackingInfo> GetDownloadTrackingInfoAsync(Guid documentId, Guid userId);
    Task<SendForSigningResponse> SendForSigningAsync(Guid documentId, SendForSigningRequest request, Guid userId, string baseUrl);
    Task<SignDocumentResponse> SignDocumentAsync(Guid documentId, SignDocumentRequest request, Guid userId, string ipAddress, string? userAgent);
    Task<DocumentSignatureStatusResponse> GetSignatureStatusAsync(Guid documentId, Guid userId);
    Task<SigningCertificateResponse> GetSigningCertificateAsync(Guid documentId, Guid userId, string? baseUrl = null);
    Task<CertificateVerificationResult> VerifyCertificateAsync(string certificateId, string? providedHash = null);
    Task<(bool IsValid, string ErrorMessage)> ValidateFileAsync(IFormFile file);
}