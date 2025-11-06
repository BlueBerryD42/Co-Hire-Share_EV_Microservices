using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Group.Api.Data;
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
/// Tests for document version control functionality
/// Tests version upload, retrieval, and download
/// </summary>
public class DocumentVersionControlTests : IDisposable
{
    private readonly GroupDbContext _context;
    private readonly Mock<IFileStorageService> _mockStorageService;
    private readonly Mock<ILogger<DocumentService>> _mockLogger;
    private readonly Mock<ICertificateGenerationService> _mockCertificateService;
    private readonly DocumentService _documentService;
    private readonly Guid _testGroupId = Guid.NewGuid();
    private readonly Guid _adminUserId = Guid.NewGuid();
    private readonly Guid _memberUserId = Guid.NewGuid();

    public DocumentVersionControlTests()
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

        _documentService = new DocumentService(
            _context,
            _mockStorageService.Object,
            mockVirusScan.Object,
            mockSigningToken.Object,
            _mockCertificateService.Object,
            mockNotification.Object,
            _mockLogger.Object);

        SeedTestData();
    }

    private void SeedTestData()
    {
        var adminUser = new User
        {
            Id = _adminUserId,
            Email = "admin@test.com",
            FirstName = "Admin",
            LastName = "User",
            Phone = "1234567890",
            KycStatus = KycStatus.Approved,
            Role = UserRole.CoOwner
        };

        var memberUser = new User
        {
            Id = _memberUserId,
            Email = "member@test.com",
            FirstName = "Member",
            LastName = "User",
            Phone = "0987654321",
            KycStatus = KycStatus.Approved,
            Role = UserRole.CoOwner
        };

        var group = new OwnershipGroup
        {
            Id = _testGroupId,
            Name = "Version Test Group",
            CreatedBy = _adminUserId,
            Status = GroupStatus.Active
        };

        var adminMember = new GroupMember
        {
            Id = Guid.NewGuid(),
            GroupId = _testGroupId,
            UserId = _adminUserId,
            RoleInGroup = GroupRole.Admin,
            SharePercentage = 0.6m
        };

        var regularMember = new GroupMember
        {
            Id = Guid.NewGuid(),
            GroupId = _testGroupId,
            UserId = _memberUserId,
            RoleInGroup = GroupRole.Member,
            SharePercentage = 0.4m
        };

        _context.Users.AddRange(adminUser, memberUser);
        _context.OwnershipGroups.Add(group);
        _context.GroupMembers.AddRange(adminMember, regularMember);
        _context.SaveChanges();
    }

    [Fact]
    public async Task UploadNewVersion_ByAdmin_ShouldSucceed()
    {
        // Arrange
        var document = CreateTestDocument();
        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        var file = CreateMockFile("updated-document.pdf", 2048);

        _mockStorageService
            .Setup(x => x.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("new-version-key");

        // Act
        var result = await _documentService.UploadNewVersionAsync(
            document.Id,
            file.Object,
            "Updated contract terms",
            _adminUserId);

        // Assert
        result.Should().NotBeNull();
        result.VersionNumber.Should().Be(1);
        result.IsCurrent.Should().BeTrue();
        result.ChangeDescription.Should().Be("Updated contract terms");
        result.FileName.Should().Be("updated-document.pdf");

        var versions = await _context.DocumentVersions
            .Where(v => v.DocumentId == document.Id)
            .ToListAsync();

        // UploadNewVersionAsync creates version 0 for the original document if no versions exist
        versions.Should().HaveCount(2); // Version 0 (original) + Version 1 (new)
        versions.Single(v => v.VersionNumber == 1).IsCurrent.Should().BeTrue();
    }

    [Fact]
    public async Task UploadNewVersion_ByOriginalUploader_ShouldSucceed()
    {
        // Arrange
        var document = CreateTestDocument();
        document.UploadedBy = _memberUserId; // Member is original uploader
        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        var file = CreateMockFile("updated-document.pdf", 2048);

        _mockStorageService
            .Setup(x => x.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("version-key");

        // Act
        var result = await _documentService.UploadNewVersionAsync(
            document.Id,
            file.Object,
            "Fixed typos",
            _memberUserId);

        // Assert
        result.Should().NotBeNull();
        result.VersionNumber.Should().Be(1);
        result.UploaderName.Should().Contain("Member");
    }

    [Fact]
    public async Task UploadNewVersion_ByNonAdminNonUploader_ShouldThrowException()
    {
        // Arrange
        var document = CreateTestDocument();
        document.UploadedBy = _adminUserId; // Admin is uploader
        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        var file = CreateMockFile("updated-document.pdf", 2048);

        // Act & Assert: Member (not admin, not uploader) tries to upload
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _documentService.UploadNewVersionAsync(document.Id, file.Object, null, _memberUserId));
    }

    [Fact]
    public async Task UploadMultipleVersions_ShouldIncrementVersionNumbers()
    {
        // Arrange
        var document = CreateTestDocument();
        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        _mockStorageService
            .Setup(x => x.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("version-key");

        // Act: Upload 3 versions
        var file1 = CreateMockFile("version1.pdf", 1024);
        var result1 = await _documentService.UploadNewVersionAsync(
            document.Id, file1.Object, "Version 1", _adminUserId);

        var file2 = CreateMockFile("version2.pdf", 2048);
        var result2 = await _documentService.UploadNewVersionAsync(
            document.Id, file2.Object, "Version 2", _adminUserId);

        var file3 = CreateMockFile("version3.pdf", 3072);
        var result3 = await _documentService.UploadNewVersionAsync(
            document.Id, file3.Object, "Version 3", _adminUserId);

        // Assert
        result1.VersionNumber.Should().Be(1);
        result2.VersionNumber.Should().Be(2);
        result3.VersionNumber.Should().Be(3);

        var versions = await _context.DocumentVersions
            .Where(v => v.DocumentId == document.Id)
            .OrderBy(v => v.VersionNumber)
            .ToListAsync();

        // UploadNewVersionAsync creates version 0 for the original document if no versions exist
        versions.Should().HaveCount(4); // Version 0 (original) + Versions 1, 2, 3
        versions.Single(v => v.VersionNumber == 0).IsCurrent.Should().BeFalse();
        versions.Single(v => v.VersionNumber == 1).IsCurrent.Should().BeFalse();
        versions.Single(v => v.VersionNumber == 2).IsCurrent.Should().BeFalse();
        versions.Single(v => v.VersionNumber == 3).IsCurrent.Should().BeTrue(); // Only latest is current
    }

    [Fact]
    public async Task GetDocumentVersions_ShouldReturnAllVersions()
    {
        // Arrange
        var document = CreateTestDocument();
        _context.Documents.Add(document);

        var version1 = new DocumentVersion
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            VersionNumber = 1,
            StorageKey = "v1-key",
            FileName = "version1.pdf",
            FileSize = 1024,
            ContentType = "application/pdf",
            UploadedBy = _adminUserId,
            UploadedAt = DateTime.UtcNow.AddDays(-2),
            ChangeDescription = "Initial version",
            IsCurrent = false
        };

        var version2 = new DocumentVersion
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            VersionNumber = 2,
            StorageKey = "v2-key",
            FileName = "version2.pdf",
            FileSize = 2048,
            ContentType = "application/pdf",
            UploadedBy = _adminUserId,
            UploadedAt = DateTime.UtcNow,
            ChangeDescription = "Updated version",
            IsCurrent = true
        };

        _context.DocumentVersions.AddRange(version1, version2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _documentService.GetDocumentVersionsAsync(document.Id, _adminUserId);

        // Assert
        result.Should().NotBeNull();
        result.DocumentId.Should().Be(document.Id);
        // GetDocumentVersionsAsync automatically creates version 0 if it doesn't exist
        result.TotalVersions.Should().Be(3); // Version 0 (auto-created) + version 1 + version 2
        result.Versions.Should().HaveCount(3);
        result.Versions[0].VersionNumber.Should().Be(2); // Latest first
        result.Versions[1].VersionNumber.Should().Be(1);
        result.Versions[2].VersionNumber.Should().Be(0); // Auto-created version 0
        result.Versions[0].IsCurrent.Should().BeTrue();
    }

    [Fact]
    public async Task GetDocumentVersions_ByNonMember_ShouldThrowException()
    {
        // Arrange
        var document = CreateTestDocument();
        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        var nonMemberId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _documentService.GetDocumentVersionsAsync(document.Id, nonMemberId));
    }

    [Fact]
    public async Task DownloadVersion_ShouldReturnCorrectVersion()
    {
        // Arrange
        var document = CreateTestDocument();
        _context.Documents.Add(document);

        var version = new DocumentVersion
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            VersionNumber = 1,
            StorageKey = "version-storage-key",
            FileName = "document-v1.pdf",
            FileSize = 1024,
            ContentType = "application/pdf",
            UploadedBy = _adminUserId,
            UploadedAt = DateTime.UtcNow,
            IsCurrent = true
        };

        _context.DocumentVersions.Add(version);
        await _context.SaveChangesAsync();

        var mockStream = new MemoryStream(new byte[1024]);
        _mockStorageService
            .Setup(x => x.DownloadFileAsync(version.StorageKey))
            .ReturnsAsync(mockStream);

        // Act
        var result = await _documentService.DownloadVersionAsync(version.Id, _adminUserId);

        // Assert
        result.Should().NotBeNull();
        result.FileStream.Should().NotBeNull();
        result.FileName.Should().Be("document-v1.pdf");
        result.FileSize.Should().Be(1024);
        result.ContentType.Should().Be("application/pdf");
    }

    [Fact]
    public async Task DownloadVersion_NonExistentVersion_ShouldThrowException()
    {
        // Arrange
        var nonExistentVersionId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _documentService.DownloadVersionAsync(nonExistentVersionId, _adminUserId));
    }

    [Fact]
    public async Task DownloadVersion_ByNonMember_ShouldThrowException()
    {
        // Arrange
        var document = CreateTestDocument();
        _context.Documents.Add(document);

        var version = new DocumentVersion
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            VersionNumber = 1,
            StorageKey = "version-key",
            FileName = "document.pdf",
            FileSize = 1024,
            ContentType = "application/pdf",
            UploadedBy = _adminUserId,
            UploadedAt = DateTime.UtcNow,
            IsCurrent = true
        };

        _context.DocumentVersions.Add(version);
        await _context.SaveChangesAsync();

        var nonMemberId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _documentService.DownloadVersionAsync(version.Id, nonMemberId));
    }

    [Fact]
    public async Task UploadNewVersion_ShouldUpdateDocumentMetadata()
    {
        // Arrange
        var document = CreateTestDocument();
        var originalFileName = document.FileName;
        var originalFileSize = document.FileSize;
        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        var file = CreateMockFile("new-version.pdf", 5120);

        _mockStorageService
            .Setup(x => x.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("new-key");

        // Act
        await _documentService.UploadNewVersionAsync(
            document.Id,
            file.Object,
            "Major update",
            _adminUserId);

        // Assert
        var updatedDocument = await _context.Documents.FindAsync(document.Id);
        updatedDocument!.FileName.Should().Be("new-version.pdf");
        updatedDocument.FileSize.Should().Be(5120);
        updatedDocument.FileName.Should().NotBe(originalFileName);
        updatedDocument.FileSize.Should().NotBe(originalFileSize);
    }

    [Fact]
    public async Task VersionControl_WithInvalidFile_ShouldThrowException()
    {
        // Arrange
        var document = CreateTestDocument();
        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        var emptyFile = CreateMockFile("empty.pdf", 0); // Empty file

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _documentService.UploadNewVersionAsync(document.Id, emptyFile.Object, null, _adminUserId));
    }

    private Document CreateTestDocument()
    {
        return new Document
        {
            Id = Guid.NewGuid(),
            GroupId = _testGroupId,
            Type = DocumentType.OwnershipAgreement,
            FileName = "original-document.pdf",
            StorageKey = "original-key",
            ContentType = "application/pdf",
            FileSize = 1024,
            SignatureStatus = SignatureStatus.Draft,
            UploadedBy = _adminUserId
        };
    }

    private Mock<IFormFile> CreateMockFile(string fileName, long fileSize)
    {
        // PDF file signature: %PDF (0x25, 0x50, 0x44, 0x46)
        var pdfHeader = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34, 0x0A }; // %PDF-1.4\n
        var fileBytes = new byte[Math.Max(fileSize, pdfHeader.Length)];
        Array.Copy(pdfHeader, fileBytes, pdfHeader.Length);
        // Fill rest with zeros if fileSize > header length
        // Add unique identifier based on fileName to make each file have different hash
        var fileNameHash = fileName.GetHashCode();
        var uniqueBytes = BitConverter.GetBytes(fileNameHash);
        Array.Copy(uniqueBytes, 0, fileBytes, pdfHeader.Length, Math.Min(uniqueBytes.Length, fileBytes.Length - pdfHeader.Length));

        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns(fileName);
        mockFile.Setup(f => f.Length).Returns(fileSize);
        mockFile.Setup(f => f.ContentType).Returns("application/pdf");
        // Each call to OpenReadStream should return a new stream instance
        mockFile.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(fileBytes));
        mockFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns((Stream target, CancellationToken token) =>
            {
                using var source = new MemoryStream(fileBytes);
                return source.CopyToAsync(target, token);
            });

        return mockFile;
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
