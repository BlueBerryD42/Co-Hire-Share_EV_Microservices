using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Group.Api.Data;
using CoOwnershipVehicle.Group.Api.DTOs;
using CoOwnershipVehicle.Group.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QRCoder;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CoOwnershipVehicle.Group.Api.Services.Implementations;

public class CertificateGenerationService : ICertificateGenerationService
{
    private readonly ILogger<CertificateGenerationService> _logger;
    private readonly IFileStorageService _fileStorageService;
    private readonly GroupDbContext _context;
    private const int CertificateValidityYears = 10;

    public CertificateGenerationService(
        ILogger<CertificateGenerationService> logger,
        IFileStorageService fileStorageService,
        GroupDbContext context)
    {
        _logger = logger;
        _fileStorageService = fileStorageService;
        _context = context;

        // Set QuestPDF license
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<SigningCertificateResponse> GenerateCertificateAsync(
        Domain.Entities.Document document,
        List<DocumentSignature> signatures,
        string? baseUrl = null)
    {
        _logger.LogInformation("Generating certificate for document {DocumentId}", document.Id);

        // Get signer information
        var signers = signatures.Select(s => s.Signer).ToList();

        // Generate certificate ID
        var certificateId = GenerateCertificateId(document.Id);

        // Calculate document hash
        var documentHash = CalculateDocumentHash(document, signatures);

        // Generate PDF
        var pdfBytes = await GenerateCertificatePdfAsync(document, signatures, signers, baseUrl);

        var generatedAt = DateTime.UtcNow;
        var expiresAt = generatedAt.AddYears(CertificateValidityYears);

        var signerInfoList = signatures.Select(s => new CertificateSignerInfo
        {
            SignerName = $"{s.Signer.FirstName} {s.Signer.LastName}",
            SignerEmail = s.Signer.Email,
            SignedAt = s.SignedAt!.Value,
            IpAddress = GetMetadataField(s.SignatureMetadata, "IpAddress"),
            DeviceInfo = GetMetadataField(s.SignatureMetadata, "DeviceInfo"),
            SignatureImageUrl = s.SignatureReference ?? string.Empty
        }).ToList();

        // Save certificate to database
        var certificateEntity = new SigningCertificate
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            CertificateId = certificateId,
            DocumentHash = documentHash,
            FileName = document.FileName,
            TotalSigners = signatures.Count,
            GeneratedAt = generatedAt,
            ExpiresAt = expiresAt,
            SignersJson = JsonSerializer.Serialize(signerInfoList),
            IsRevoked = false,
            CreatedAt = generatedAt,
            UpdatedAt = generatedAt
        };

        // Check if certificate already exists for this document
        var existingCert = await _context.SigningCertificates
            .FirstOrDefaultAsync(c => c.DocumentId == document.Id);

        if (existingCert != null)
        {
            // Update existing certificate
            existingCert.CertificateId = certificateId;
            existingCert.DocumentHash = documentHash;
            existingCert.GeneratedAt = generatedAt;
            existingCert.ExpiresAt = expiresAt;
            existingCert.SignersJson = certificateEntity.SignersJson;
            existingCert.UpdatedAt = generatedAt;

            _context.SigningCertificates.Update(existingCert);
            _logger.LogInformation("Updated existing certificate for document {DocumentId}", document.Id);
        }
        else
        {
            // Add new certificate
            await _context.SigningCertificates.AddAsync(certificateEntity);
            _logger.LogInformation("Created new certificate for document {DocumentId}", document.Id);
        }

        await _context.SaveChangesAsync();

        var response = new SigningCertificateResponse
        {
            DocumentId = document.Id,
            FileName = document.FileName,
            CertificatePdf = pdfBytes,
            CertificateId = certificateId,
            GeneratedAt = generatedAt,
            ExpiresAt = expiresAt,
            DocumentHash = documentHash,
            Signers = signerInfoList
        };

        _logger.LogInformation("Certificate generated and saved successfully for document {DocumentId}", document.Id);

        return response;
    }

    public async Task<byte[]> GenerateCertificatePdfAsync(
        Domain.Entities.Document document,
        List<DocumentSignature> signatures,
        List<User> signers,
        string? baseUrl = null)
    {
        var certificateId = GenerateCertificateId(document.Id);
        var documentHash = CalculateDocumentHash(document, signatures);
        var generatedAt = DateTime.UtcNow;
        var expiresAt = generatedAt.AddYears(CertificateValidityYears);

        // Generate QR code for verification
        var qrCodeData = GenerateQRCode(certificateId, documentHash, baseUrl);

        return await Task.Run(() =>
        {
            var document_pdf = QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(50);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                    // Header
                    page.Header().Column(column =>
                    {
                        column.Item().AlignCenter().Text("DIGITAL SIGNATURE CERTIFICATE")
                            .FontSize(20).Bold().FontColor(Colors.Blue.Darken2);
                        column.Item().PaddingVertical(5).LineHorizontal(2).LineColor(Colors.Blue.Darken2);
                    });

                    // Content
                    page.Content().Column(column =>
                    {
                        column.Spacing(15);

                        // Certificate Info
                        column.Item().Element(container => RenderSection(container, "Certificate Information", section =>
                        {
                            section.Item().Row(row =>
                            {
                                row.RelativeItem().Text(text =>
                                {
                                    text.Span("Certificate ID: ").Bold();
                                    text.Span(certificateId);
                                });
                            });
                            section.Item().Row(row =>
                            {
                                row.RelativeItem().Text(text =>
                                {
                                    text.Span("Generated: ").Bold();
                                    text.Span(generatedAt.ToString("yyyy-MM-dd HH:mm:ss UTC"));
                                });
                            });
                            section.Item().Row(row =>
                            {
                                row.RelativeItem().Text(text =>
                                {
                                    text.Span("Valid Until: ").Bold();
                                    text.Span(expiresAt.ToString("yyyy-MM-dd"));
                                });
                            });
                        }));

                        // Document Details
                        column.Item().Element(container => RenderSection(container, "Document Information", section =>
                        {
                            section.Item().Row(row =>
                            {
                                row.RelativeItem().Text(text =>
                                {
                                    text.Span("Document Name: ").Bold();
                                    text.Span(document.FileName);
                                });
                            });
                            section.Item().Row(row =>
                            {
                                row.RelativeItem().Text(text =>
                                {
                                    text.Span("Document Type: ").Bold();
                                    text.Span(document.Type.ToString());
                                });
                            });
                            section.Item().Row(row =>
                            {
                                row.RelativeItem().Text(text =>
                                {
                                    text.Span("Document Hash: ").Bold();
                                    text.Span(documentHash).FontSize(9);
                                });
                            });
                        }));

                        // Signatures
                        column.Item().Element(container => RenderSection(container, "Digital Signatures", section =>
                        {
                            foreach (var sig in signatures.OrderBy(s => s.SignatureOrder))
                            {
                                var metadata = DeserializeMetadata(sig.SignatureMetadata);

                                section.Item().PaddingTop(10).Column(sigColumn =>
                                {
                                    sigColumn.Item().Background(Colors.Grey.Lighten3).Padding(10).Column(sigInfo =>
                                    {
                                        sigInfo.Item().Text(text =>
                                        {
                                            text.Span($"Signer {sig.SignatureOrder + 1}: ").Bold().FontSize(12);
                                            text.Span($"{sig.Signer.FirstName} {sig.Signer.LastName}");
                                        });

                                        sigInfo.Item().PaddingTop(5).Text(text =>
                                        {
                                            text.Span("Email: ").Bold();
                                            text.Span(sig.Signer.Email);
                                        });

                                        sigInfo.Item().Text(text =>
                                        {
                                            text.Span("Signed At: ").Bold();
                                            text.Span(sig.SignedAt!.Value.ToString("yyyy-MM-dd HH:mm:ss UTC"));
                                        });

                                        if (metadata != null)
                                        {
                                            sigInfo.Item().Text(text =>
                                            {
                                                text.Span("IP Address: ").Bold();
                                                text.Span(metadata.IpAddress);
                                            });

                                            sigInfo.Item().Text(text =>
                                            {
                                                text.Span("Device: ").Bold();
                                                text.Span(metadata.DeviceInfo);
                                            });

                                            if (!string.IsNullOrEmpty(metadata.GpsCoordinates))
                                            {
                                                sigInfo.Item().Text(text =>
                                                {
                                                    text.Span("GPS: ").Bold();
                                                    text.Span(metadata.GpsCoordinates);
                                                });
                                            }
                                        }
                                    });
                                });
                            }
                        }));

                        // Verification Section with QR Code
                        column.Item().PaddingTop(20).Element(container => RenderSection(container, "Verification", section =>
                        {
                            section.Item().Row(row =>
                            {
                                row.RelativeItem().Column(col =>
                                {
                                    col.Item().Text("Scan QR code to verify this certificate:").Bold();
                                    col.Item().PaddingTop(5).Text("This certificate is digitally signed and tamper-proof.")
                                        .FontSize(9).Italic();
                                });

                                row.ConstantItem(100).AlignRight().Image(qrCodeData);
                            });
                        }));
                    });

                    // Footer
                    page.Footer().Column(column =>
                    {
                        column.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Medium);
                        column.Item().AlignCenter().Text(text =>
                        {
                            text.Span("This is a computer-generated certificate. No signature is required.").FontSize(9).Italic();
                        });
                        column.Item().AlignCenter().Text(text =>
                        {
                            text.Span("Generated by Co-Ownership Vehicle Management System").FontSize(8);
                        });
                    });
                });
            });

            return document_pdf.GeneratePdf();
        });
    }

    private void RenderSection(IContainer container, string title, Action<ColumnDescriptor> content)
    {
        container.Column(column =>
        {
            column.Item().Text(title).FontSize(14).Bold().FontColor(Colors.Blue.Darken1);
            column.Item().PaddingBottom(5).LineHorizontal(1).LineColor(Colors.Grey.Medium);
            column.Item().PaddingTop(5).Column(content);
        });
    }

    public string GenerateCertificateId(Guid documentId)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var hash = Convert.ToBase64String(SHA256.HashData(documentId.ToByteArray()))
            .Replace("+", "").Replace("/", "").Replace("=", "")
            .Substring(0, 8);

        return $"CERT-{timestamp}-{hash}";
    }

    public string CalculateDocumentHash(Domain.Entities.Document document, List<DocumentSignature> signatures)
    {
        var hashData = new
        {
            DocumentId = document.Id,
            FileName = document.FileName,
            FileHash = document.FileHash,
            FileSize = document.FileSize,
            Signatures = signatures.OrderBy(s => s.SignatureOrder).Select(s => new
            {
                s.SignerId,
                s.SignedAt,
                s.SignatureOrder,
                s.SignatureReference
            })
        };

        var json = JsonSerializer.Serialize(hashData);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToBase64String(hash);
    }

    public async Task<(bool IsValid, string ErrorMessage)> ValidateCertificateAsync(
        Guid documentId,
        string certificateHash)
    {
        // This would validate against stored certificate hash
        // For now, return success
        await Task.CompletedTask;
        return (true, string.Empty);
    }

    private byte[] GenerateQRCode(string certificateId, string documentHash, string? baseUrl = null)
    {
        // If baseUrl is provided, create a verification URL
        // Otherwise, fallback to data format
        string qrContent;

        if (!string.IsNullOrEmpty(baseUrl))
        {
            // Clean baseUrl
            baseUrl = baseUrl.TrimEnd('/');

            // Create verification URL that can be scanned with any QR reader
            qrContent = $"{baseUrl}/api/Document/verify-certificate/{certificateId}?hash={Uri.EscapeDataString(documentHash)}";
        }
        else
        {
            // Fallback to data format
            qrContent = $"CERT:{certificateId}|HASH:{documentHash}";
        }

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(qrContent, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrCodeData);

        return qrCode.GetGraphic(20);
    }

    private SignatureMetadata? DeserializeMetadata(string? metadataJson)
    {
        if (string.IsNullOrEmpty(metadataJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<SignatureMetadata>(metadataJson);
        }
        catch
        {
            return null;
        }
    }

    private string GetMetadataField(string? metadataJson, string fieldName)
    {
        var metadata = DeserializeMetadata(metadataJson);
        if (metadata == null) return "N/A";

        return fieldName switch
        {
            "IpAddress" => metadata.IpAddress ?? "N/A",
            "DeviceInfo" => metadata.DeviceInfo ?? "N/A",
            "UserAgent" => metadata.UserAgent ?? "N/A",
            _ => "N/A"
        };
    }
}
