using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Group.Api.Data;
using CoOwnershipVehicle.Group.Api.DTOs;
using CoOwnershipVehicle.Group.Api.Services.Implementations;
using CoOwnershipVehicle.Group.Api.Services.Interfaces;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CoOwnershipVehicle.Group.Api.Tests;

/// <summary>
/// Unit tests for document signature endpoints
/// Tests signature creation, validation, and status tracking
/// </summary>
public class DocumentSignatureTests : IDisposable
{
    private readonly GroupDbContext _context;
    private readonly Mock<IStorageService> _mockStorageService;
    private readonly Mock<ILogger<DocumentService>> _mockLogger;
    private readonly Mock<ICertificateGenerationService> _mockCertificateService;
    private readonly DocumentService _documentService;
    private readonly Guid _testGroupId = Guid.NewGuid();
    private readonly Guid _testUserId = Guid.NewGuid();
    private readonly Guid _testUser2Id = Guid.NewGuid();

    public DocumentSignatureTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<GroupDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new GroupDbContext(options);

        // Setup mocks
        _mockStorageService = new Mock<IStorageService>();
        _mockLogger = new Mock<ILogger<DocumentService>>();
        _mockCertificateService = new Mock<ICertificateGenerationService>();

        _documentService = new DocumentService(
            _context,
            _mockStorageService.Object,
            _mockLogger.Object,
            _mockCertificateService.Object);

        // Seed test data
        SeedTestData();
    }

    private void SeedTestData()
    {
        // Create test users
        var user1 = new User
        {
            Id = _testUserId,
            Email = "user1@test.com",
            FirstName = "Test",
            LastName = "User1",
            Phone = "1234567890",
            KycStatus = KycStatus.Approved,
            Role = UserRole.CoOwner
        };

        var user2 = new User
        {
            Id = _testUser2Id,
            Email = "user2@test.com",
            FirstName = "Test",
            LastName = "User2",
            Phone = "0987654321",
            KycStatus = KycStatus.Approved,
            Role = UserRole.CoOwner
        };

        // Create test group
        var group = new OwnershipGroup
        {
            Id = _testGroupId,
            Name = "Test Group",
            CreatedBy = _testUserId,
            Status = GroupStatus.Active
        };

        // Create group members
        var member1 = new GroupMember
        {
            Id = Guid.NewGuid(),
            GroupId = _testGroupId,
            UserId = _testUserId,
            RoleInGroup = GroupRole.Admin,
            SharePercentage = 0.5m
        };

        var member2 = new GroupMember
        {
            Id = Guid.NewGuid(),
            GroupId = _testGroupId,
            UserId = _testUser2Id,
            RoleInGroup = GroupRole.Member,
            SharePercentage = 0.5m
        };

        _context.Users.AddRange(user1, user2);
        _context.OwnershipGroups.Add(group);
        _context.GroupMembers.AddRange(member1, member2);
        _context.SaveChanges();
    }

    [Fact]
    public async Task SendForSigning_WithValidData_ShouldCreateSignatureRecords()
    {
        // Arrange
        var document = CreateTestDocument();
        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        var request = new SendForSigningRequest
        {
            SignerIds = new List<Guid> { _testUserId, _testUser2Id },
            SigningMode = SigningMode.Parallel,
            DueDate = DateTime.UtcNow.AddDays(7),
            Message = "Please sign this document"
        };

        // Act
        var result = await _documentService.SendForSigningAsync(
            document.Id,
            request,
            _testUserId,
            "http://localhost:5000");

        // Assert
        result.Should().NotBeNull();
        result.SignatureIds.Should().HaveCount(2);
        result.SigningMode.Should().Be(SigningMode.Parallel);
        result.DocumentStatus.Should().Be(SignatureStatus.SentForSigning);

        var signatures = await _context.DocumentSignatures
            .Where(s => s.DocumentId == document.Id)
            .ToListAsync();

        signatures.Should().HaveCount(2);
        signatures.All(s => !string.IsNullOrEmpty(s.SigningToken)).Should().BeTrue();
        signatures.All(s => s.TokenExpiresAt > DateTime.UtcNow).Should().BeTrue();
    }

    [Fact]
    public async Task SendForSigning_WithSequentialMode_ShouldSetCorrectOrder()
    {
        // Arrange
        var document = CreateTestDocument();
        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        var request = new SendForSigningRequest
        {
            SignerIds = new List<Guid> { _testUserId, _testUser2Id },
            SigningMode = SigningMode.Sequential,
            DueDate = DateTime.UtcNow.AddDays(7)
        };

        // Act
        var result = await _documentService.SendForSigningAsync(
            document.Id,
            request,
            _testUserId,
            "http://localhost:5000");

        // Assert
        var signatures = await _context.DocumentSignatures
            .Where(s => s.DocumentId == document.Id)
            .OrderBy(s => s.SignatureOrder)
            .ToListAsync();

        signatures[0].SignatureOrder.Should().Be(1);
        signatures[1].SignatureOrder.Should().Be(2);
    }

    [Fact]
    public async Task SendForSigning_WithNonMember_ShouldThrowUnauthorizedException()
    {
        // Arrange
        var document = CreateTestDocument();
        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        var nonMemberId = Guid.NewGuid();
        var request = new SendForSigningRequest
        {
            SignerIds = new List<Guid> { _testUserId },
            SigningMode = SigningMode.Parallel
        };

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _documentService.SendForSigningAsync(document.Id, request, nonMemberId, "http://localhost"));
    }

    [Fact]
    public async Task SignDocument_WithValidToken_ShouldSucceed()
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

        var signRequest = new SignDocumentRequest
        {
            SigningToken = signature.SigningToken!,
            SignatureData = Convert.ToBase64String(new byte[100]),
            IpAddress = "127.0.0.1",
            DeviceInfo = "Test Device"
        };

        _mockStorageService
            .Setup(x => x.UploadFileAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new StorageUploadResult { Success = true, StorageKey = "test-key" });

        // Act
        var result = await _documentService.SignDocumentAsync(
            document.Id,
            signRequest,
            _testUserId,
            "127.0.0.1",
            "Test Agent");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.SignedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        var updatedSignature = await _context.DocumentSignatures.FindAsync(signature.Id);
        updatedSignature!.Status.Should().Be(SignatureStatus.PartiallySigned);
        updatedSignature.SignedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SignDocument_WithExpiredToken_ShouldThrowException()
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
            TokenExpiresAt = DateTime.UtcNow.AddDays(-1), // Expired
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
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _documentService.SignDocumentAsync(document.Id, signRequest, _testUserId, "127.0.0.1", null));
    }

    [Fact]
    public async Task GetSignatureStatus_ShouldReturnCorrectInformation()
    {
        // Arrange
        var document = CreateTestDocument();
        document.SignatureStatus = SignatureStatus.PartiallySigned;
        _context.Documents.Add(document);

        var signature1 = new DocumentSignature
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            SignerId = _testUserId,
            SignatureOrder = 1,
            Status = SignatureStatus.FullySigned,
            SignedAt = DateTime.UtcNow.AddHours(-1),
            SigningMode = SigningMode.Sequential
        };

        var signature2 = new DocumentSignature
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            SignerId = _testUser2Id,
            SignatureOrder = 2,
            Status = SignatureStatus.SentForSigning,
            SigningMode = SigningMode.Sequential
        };

        _context.DocumentSignatures.AddRange(signature1, signature2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _documentService.GetSignatureStatusAsync(document.Id, _testUserId);

        // Assert
        result.Should().NotBeNull();
        result.DocumentId.Should().Be(document.Id);
        result.Status.Should().Be(SignatureStatus.PartiallySigned);
        result.SigningMode.Should().Be(SigningMode.Sequential);
        result.TotalSigners.Should().Be(2);
        result.CompletedSignatures.Should().Be(1);
        result.PendingSignatures.Should().Be(1);
        result.Signatures.Should().HaveCount(2);
    }

    [Fact]
    public async Task GenerateCertificate_WithPartiallySignedDocument_ShouldThrowException()
    {
        // Arrange
        var document = CreateTestDocument();
        document.SignatureStatus = SignatureStatus.PartiallySigned;
        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _documentService.GetSigningCertificateAsync(document.Id, _testUserId));
    }

    [Fact]
    public async Task GenerateCertificate_WithFullySignedDocument_ShouldSucceed()
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
            Status = SignatureStatus.FullySigned,
            SignedAt = DateTime.UtcNow,
            SigningMode = SigningMode.Parallel
        };
        _context.DocumentSignatures.Add(signature);
        await _context.SaveChangesAsync();

        _mockCertificateService
            .Setup(x => x.GenerateCertificateAsync(It.IsAny<Document>(), It.IsAny<List<DocumentSignature>>(), It.IsAny<string>()))
            .ReturnsAsync(new byte[100]);

        // Act
        var result = await _documentService.GetSigningCertificateAsync(document.Id, _testUserId, "http://localhost");

        // Assert
        result.Should().NotBeNull();
        result.CertificateData.Should().NotBeEmpty();
        result.FileName.Should().Contain("certificate");
    }

    private Document CreateTestDocument()
    {
        return new Document
        {
            Id = Guid.NewGuid(),
            GroupId = _testGroupId,
            Type = DocumentType.OwnershipAgreement,
            FileName = "test-document.pdf",
            StorageKey = "test-key",
            ContentType = "application/pdf",
            FileSize = 1024,
            SignatureStatus = SignatureStatus.Draft,
            UploadedBy = _testUserId
        };
    }

    private string GenerateTestToken(Guid documentId, Guid signerId)
    {
        var tokenData = $"SIGN|{documentId:N}|{signerId:N}|{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}|{Guid.NewGuid():N}";
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(tokenData));
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
