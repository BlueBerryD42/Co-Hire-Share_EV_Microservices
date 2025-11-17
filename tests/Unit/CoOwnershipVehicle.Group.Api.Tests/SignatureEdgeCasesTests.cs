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
/// Tests for edge cases and validation scenarios in signature workflow
/// </summary>
public class SignatureEdgeCasesTests : IDisposable
{
    private readonly GroupDbContext _context;
    private readonly Mock<IFileStorageService> _mockStorageService;
    private readonly Mock<ILogger<DocumentService>> _mockLogger;
    private readonly Mock<ICertificateGenerationService> _mockCertificateService;
    private readonly DocumentService _documentService;
    private readonly Guid _testGroupId = Guid.NewGuid();
    private readonly Guid _testUserId = Guid.NewGuid();

    public SignatureEdgeCasesTests()
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

        // Mock IUserServiceClient
        var userServiceClientMock = new Mock<IUserServiceClient>();
        userServiceClientMock.Setup(x => x.GetUserAsync(_testUserId, It.IsAny<string>()))
            .ReturnsAsync(new UserInfoDto { Id = _testUserId, Email = "test@test.com", FirstName = "Test", LastName = "User", Role = UserRole.CoOwner });
        userServiceClientMock.Setup(x => x.GetUsersAsync(It.IsAny<List<Guid>>(), It.IsAny<string>()))
            .ReturnsAsync((List<Guid> userIds, string token) => userIds.ToDictionary(
                id => id,
                id => new UserInfoDto { Id = id, Email = "test@test.com", FirstName = "Test", LastName = "User", Role = UserRole.CoOwner }));

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
        var user = new User
        {
            Id = _testUserId,
            Email = "test@test.com",
            FirstName = "Test",
            LastName = "User",
            Phone = "1234567890",
            KycStatus = KycStatus.Approved,
            Role = UserRole.CoOwner
        };

        var group = new OwnershipGroup
        {
            Id = _testGroupId,
            Name = "Test Group",
            CreatedBy = _testUserId,
            Status = GroupStatus.Active
        };

        var member = new GroupMember
        {
            Id = Guid.NewGuid(),
            GroupId = _testGroupId,
            UserId = _testUserId,
            RoleInGroup = GroupRole.Admin,
            SharePercentage = 1.0m
        };

        // Note: Users are no longer stored in GroupDbContext - they're fetched via HTTP
        _context.OwnershipGroups.Add(group);
        _context.GroupMembers.Add(member);
        _context.SaveChanges();
    }

    [Fact]
    public async Task SendForSigning_WithEmptySignerList_ShouldThrowException()
    {
        // Arrange
        var document = CreateTestDocument();
        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        var request = new SendForSigningRequest
        {
            SignerIds = new List<Guid>(),
            SigningMode = SigningMode.Parallel
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _documentService.SendForSigningAsync(document.Id, request, _testUserId, "http://localhost"));
    }

    [Fact]
    public async Task SendForSigning_WithNonExistentSigner_ShouldThrowException()
    {
        // Arrange
        var document = CreateTestDocument();
        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        var nonExistentUserId = Guid.NewGuid();
        var request = new SendForSigningRequest
        {
            SignerIds = new List<Guid> { nonExistentUserId },
            SigningMode = SigningMode.Parallel
        };

        // Act & Assert
        // Service validates group membership first, so non-existent signer will throw ArgumentException
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _documentService.SendForSigningAsync(document.Id, request, _testUserId, "http://localhost"));
    }

    [Fact]
    public async Task SignDocument_WithInvalidToken_ShouldThrowException()
    {
        // Arrange
        var document = CreateTestDocument();
        document.SignatureStatus = SignatureStatus.SentForSigning;
        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        var invalidRequest = new SignDocumentRequest
        {
            SigningToken = "invalid-token-12345",
            SignatureData = Convert.ToBase64String(new byte[100])
        };

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _documentService.SignDocumentAsync(document.Id, invalidRequest, _testUserId, "127.0.0.1", null));
    }

    [Fact]
    public async Task SignDocument_WithWrongDocument_ShouldThrowException()
    {
        // Arrange
        var document1 = CreateTestDocument();
        var document2 = CreateTestDocument();
        _context.Documents.AddRange(document1, document2);

        var signature = new DocumentSignature
        {
            Id = Guid.NewGuid(),
            DocumentId = document1.Id,
            SignerId = _testUserId,
            SignatureOrder = 1,
            Status = SignatureStatus.SentForSigning,
            SigningToken = GenerateTestToken(document1.Id, _testUserId),
            TokenExpiresAt = DateTime.UtcNow.AddDays(1),
            SigningMode = SigningMode.Parallel
        };
        _context.DocumentSignatures.Add(signature);
        await _context.SaveChangesAsync();

        var request = new SignDocumentRequest
        {
            SigningToken = signature.SigningToken!,
            SignatureData = Convert.ToBase64String(new byte[100])
        };

        // Act & Assert: Try to use document1's token on document2
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _documentService.SignDocumentAsync(document2.Id, request, _testUserId, "127.0.0.1", null));
    }

    [Fact]
    public async Task SignDocument_WithEmptySignatureData_ShouldThrowException()
    {
        // Arrange
        var document = CreateTestDocument();
        document.SignatureStatus = SignatureStatus.SentForSigning;
        _context.Documents.Add(document);

        var signature = new DocumentSignature
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            SignerId = _testUserId,
            SignatureOrder = 1,
            Status = SignatureStatus.SentForSigning,
            SigningToken = GenerateTestToken(document.Id, _testUserId),
            TokenExpiresAt = DateTime.UtcNow.AddDays(1),
            SigningMode = SigningMode.Parallel
        };
        _context.DocumentSignatures.Add(signature);
        await _context.SaveChangesAsync();

        var request = new SignDocumentRequest
        {
            SigningToken = signature.SigningToken!,
            SignatureData = string.Empty // Empty signature
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _documentService.SignDocumentAsync(document.Id, request, _testUserId, "127.0.0.1", null));
    }

    [Fact]
    public async Task SignDocument_AlreadySigned_ShouldThrowException()
    {
        // Arrange
        var document = CreateTestDocument();
        document.SignatureStatus = SignatureStatus.FullySigned;
        _context.Documents.Add(document);

        var signature = new DocumentSignature
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            SignerId = _testUserId,
            SignatureOrder = 1,
            Status = SignatureStatus.FullySigned, // Already signed
            SignedAt = DateTime.UtcNow.AddHours(-1),
            SigningToken = GenerateTestToken(document.Id, _testUserId),
            TokenExpiresAt = DateTime.UtcNow.AddDays(1),
            SigningMode = SigningMode.Parallel
        };
        _context.DocumentSignatures.Add(signature);
        await _context.SaveChangesAsync();

        var request = new SignDocumentRequest
        {
            SigningToken = signature.SigningToken!,
            SignatureData = Convert.ToBase64String(new byte[100])
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _documentService.SignDocumentAsync(document.Id, request, _testUserId, "127.0.0.1", null));
    }

    [Fact]
    public async Task Sequential_SignOutOfOrder_ShouldThrowException()
    {
        // Arrange
        var document = CreateTestDocument();
        document.SignatureStatus = SignatureStatus.SentForSigning;
        _context.Documents.Add(document);

        var user2Id = Guid.NewGuid();
        // Note: Users are no longer stored in GroupDbContext - they're fetched via HTTP
        // The userServiceClientMock will handle fetching user2Id when needed

        var member2 = new GroupMember
        {
            Id = Guid.NewGuid(),
            GroupId = _testGroupId,
            UserId = user2Id,
            RoleInGroup = GroupRole.Member,
            SharePercentage = 0.5m
        };
        _context.GroupMembers.Add(member2);

        var signature1 = new DocumentSignature
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            SignerId = _testUserId,
            SignatureOrder = 1,
            Status = SignatureStatus.SentForSigning, // Not signed yet
            SigningToken = GenerateTestToken(document.Id, _testUserId),
            TokenExpiresAt = DateTime.UtcNow.AddDays(1),
            SigningMode = SigningMode.Sequential
        };

        var signature2 = new DocumentSignature
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            SignerId = user2Id,
            SignatureOrder = 2,
            Status = SignatureStatus.SentForSigning,
            SigningToken = GenerateTestToken(document.Id, user2Id),
            TokenExpiresAt = DateTime.UtcNow.AddDays(1),
            SigningMode = SigningMode.Sequential
        };

        _context.DocumentSignatures.AddRange(signature1, signature2);
        await _context.SaveChangesAsync();

        var request = new SignDocumentRequest
        {
            SigningToken = signature2.SigningToken!,
            SignatureData = Convert.ToBase64String(new byte[100])
        };

        _mockStorageService
            .Setup(x => x.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("test-key");

        // Act & Assert: User2 tries to sign before User1
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _documentService.SignDocumentAsync(document.Id, request, user2Id, "127.0.0.1", null));

        exception.Message.Should().Contain("Previous signers");
    }

    [Fact]
    public async Task CertificateVerification_RevokedCertificate_ShouldReturnInvalid()
    {
        // Arrange
        var document = CreateTestDocument();
        _context.Documents.Add(document);

        var certificateId = $"CERT-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}".Substring(0, 50);
        var certificate = new SigningCertificate
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            CertificateId = certificateId,
            DocumentHash = "test-hash",
            FileName = document.FileName,
            TotalSigners = 1,
            GeneratedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddYears(10),
            SignersJson = "[]",
            IsRevoked = true,
            RevocationReason = "Document was updated"
        };
        _context.SigningCertificates.Add(certificate);
        await _context.SaveChangesAsync();

        // Act
        var result = await _documentService.VerifyCertificateAsync(certificateId);

        // Assert
        result.IsValid.Should().BeFalse();
        result.IsRevoked.Should().BeTrue();
        result.RevocationReason.Should().Be("Document was updated");
    }

    [Fact]
    public async Task CertificateVerification_ExpiredCertificate_ShouldReturnInvalid()
    {
        // Arrange
        var document = CreateTestDocument();
        _context.Documents.Add(document);

        var certificateId = $"CERT-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}".Substring(0, 50);
        var certificate = new SigningCertificate
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            CertificateId = certificateId,
            DocumentHash = "test-hash",
            FileName = document.FileName,
            TotalSigners = 1,
            GeneratedAt = DateTime.UtcNow.AddYears(-11),
            ExpiresAt = DateTime.UtcNow.AddDays(-1), // Expired yesterday
            SignersJson = "[]",
            IsRevoked = false
        };
        _context.SigningCertificates.Add(certificate);
        await _context.SaveChangesAsync();

        // Act
        var result = await _documentService.VerifyCertificateAsync(certificateId);

        // Assert
        result.IsValid.Should().BeFalse();
        result.IsExpired.Should().BeTrue();
    }

    [Fact]
    public async Task CertificateVerification_InvalidHash_ShouldReturnHashMismatch()
    {
        // Arrange
        var document = CreateTestDocument();
        _context.Documents.Add(document);

        var certificateId = $"CERT-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}".Substring(0, 50);
        var certificate = new SigningCertificate
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            CertificateId = certificateId,
            DocumentHash = "correct-hash-123",
            FileName = document.FileName,
            TotalSigners = 1,
            GeneratedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddYears(10),
            SignersJson = "[]",
            IsRevoked = false
        };
        _context.SigningCertificates.Add(certificate);
        await _context.SaveChangesAsync();

        // Act: Provide wrong hash
        var result = await _documentService.VerifyCertificateAsync(certificateId, "wrong-hash-456");

        // Assert
        result.IsValid.Should().BeFalse();
        result.HashMatches.Should().BeFalse();
    }

    [Fact]
    public async Task CertificateVerification_NonExistentCertificate_ShouldThrowException()
    {
        // Arrange
        var nonExistentCertId = "CERT-NONEXISTENT-123";

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _documentService.VerifyCertificateAsync(nonExistentCertId));
    }

    [Fact]
    public async Task GetSignatureStatus_NonExistentDocument_ShouldThrowException()
    {
        // Arrange
        var nonExistentDocId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _documentService.GetSignatureStatusAsync(nonExistentDocId, _testUserId));
    }

    [Fact]
    public async Task GetSignatureStatus_UnauthorizedUser_ShouldThrowException()
    {
        // Arrange
        var document = CreateTestDocument();
        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        var unauthorizedUserId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _documentService.GetSignatureStatusAsync(document.Id, unauthorizedUserId));
    }

    private Document CreateTestDocument()
    {
        return new Document
        {
            Id = Guid.NewGuid(),
            GroupId = _testGroupId,
            Type = DocumentType.OwnershipAgreement,
            FileName = "edge-case-test.pdf",
            StorageKey = "test-key",
            ContentType = "application/pdf",
            FileSize = 1024,
            SignatureStatus = SignatureStatus.Draft,
            UploadedBy = _testUserId
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
