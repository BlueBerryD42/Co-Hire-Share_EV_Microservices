using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Group.Api.DTOs;

namespace CoOwnershipVehicle.Group.Api.Services.Interfaces;

public interface ICertificateGenerationService
{
    Task<SigningCertificateResponse> GenerateCertificateAsync(Domain.Entities.Document document, List<DocumentSignature> signatures, string? baseUrl = null);
    Task<byte[]> GenerateCertificatePdfAsync(Domain.Entities.Document document, List<DocumentSignature> signatures, List<User> signers, string? baseUrl = null);
    string GenerateCertificateId(Guid documentId);
    string CalculateDocumentHash(Domain.Entities.Document document, List<DocumentSignature> signatures);
    Task<(bool IsValid, string ErrorMessage)> ValidateCertificateAsync(Guid documentId, string certificateHash);
}
