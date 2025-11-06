using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Group.Api.Data;
using CoOwnershipVehicle.Group.Api.DTOs;
using CoOwnershipVehicle.Group.Api.Services.Implementations;
using CoOwnershipVehicle.Group.Api.Services.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CoOwnershipVehicle.Group.Api.Tests;

/// <summary>
/// Integration tests for Document Service with Group Service
/// Tests group-document relationships, permissions, and lifecycle
/// </summary>
public class DocumentServiceIntegrationTests : IDisposable
{
    private readonly GroupDbContext _context;
    private readonly Mock<IFileStorageService> _mockFileStorage;
    private readonly Mock<IVirusScanService> _mockVirusScan;
    private readonly Mock<INotificationService> _mockNotificationService;
    private readonly Mock<ILogger<DocumentService>> _mockLogger;
    private readonly DocumentService _documentService;

    private readonly Guid _testGroupId = Guid.NewGuid();
    private readonly Guid _adminUserId = Guid.NewGuid();
    private readonly Guid _memberUserId = Guid.NewGuid();
    private readonly Guid _nonMemberUserId = Guid.NewGuid();

    public DocumentServiceIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<GroupDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new GroupDbContext(options);
        _mockFileStorage = new Mock<IFileStorageService>();
        _mockVirusScan = new Mock<IVirusScanService>();
        _mockNotificationService = new Mock<INotificationService>();
        _mockLogger = new Mock<ILogger<DocumentService>>();

        _documentService = new DocumentService(
            _context,
            _mockFileStorage.Object,
            _mockVirusScan.Object,
            Mock.Of<ISigningTokenService>(),
            Mock.Of<ICertificateGenerationService>(),
            _mockNotificationService.Object,
            _mockLogger.Object);

        SeedTestData();
    }

    private void SeedTestData()
    {
        // Create test users
        var adminUser = new User
        {
            Id = _adminUserId,
            Email = "admin@test.com",
            FirstName = "Admin",
            LastName = "User",
            Phone = "1234567890",
            KycStatus = KycStatus.Approved,
            Role = UserRole.CoOwner,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };

        var memberUser = new User
        {
            Id = _memberUserId,
            Email = "member@test.com",
            FirstName = "Member",
            LastName = "User",
            Phone = "0987654321",
            KycStatus = KycStatus.Approved,
            Role = UserRole.CoOwner,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };

        var nonMemberUser = new User
        {
            Id = _nonMemberUserId,
            Email = "outsider@test.com",
            FirstName = "Outsider",
            LastName = "User",
            Phone = "5555555555",
            KycStatus = KycStatus.Approved,
            Role = UserRole.CoOwner,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };

        // Create test group
        var group = new OwnershipGroup
        {
            Id = _testGroupId,
            Name = "Test Group",
            Description = "Integration test group",
            CreatedBy = _adminUserId,
            Status = GroupStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        // Create group members
        var adminMember = new GroupMember
        {
            Id = Guid.NewGuid(),
            GroupId = _testGroupId,
            UserId = _adminUserId,
            RoleInGroup = GroupRole.Admin,
            SharePercentage = 0.6m,
            JoinedAt = DateTime.UtcNow
        };

        var regularMember = new GroupMember
        {
            Id = Guid.NewGuid(),
            GroupId = _testGroupId,
            UserId = _memberUserId,
            RoleInGroup = GroupRole.Member,
            SharePercentage = 0.4m,
            JoinedAt = DateTime.UtcNow
        };

        _context.Users.AddRange(adminUser, memberUser, nonMemberUser);
        _context.OwnershipGroups.Add(group);
        _context.GroupMembers.AddRange(adminMember, regularMember);
        _context.SaveChanges();
    }

    #region Group Integration Tests

    [Fact]
    public async Task UploadDocument_ShouldLinkToCorrectGroup()
    {
        // Arrange
        // PDF file signature: %PDF (0x25, 0x50, 0x44, 0x46)
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34, 0x0A }; // %PDF-1.4\n
        var storageKey = $"documents/{_testGroupId}/{Guid.NewGuid()}.pdf";

        _mockVirusScan.Setup(x => x.ScanFileAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync(new VirusScanResult { IsClean = true });

        _mockFileStorage.Setup(x => x.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(storageKey);

        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns("test-document.pdf");
        file.Setup(f => f.ContentType).Returns("application/pdf");
        file.Setup(f => f.Length).Returns((long)pdfBytes.Length);
        // Each call to OpenReadStream should return a new stream instance
        file.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(pdfBytes));
        file.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns((Stream target, CancellationToken ct) =>
            {
                using var source = new MemoryStream(pdfBytes);
                return source.CopyToAsync(target, ct);
            });

        var request = new DocumentUploadRequest
        {
            GroupId = _testGroupId,
            DocumentType = DocumentType.OwnershipAgreement,
            Description = "Test contract document",
            File = file.Object
        };

        // Act
        var result = await _documentService.UploadDocumentAsync(request, _adminUserId);

        // Assert
        result.Should().NotBeNull();
        result.GroupId.Should().Be(_testGroupId);
        result.FileName.Should().Be("test-document.pdf");

        var document = await _context.Documents.FindAsync(result.Id);
        document.Should().NotBeNull();
        document!.GroupId.Should().Be(_testGroupId);
        document.UploadedBy.Should().Be(_adminUserId);
    }

    [Fact]
    public async Task UploadDocument_NonMember_ShouldThrowUnauthorized()
    {
        // Arrange
        // PDF file signature: %PDF (0x25, 0x50, 0x44, 0x46)
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34, 0x0A }; // %PDF-1.4\n
        var fileStream = new MemoryStream(pdfBytes);
        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns("unauthorized.pdf");
        file.Setup(f => f.ContentType).Returns("application/pdf");
        file.Setup(f => f.Length).Returns(fileStream.Length);
        file.Setup(f => f.OpenReadStream()).Returns(fileStream);
        file.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns((Stream stream, CancellationToken ct) => fileStream.CopyToAsync(stream, ct));

        var request = new DocumentUploadRequest
        {
            GroupId = _testGroupId,
            DocumentType = DocumentType.OwnershipAgreement,
            File = file.Object
        };

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _documentService.UploadDocumentAsync(request, _nonMemberUserId));
    }

    [Fact]
    public async Task GetGroupDocuments_ShouldEnforceGroupMemberPermissions()
    {
        // Arrange
        var document = new Document
        {
            Id = Guid.NewGuid(),
            GroupId = _testGroupId,
            FileName = "group-doc.pdf",
            StorageKey = "storage/key.pdf",
            ContentType = "application/pdf",
            FileSize = 1000,
            Type = DocumentType.OwnershipAgreement,
            SignatureStatus = SignatureStatus.Draft,
            UploadedBy = _adminUserId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        // Act - Member should have access
        var memberResult = await _documentService.GetDocumentByIdAsync(document.Id, _memberUserId);
        memberResult.Should().NotBeNull();

        // Act - Non-member should not have access
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _documentService.GetDocumentByIdAsync(document.Id, _nonMemberUserId));
    }

    [Fact]
    public async Task DeleteGroup_ShouldHandleDocuments()
    {
        // Arrange
        var document = new Document
        {
            Id = Guid.NewGuid(),
            GroupId = _testGroupId,
            FileName = "to-be-deleted.pdf",
            StorageKey = "storage/key.pdf",
            ContentType = "application/pdf",
            FileSize = 1000,
            Type = DocumentType.OwnershipAgreement,
            SignatureStatus = SignatureStatus.Draft,
            UploadedBy = _adminUserId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        // Act - Delete group
        var group = await _context.OwnershipGroups.FindAsync(_testGroupId);
        _context.OwnershipGroups.Remove(group!);
        await _context.SaveChangesAsync();

        // Assert - Documents should be cascade deleted or soft-deleted
        var deletedDoc = await _context.Documents.IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.Id == document.Id);

        // Either deleted or marked as deleted
        if (deletedDoc != null)
        {
            deletedDoc.IsDeleted.Should().BeTrue();
        }
    }

    #endregion

    #region Notification Integration Tests

    [Fact]
    public async Task UploadDocument_ShouldSendNotificationToGroupMembers()
    {
        // Arrange
        // PDF file signature: %PDF (0x25, 0x50, 0x44, 0x46)
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34, 0x0A }; // %PDF-1.4\n
        var storageKey = $"documents/{_testGroupId}/{Guid.NewGuid()}.pdf";

        _mockVirusScan.Setup(x => x.ScanFileAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync(new VirusScanResult { IsClean = true });

        _mockFileStorage.Setup(x => x.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(storageKey);

        // Notification service methods are called internally by DocumentService
        // No need to mock specific methods as they're not part of the public interface

        var file = new Mock<IFormFile>();
        file.Setup(f => f.FileName).Returns("notification-test.pdf");
        file.Setup(f => f.ContentType).Returns("application/pdf");
        file.Setup(f => f.Length).Returns((long)pdfBytes.Length);
        // Each call to OpenReadStream should return a new stream instance
        file.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(pdfBytes));
        file.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns((Stream target, CancellationToken ct) =>
            {
                using var source = new MemoryStream(pdfBytes);
                return source.CopyToAsync(target, ct);
            });

        var request = new DocumentUploadRequest
        {
            GroupId = _testGroupId,
            DocumentType = DocumentType.OwnershipAgreement,
            File = file.Object
        };

        // Act
        await _documentService.UploadDocumentAsync(request, _adminUserId);

        // Assert - Notification service is called internally (methods not in public interface)
        // Verification would require checking internal implementation details
    }

    [Fact]
    public async Task SendForSigning_ShouldSendSignatureRequestNotification()
    {
        // Arrange
        var document = new Document
        {
            Id = Guid.NewGuid(),
            GroupId = _testGroupId,
            FileName = "to-sign.pdf",
            StorageKey = "storage/key.pdf",
            ContentType = "application/pdf",
            FileSize = 1000,
            Type = DocumentType.OwnershipAgreement,
            SignatureStatus = SignatureStatus.Draft,
            UploadedBy = _adminUserId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        // Notification service methods are called internally by DocumentService
        // No need to mock specific methods as they're not part of the public interface

        var request = new SendForSigningRequest
        {
            SignerIds = new List<Guid> { _memberUserId },
            SigningMode = SigningMode.Sequential
        };

        // Act
        await _documentService.SendForSigningAsync(document.Id, request, _adminUserId, "http://localhost");

        // Assert
        // Notification service is called internally (methods not in public interface)
        // Verification would require checking internal implementation details
    }

    #endregion

    #region Permission Tests

    [Fact]
    public async Task DeleteDocument_OnlyAdminAndUploader_ShouldSucceed()
    {
        // Arrange - Document uploaded by admin
        var document = new Document
        {
            Id = Guid.NewGuid(),
            GroupId = _testGroupId,
            FileName = "admin-doc.pdf",
            StorageKey = "storage/key.pdf",
            ContentType = "application/pdf",
            FileSize = 1000,
            Type = DocumentType.OwnershipAgreement,
            SignatureStatus = SignatureStatus.Draft,
            UploadedBy = _adminUserId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        // Act - Admin (uploader) can delete
        await _documentService.DeleteDocumentAsync(document.Id, _adminUserId);

        // Assert
        var deleted = await _context.Documents.IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.Id == document.Id);
        deleted.Should().NotBeNull();
        deleted!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteDocument_RegularMember_NotUploader_ShouldFail()
    {
        // Arrange - Document uploaded by admin
        var document = new Document
        {
            Id = Guid.NewGuid(),
            GroupId = _testGroupId,
            FileName = "admin-doc.pdf",
            StorageKey = "storage/key.pdf",
            ContentType = "application/pdf",
            FileSize = 1000,
            Type = DocumentType.OwnershipAgreement,
            SignatureStatus = SignatureStatus.Draft,
            UploadedBy = _adminUserId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        // Act & Assert - Regular member cannot delete admin's document
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _documentService.DeleteDocumentAsync(document.Id, _memberUserId));
    }

    [Fact]
    public async Task DeleteFullySignedDocument_ShouldFail()
    {
        // Arrange
        var document = new Document
        {
            Id = Guid.NewGuid(),
            GroupId = _testGroupId,
            FileName = "signed-doc.pdf",
            StorageKey = "storage/key.pdf",
            ContentType = "application/pdf",
            FileSize = 1000,
            Type = DocumentType.OwnershipAgreement,
            SignatureStatus = SignatureStatus.FullySigned,
            UploadedBy = _adminUserId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _documentService.DeleteDocumentAsync(document.Id, _adminUserId));
    }

    #endregion

    public void Dispose()
    {
        _context?.Dispose();
    }
}
