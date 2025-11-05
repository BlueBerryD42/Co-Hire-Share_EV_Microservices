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
    private readonly Mock<IStorageService> _mockStorageService;
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
        _mockStorageService = new Mock<IStorageService>();
        _mockLogger = new Mock<ILogger<DocumentService>>();
        _mockCertificateService = new Mock<ICertificateGenerationService>();

        _documentService = new DocumentService(
            _context,
            _mockStorageService.Object,
            _mockLogger.Object,
            _mockCertificateService.Object);

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
            .Setup(x => x.UploadFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>()))
            .ReturnsAsync(new StorageUploadResult { Success = true, StorageKey = "new-version-key" });

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

        versions.Should().HaveCount(1);
        versions[0].IsCurrent.Should().BeTrue();
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
            .Setup(x => x.UploadFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>()))
            .ReturnsAsync(new StorageUploadResult { Success = true, StorageKey = "version-key" });

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
            .Setup(x => x.UploadFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>()))
            .ReturnsAsync(new StorageUploadResult { Success = true, StorageKey = "version-key" });

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

        versions.Should().HaveCount(3);
        versions[0].IsCurrent.Should().BeFalse();
        versions[1].IsCurrent.Should().BeFalse();
        versions[2].IsCurrent.Should().BeTrue(); // Only latest is current
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
        result.TotalVersions.Should().Be(2);
        result.Versions.Should().HaveCount(2);
        result.Versions[0].VersionNumber.Should().Be(2); // Latest first
        result.Versions[1].VersionNumber.Should().Be(1);
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
            .Setup(x => x.UploadFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>()))
            .ReturnsAsync(new StorageUploadResult { Success = true, StorageKey = "new-key" });

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
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns(fileName);
        mockFile.Setup(f => f.Length).Returns(fileSize);
        mockFile.Setup(f => f.ContentType).Returns("application/pdf");

        var content = new byte[fileSize];
        new Random().NextBytes(content);
        var stream = new MemoryStream(content);

        mockFile.Setup(f => f.OpenReadStream()).Returns(stream);
        mockFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns((Stream target, CancellationToken token) => stream.CopyToAsync(target, token));

        return mockFile;
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
