using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Group.Api.Data;
using CoOwnershipVehicle.Group.Api.DTOs;
using CoOwnershipVehicle.Group.Api.Services.Implementations;
using CoOwnershipVehicle.Group.Api.Services.Interfaces;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CoOwnershipVehicle.Group.Api.Tests;

/// <summary>
/// Integration tests for complete signing workflows
/// Tests end-to-end scenarios: Upload → Send → Sign → Certificate
/// </summary>
public class SigningWorkflowIntegrationTests : IDisposable
{
    private readonly GroupDbContext _context;
    private readonly Mock<IFileStorageService> _mockStorageService;
    private readonly Mock<ILogger<DocumentService>> _mockLogger;
    private readonly Mock<ICertificateGenerationService> _mockCertificateService;
    private readonly DocumentService _documentService;
    private readonly Guid _testGroupId = Guid.NewGuid();
    private readonly List<Guid> _signerIds;

    public SigningWorkflowIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<GroupDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new GroupDbContext(options);
        _mockStorageService = new Mock<IFileStorageService>();
        _mockLogger = new Mock<ILogger<DocumentService>>();
        _mockCertificateService = new Mock<ICertificateGenerationService>();
        var mockVirusScan = new Mock<IVirusScanService>();
        var mockSigningToken = new Mock<ISigningTokenService>();
        var mockNotification = new Mock<INotificationService>();

        // Setup signing token service mock to generate and validate tokens
        mockSigningToken
            .Setup(x => x.GenerateSigningToken(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>()))
            .Returns<Guid, Guid, int>((docId, signerId, expDays) => GenerateTestToken(docId, signerId, expDays));
        
        mockSigningToken
            .Setup(x => x.ValidateSigningToken(It.IsAny<string>()))
            .Returns<string>(token => ValidateTestToken(token));
        
        mockSigningToken
            .Setup(x => x.GenerateSigningUrl(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((token, baseUrl) => $"{baseUrl.TrimEnd('/')}/sign/{token}");

        _signerIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        // Mock IUserServiceClient
        var userServiceClientMock = new Mock<IUserServiceClient>();
        foreach (var signerId in _signerIds)
        {
            var index = _signerIds.IndexOf(signerId);
            userServiceClientMock.Setup(x => x.GetUserAsync(signerId, It.IsAny<string>()))
                .ReturnsAsync(new UserInfoDto { Id = signerId, Email = $"user{index}@test.com", FirstName = $"User{index}", LastName = "Test", Role = UserRole.CoOwner });
        }
        userServiceClientMock.Setup(x => x.GetUsersAsync(It.IsAny<List<Guid>>(), It.IsAny<string>()))
            .ReturnsAsync((List<Guid> userIds, string token) => userIds.ToDictionary(
                id => id,
                id => {
                    var index = _signerIds.IndexOf(id);
                    return new UserInfoDto { Id = id, Email = $"user{index}@test.com", FirstName = $"User{index}", LastName = "Test", Role = UserRole.CoOwner };
                }));

        // Mock IHttpContextAccessor
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = "Bearer test-token";
        httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);

        _documentService = new DocumentService(
            _context,
            _mockStorageService.Object,
            mockVirusScan.Object,
            mockSigningToken.Object,
            _mockCertificateService.Object,
            mockNotification.Object,
            _mockLogger.Object,
            userServiceClientMock.Object,
            httpContextAccessorMock.Object);
        SeedTestData();
    }

    private void SeedTestData()
    {
        var users = _signerIds.Select((id, index) => new User
        {
            Id = id,
            Email = $"user{index}@test.com",
            FirstName = $"User{index}",
            LastName = "Test",
            Phone = $"123456{index}",
            KycStatus = KycStatus.Approved,
            Role = UserRole.CoOwner
        }).ToList();

        var group = new OwnershipGroup
        {
            Id = _testGroupId,
            Name = "Integration Test Group",
            CreatedBy = _signerIds[0],
            Status = GroupStatus.Active
        };

        var members = _signerIds.Select((id, index) => new GroupMember
        {
            Id = Guid.NewGuid(),
            GroupId = _testGroupId,
            UserId = id,
            RoleInGroup = index == 0 ? GroupRole.Admin : GroupRole.Member,
            SharePercentage = 0.33m
        }).ToList();

        // Note: Users are no longer stored in GroupDbContext - they're fetched via HTTP
        _context.OwnershipGroups.Add(group);
        _context.GroupMembers.AddRange(members);
        _context.SaveChanges();
    }

    [Fact]
    public async Task CompleteWorkflow_ParallelSigning_ShouldSucceed()
    {
        // Arrange
        var document = CreateTestDocument();
        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        _mockStorageService
            .Setup(x => x.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("signature-key");

        _mockCertificateService
            .Setup(x => x.GenerateCertificateAsync(It.IsAny<Document>(), It.IsAny<List<DocumentSignature>>(), It.IsAny<string>()))
            .ReturnsAsync(new SigningCertificateResponse
            {
                DocumentId = Guid.NewGuid(),
                FileName = "certificate.pdf",
                CertificatePdf = new byte[100],
                CertificateId = Guid.NewGuid().ToString(),
                GeneratedAt = DateTime.UtcNow
            });

        // Act: Step 1 - Send for signing
        var sendRequest = new SendForSigningRequest
        {
            SignerIds = _signerIds.Take(2).ToList(),
            SigningMode = SigningMode.Parallel,
            DueDate = DateTime.UtcNow.AddDays(7),
            Message = "Please sign this document"
        };

        var sendResult = await _documentService.SendForSigningAsync(
            document.Id,
            sendRequest,
            _signerIds[0],
            "http://localhost");

        sendResult.SignatureStatus.Should().Be(SignatureStatus.SentForSigning);

        // Act: Step 2 - All signers sign (parallel)
        var signatures = await _context.DocumentSignatures
            .Where(s => s.DocumentId == document.Id)
            .ToListAsync();

        foreach (var signature in signatures)
        {
            var signRequest = new SignDocumentRequest
            {
                SigningToken = signature.SigningToken!,
                SignatureData = Convert.ToBase64String(new byte[100]),
                IpAddress = "127.0.0.1",
                DeviceInfo = "Test Device"
            };

            await _documentService.SignDocumentAsync(
                document.Id,
                signRequest,
                signature.SignerId,
                "127.0.0.1",
                "Test Agent");
        }

        // Assert: Step 3 - Verify document is fully signed
        var updatedDocument = await _context.Documents.FindAsync(document.Id);
        updatedDocument!.SignatureStatus.Should().Be(SignatureStatus.FullySigned);

        // Act: Step 4 - Generate certificate
        var certificate = await _documentService.GetSigningCertificateAsync(
            document.Id,
            _signerIds[0],
            "http://localhost");

        // Assert: Certificate generated
        certificate.Should().NotBeNull();
        certificate.CertificatePdf.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CompleteWorkflow_SequentialSigning_ShouldEnforceOrder()
    {
        // Arrange
        var document = CreateTestDocument();
        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        _mockStorageService
            .Setup(x => x.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("signature-key");

        // Act: Send for sequential signing
        var sendRequest = new SendForSigningRequest
        {
            SignerIds = _signerIds,
            SigningMode = SigningMode.Sequential,
            DueDate = DateTime.UtcNow.AddDays(7)
        };

        await _documentService.SendForSigningAsync(
            document.Id,
            sendRequest,
            _signerIds[0],
            "http://localhost");

        var signatures = await _context.DocumentSignatures
            .Where(s => s.DocumentId == document.Id)
            .OrderBy(s => s.SignatureOrder)
            .ToListAsync();

        // Act: Try to sign out of order (should fail)
        var thirdSignerToken = signatures[2].SigningToken!;
        var outOfOrderRequest = new SignDocumentRequest
        {
            SigningToken = thirdSignerToken,
            SignatureData = Convert.ToBase64String(new byte[100])
        };

        // Assert: Out of order signing should fail
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _documentService.SignDocumentAsync(
                document.Id,
                outOfOrderRequest,
                _signerIds[2],
                "127.0.0.1",
                null));

        // Act: Sign in correct order
        for (int i = 0; i < signatures.Count; i++)
        {
            var signRequest = new SignDocumentRequest
            {
                SigningToken = signatures[i].SigningToken!,
                SignatureData = Convert.ToBase64String(new byte[100]),
                IpAddress = "127.0.0.1"
            };

            await _documentService.SignDocumentAsync(
                document.Id,
                signRequest,
                signatures[i].SignerId,
                "127.0.0.1",
                null);

            // Check status after each signature
            var doc = await _context.Documents.FindAsync(document.Id);
            if (i < signatures.Count - 1)
            {
                doc!.SignatureStatus.Should().Be(SignatureStatus.PartiallySigned);
            }
            else
            {
                doc!.SignatureStatus.Should().Be(SignatureStatus.FullySigned);
            }
        }
    }

    [Fact]
    public async Task PartialSigningScenario_ShouldTrackProgress()
    {
        // Arrange
        var document = CreateTestDocument();
        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        _mockStorageService
            .Setup(x => x.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("signature-key");

        var sendRequest = new SendForSigningRequest
        {
            SignerIds = _signerIds,
            SigningMode = SigningMode.Parallel,
            DueDate = DateTime.UtcNow.AddDays(7)
        };

        await _documentService.SendForSigningAsync(
            document.Id,
            sendRequest,
            _signerIds[0],
            "http://localhost");

        // Act: Only first two signers sign
        var signatures = await _context.DocumentSignatures
            .Where(s => s.DocumentId == document.Id)
            .Take(2)
            .ToListAsync();

        foreach (var signature in signatures)
        {
            var signRequest = new SignDocumentRequest
            {
                SigningToken = signature.SigningToken!,
                SignatureData = Convert.ToBase64String(new byte[100])
            };

            await _documentService.SignDocumentAsync(
                document.Id,
                signRequest,
                signature.SignerId,
                "127.0.0.1",
                null);
        }

        // Assert: Check partial status
        var status = await _documentService.GetSignatureStatusAsync(document.Id, _signerIds[0]);

        status.Status.Should().Be(SignatureStatus.PartiallySigned);
        status.TotalSigners.Should().Be(3);
        status.SignedCount.Should().Be(2);
        status.ProgressPercentage.Should().BeApproximately(66.67, 0.1);
    }

    [Fact]
    public async Task SignatureExpiration_ShouldPreventSigning()
    {
        // Arrange
        var document = CreateTestDocument();
        document.SignatureStatus = SignatureStatus.SentForSigning;
        _context.Documents.Add(document);

        // Generate an expired token (expiration in the past)
        var expiredToken = GenerateTestToken(document.Id, _signerIds[0], -1); // -1 day = expired
        
        var signature = new DocumentSignature
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            SignerId = _signerIds[0],
            SignatureOrder = 1,
            Status = SignatureStatus.SentForSigning,
            SigningToken = expiredToken,
            TokenExpiresAt = DateTime.UtcNow.AddMinutes(-1), // Expired 1 minute ago
            SigningMode = SigningMode.Parallel
        };
        _context.DocumentSignatures.Add(signature);
        await _context.SaveChangesAsync();

        var signRequest = new SignDocumentRequest
        {
            SigningToken = signature.SigningToken!,
            SignatureData = Convert.ToBase64String(new byte[100])
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _documentService.SignDocumentAsync(
                document.Id,
                signRequest,
                _signerIds[0],
                "127.0.0.1",
                null));

        exception.Message.Should().Contain("expired");
    }

    [Fact]
    public async Task CertificateVerification_ShouldValidateCorrectly()
    {
        // Arrange
        var document = CreateTestDocument();
        document.SignatureStatus = SignatureStatus.FullySigned;
        _context.Documents.Add(document);

        var signature = new DocumentSignature
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            SignerId = _signerIds[0],
            Status = SignatureStatus.FullySigned,
            SignedAt = DateTime.UtcNow,
            SigningMode = SigningMode.Parallel
        };
        _context.DocumentSignatures.Add(signature);

        var certificateId = $"CERT-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}".Substring(0, 50);
        var certificate = new SigningCertificate
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            CertificateId = certificateId,
            DocumentHash = "test-hash-123",
            FileName = document.FileName,
            TotalSigners = 1,
            GeneratedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddYears(10),
            SignersJson = "[]",
            IsRevoked = false
        };
        _context.SigningCertificates.Add(certificate);
        await _context.SaveChangesAsync();

        // Act
        var verification = await _documentService.VerifyCertificateAsync(certificateId, "test-hash-123");

        // Assert
        verification.Should().NotBeNull();
        verification.IsValid.Should().BeTrue();
        verification.HashMatches.Should().BeTrue();
        verification.IsRevoked.Should().BeFalse();
        verification.IsExpired.Should().BeFalse();
    }

    [Fact]
    public async Task DuplicateSignature_ShouldBeRejected()
    {
        // Arrange
        var document = CreateTestDocument();
        document.SignatureStatus = SignatureStatus.PartiallySigned;
        _context.Documents.Add(document);

        var signature = new DocumentSignature
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            SignerId = _signerIds[0],
            SignatureOrder = 1,
            Status = SignatureStatus.FullySigned, // Already signed
            SignedAt = DateTime.UtcNow.AddHours(-1),
            SigningToken = GenerateTestToken(document.Id, _signerIds[0]),
            TokenExpiresAt = DateTime.UtcNow.AddDays(1),
            SigningMode = SigningMode.Parallel
        };
        _context.DocumentSignatures.Add(signature);
        await _context.SaveChangesAsync();

        var signRequest = new SignDocumentRequest
        {
            SigningToken = signature.SigningToken!,
            SignatureData = Convert.ToBase64String(new byte[100])
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _documentService.SignDocumentAsync(
                document.Id,
                signRequest,
                _signerIds[0],
                "127.0.0.1",
                null));
    }

    private Document CreateTestDocument()
    {
        return new Document
        {
            Id = Guid.NewGuid(),
            GroupId = _testGroupId,
            Type = DocumentType.OwnershipAgreement,
            FileName = "integration-test.pdf",
            StorageKey = "test-storage-key",
            ContentType = "application/pdf",
            FileSize = 2048,
            SignatureStatus = SignatureStatus.Draft,
            UploadedBy = _signerIds[0]
        };
    }

    private string GenerateTestToken(Guid documentId, Guid signerId, int expirationDays = 7)
    {
        var expiresAt = DateTime.UtcNow.AddDays(expirationDays);
        var expirationTimestamp = new DateTimeOffset(expiresAt).ToUnixTimeSeconds();
        var randomBytes = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
        var tokenData = $"SIGN|{documentId:N}|{signerId:N}|{expirationTimestamp}|{randomBytes}";
        var tokenBytes = System.Text.Encoding.UTF8.GetBytes(tokenData);
        return Convert.ToBase64String(tokenBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private (bool IsValid, Guid DocumentId, Guid SignerId) ValidateTestToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return (false, Guid.Empty, Guid.Empty);
        }

        try
        {
            // Convert from Base64URL back to standard Base64
            var base64 = token
                .Replace('-', '+')
                .Replace('_', '/');

            // Add padding if needed
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }

            // Decode
            var tokenBytes = Convert.FromBase64String(base64);
            var tokenData = System.Text.Encoding.UTF8.GetString(tokenBytes);

            // Parse: prefix|documentId|signerId|expirationTimestamp|randomBytes
            var parts = tokenData.Split('|');
            if (parts.Length != 5 || parts[0] != "SIGN")
            {
                return (false, Guid.Empty, Guid.Empty);
            }

            // Extract data
            if (!Guid.TryParseExact(parts[1], "N", out var documentId))
            {
                return (false, Guid.Empty, Guid.Empty);
            }

            if (!Guid.TryParseExact(parts[2], "N", out var signerId))
            {
                return (false, Guid.Empty, Guid.Empty);
            }

            if (!long.TryParse(parts[3], out var expirationTimestamp))
            {
                return (false, Guid.Empty, Guid.Empty);
            }

            // Check if expired
            var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expirationTimestamp).UtcDateTime;
            if (DateTime.UtcNow > expiresAt)
            {
                return (false, documentId, signerId);
            }

            return (true, documentId, signerId);
        }
        catch
        {
            return (false, Guid.Empty, Guid.Empty);
        }
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
