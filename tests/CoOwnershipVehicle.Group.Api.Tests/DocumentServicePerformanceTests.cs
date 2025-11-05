using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Group.Api.Data;
using CoOwnershipVehicle.Group.Api.DTOs;
using CoOwnershipVehicle.Group.Api.Services.Implementations;
using CoOwnershipVehicle.Group.Api.Services.Interfaces;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using NBomber.Contracts;
using NBomber.CSharp;
using Xunit;
using Xunit.Abstractions;

namespace CoOwnershipVehicle.Group.Api.Tests;

/// <summary>
/// Performance tests for Document Service
/// Tests load handling, concurrency, large file operations, and search performance
/// </summary>
public class DocumentServicePerformanceTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly GroupDbContext _context;
    private readonly Mock<IFileStorageService> _mockFileStorage;
    private readonly Mock<IVirusScanService> _mockVirusScan;
    private readonly Mock<INotificationService> _mockNotificationService;
    private readonly DocumentService _documentService;
    private readonly DocumentSearchService _searchService;

    private readonly Guid _testGroupId = Guid.NewGuid();
    private readonly Guid _testUserId = Guid.NewGuid();

    public DocumentServicePerformanceTests(ITestOutputHelper output)
    {
        _output = output;

        var options = new DbContextOptionsBuilder<GroupDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new GroupDbContext(options);
        _mockFileStorage = new Mock<IFileStorageService>();
        _mockVirusScan = new Mock<IVirusScanService>();
        _mockNotificationService = new Mock<INotificationService>();

        _documentService = new DocumentService(
            _context,
            _mockFileStorage.Object,
            _mockVirusScan.Object,
            Mock.Of<ISigningTokenService>(),
            Mock.Of<ICertificateGenerationService>(),
            _mockNotificationService.Object,
            Mock.Of<ILogger<DocumentService>>());

        _searchService = new DocumentSearchService(
            _context,
            _mockFileStorage.Object,
            Mock.Of<ILogger<DocumentSearchService>>());

        SetupMocks();
        SeedTestData();
    }

    private void SetupMocks()
    {
        _mockVirusScan.Setup(x => x.ScanFileAsync(It.IsAny<Stream>()))
            .ReturnsAsync(new VirusScanResult { IsClean = true });

        _mockFileStorage.Setup(x => x.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((Stream s, string key, string ct) => key);

        _mockFileStorage.Setup(x => x.DownloadFileAsync(It.IsAny<string>()))
            .ReturnsAsync(new MemoryStream(new byte[1024]));

        _mockNotificationService.Setup(x => x.SendDocumentUploadedNotificationAsync(
            It.IsAny<User>(), It.IsAny<Document>(), It.IsAny<OwnershipGroup>()))
            .ReturnsAsync(true);
    }

    private void SeedTestData()
    {
        var user = new User
        {
            Id = _testUserId,
            Email = "perftest@test.com",
            FirstName = "Perf",
            LastName = "Tester",
            Phone = "1234567890",
            KycStatus = KycStatus.Approved,
            Role = UserRole.CoOwner,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };

        var group = new OwnershipGroup
        {
            Id = _testGroupId,
            Name = "Performance Test Group",
            CreatedBy = _testUserId,
            Status = GroupStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        var member = new GroupMember
        {
            Id = Guid.NewGuid(),
            GroupId = _testGroupId,
            UserId = _testUserId,
            RoleInGroup = GroupRole.Admin,
            SharePercentage = 1.0m,
            JoinedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        _context.OwnershipGroups.Add(group);
        _context.GroupMembers.Add(member);
        _context.SaveChanges();
    }

    [Fact]
    public async Task LoadTest_ConcurrentDocumentUploads_ShouldHandleLoad()
    {
        // Arrange
        const int concurrentUploads = 50;
        const int uploadSizeKB = 100;

        // Act
        var uploadTasks = Enumerable.Range(0, concurrentUploads).Select(async i =>
        {
            var fileStream = new MemoryStream(new byte[uploadSizeKB * 1024]);
            var request = new UploadDocumentRequest
            {
                GroupId = _testGroupId,
                FileName = $"concurrent-{i}.pdf",
                ContentType = "application/pdf",
                FileSize = fileStream.Length,
                Type = DocumentType.Contract
            };

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = await _documentService.UploadDocumentAsync(request, fileStream, _testUserId);
            stopwatch.Stop();

            return new { Result = result, Duration = stopwatch.ElapsedMilliseconds };
        }).ToList();

        var results = await Task.WhenAll(uploadTasks);

        // Assert
        results.Should().HaveCount(concurrentUploads);
        results.All(r => r.Result != null).Should().BeTrue();

        var avgDuration = results.Average(r => r.Duration);
        var maxDuration = results.Max(r => r.Duration);

        _output.WriteLine($"Concurrent Uploads: {concurrentUploads}");
        _output.WriteLine($"Average Duration: {avgDuration}ms");
        _output.WriteLine($"Max Duration: {maxDuration}ms");

        // Performance assertions
        avgDuration.Should().BeLessThan(5000, "Average upload time should be under 5 seconds");
        maxDuration.Should().BeLessThan(10000, "Max upload time should be under 10 seconds");
    }

    [Fact]
    public async Task LoadTest_ConcurrentDownloads_ShouldStream()
    {
        // Arrange - Create documents for download
        const int documentCount = 100;
        var documents = new List<Guid>();

        for (int i = 0; i < documentCount; i++)
        {
            var doc = new Document
            {
                Id = Guid.NewGuid(),
                GroupId = _testGroupId,
                FileName = $"download-test-{i}.pdf",
                StorageKey = $"storage/download-{i}.pdf",
                ContentType = "application/pdf",
                FileSize = 1024 * 100, // 100KB
                Type = DocumentType.Contract,
                SignatureStatus = SignatureStatus.NoSignatureRequired,
                UploadedBy = _testUserId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Documents.Add(doc);
            documents.Add(doc.Id);
        }

        await _context.SaveChangesAsync();

        // Act - Concurrent downloads
        var downloadTasks = documents.Select(async docId =>
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var stream = await _documentService.DownloadDocumentAsync(docId, _testUserId);
            stopwatch.Stop();

            return new { Stream = stream, Duration = stopwatch.ElapsedMilliseconds };
        }).ToList();

        var results = await Task.WhenAll(downloadTasks);

        // Assert
        results.Should().HaveCount(documentCount);
        results.All(r => r.Stream != null).Should().BeTrue();

        var avgDuration = results.Average(r => r.Duration);
        var maxDuration = results.Max(r => r.Duration);

        _output.WriteLine($"Concurrent Downloads: {documentCount}");
        _output.WriteLine($"Average Duration: {avgDuration}ms");
        _output.WriteLine($"Max Duration: {maxDuration}ms");

        avgDuration.Should().BeLessThan(2000, "Average download time should be under 2 seconds");
    }

    [Fact]
    public async Task LargeFileTest_50MBUpload_ShouldSucceed()
    {
        // Arrange
        const int fileSizeMB = 50;
        var largeFileData = new byte[fileSizeMB * 1024 * 1024];
        new Random().NextBytes(largeFileData);

        var fileStream = new MemoryStream(largeFileData);
        var request = new UploadDocumentRequest
        {
            GroupId = _testGroupId,
            FileName = "large-file-50mb.pdf",
            ContentType = "application/pdf",
            FileSize = fileStream.Length,
            Type = DocumentType.Contract
        };

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await _documentService.UploadDocumentAsync(request, fileStream, _testUserId);
        stopwatch.Stop();

        // Assert
        result.Should().NotBeNull();
        result.FileSize.Should().Be(fileSizeMB * 1024 * 1024);

        _output.WriteLine($"Large File Upload ({fileSizeMB}MB): {stopwatch.ElapsedMilliseconds}ms");
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(30000, "50MB upload should complete within 30 seconds");
    }

    [Fact]
    public async Task SearchPerformanceTest_With1000Documents_ShouldBeFast()
    {
        // Arrange - Create 1000+ documents
        const int documentCount = 1000;
        var random = new Random();

        var documentTypes = Enum.GetValues<DocumentType>();
        var signatureStatuses = Enum.GetValues<SignatureStatus>();

        for (int i = 0; i < documentCount; i++)
        {
            var doc = new Document
            {
                Id = Guid.NewGuid(),
                GroupId = _testGroupId,
                FileName = $"search-test-{i}-{Guid.NewGuid()}.pdf",
                StorageKey = $"storage/search-{i}.pdf",
                ContentType = "application/pdf",
                FileSize = random.Next(1000, 10000000),
                Type = documentTypes[random.Next(documentTypes.Length)],
                SignatureStatus = signatureStatuses[random.Next(signatureStatuses.Length)],
                Description = $"Search test document number {i} with various keywords",
                UploadedBy = _testUserId,
                CreatedAt = DateTime.UtcNow.AddDays(-random.Next(0, 365))
            };

            _context.Documents.Add(doc);

            // Batch save to improve performance
            if (i % 100 == 0)
            {
                await _context.SaveChangesAsync();
            }
        }

        await _context.SaveChangesAsync();

        // Act - Test various search scenarios
        var searchScenarios = new[]
        {
            new AdvancedDocumentSearchRequest
            {
                GroupId = _testGroupId,
                SearchTerm = "test",
                Page = 1,
                PageSize = 20
            },
            new AdvancedDocumentSearchRequest
            {
                GroupId = _testGroupId,
                DocumentTypes = new List<DocumentType> { DocumentType.Contract },
                Page = 1,
                PageSize = 20
            },
            new AdvancedDocumentSearchRequest
            {
                GroupId = _testGroupId,
                SignatureStatuses = new List<SignatureStatus> { SignatureStatus.FullySigned },
                Page = 1,
                PageSize = 20
            },
            new AdvancedDocumentSearchRequest
            {
                GroupId = _testGroupId,
                UploadedFrom = DateTime.UtcNow.AddMonths(-6),
                UploadedTo = DateTime.UtcNow,
                SortBy = DocumentSortBy.UploadedDate,
                SortDescending = true,
                Page = 1,
                PageSize = 50
            }
        };

        var searchResults = new List<(string Scenario, long Duration, int ResultCount)>();

        foreach (var (scenario, index) in searchScenarios.Select((s, i) => (s, i)))
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = await _searchService.SearchDocumentsAsync(scenario, _testUserId);
            stopwatch.Stop();

            searchResults.Add(($"Scenario {index + 1}", stopwatch.ElapsedMilliseconds, result.TotalCount));
        }

        // Assert
        foreach (var (scenario, duration, resultCount) in searchResults)
        {
            _output.WriteLine($"{scenario}: {duration}ms, Results: {resultCount}");
            duration.Should().BeLessThan(3000, $"{scenario} should complete within 3 seconds");
        }

        var avgSearchTime = searchResults.Average(r => r.Duration);
        _output.WriteLine($"Average Search Time: {avgSearchTime}ms");
        avgSearchTime.Should().BeLessThan(2000, "Average search time should be under 2 seconds");
    }

    [Fact]
    public async Task PaginationPerformanceTest_DeepPagination_ShouldBeFast()
    {
        // Arrange - Create documents
        for (int i = 0; i < 500; i++)
        {
            var doc = new Document
            {
                Id = Guid.NewGuid(),
                GroupId = _testGroupId,
                FileName = $"pagination-test-{i}.pdf",
                StorageKey = $"storage/page-{i}.pdf",
                ContentType = "application/pdf",
                FileSize = 1000,
                Type = DocumentType.Contract,
                SignatureStatus = SignatureStatus.NoSignatureRequired,
                UploadedBy = _testUserId,
                CreatedAt = DateTime.UtcNow.AddMinutes(-i)
            };

            _context.Documents.Add(doc);
        }

        await _context.SaveChangesAsync();

        // Act - Test pagination at different depths
        var pageTests = new[] { 1, 5, 10, 20 };
        var paginationResults = new List<(int Page, long Duration)>();

        foreach (var page in pageTests)
        {
            var request = new AdvancedDocumentSearchRequest
            {
                GroupId = _testGroupId,
                Page = page,
                PageSize = 20,
                SortBy = DocumentSortBy.UploadedDate,
                SortDescending = true
            };

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = await _searchService.SearchDocumentsAsync(request, _testUserId);
            stopwatch.Stop();

            paginationResults.Add((page, stopwatch.ElapsedMilliseconds));
        }

        // Assert
        foreach (var (page, duration) in paginationResults)
        {
            _output.WriteLine($"Page {page}: {duration}ms");
            duration.Should().BeLessThan(1500, $"Page {page} should load within 1.5 seconds");
        }

        // Deep pagination shouldn't be significantly slower
        var firstPageTime = paginationResults.First(r => r.Page == 1).Duration;
        var lastPageTime = paginationResults.Last().Duration;

        (lastPageTime - firstPageTime).Should().BeLessThan(500, "Deep pagination shouldn't be more than 500ms slower");
    }

    [Fact]
    public void MemoryLeakTest_RepeatedFileOperations_ShouldNotLeakMemory()
    {
        // Arrange
        var initialMemory = GC.GetTotalMemory(true);

        // Act - Perform many file operations
        for (int i = 0; i < 100; i++)
        {
            using var stream = new MemoryStream(new byte[1024 * 100]); // 100KB
            var request = new UploadDocumentRequest
            {
                GroupId = _testGroupId,
                FileName = $"memory-test-{i}.pdf",
                ContentType = "application/pdf",
                FileSize = stream.Length,
                Type = DocumentType.Contract
            };

            var task = _documentService.UploadDocumentAsync(request, stream, _testUserId);
            task.Wait();
        }

        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = GC.GetTotalMemory(true);
        var memoryIncrease = finalMemory - initialMemory;

        // Assert
        _output.WriteLine($"Initial Memory: {initialMemory / 1024 / 1024}MB");
        _output.WriteLine($"Final Memory: {finalMemory / 1024 / 1024}MB");
        _output.WriteLine($"Memory Increase: {memoryIncrease / 1024 / 1024}MB");

        // Memory increase should be reasonable (less than 50MB for 100 operations)
        memoryIncrease.Should().BeLessThan(50 * 1024 * 1024, "Memory leak detected");
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
