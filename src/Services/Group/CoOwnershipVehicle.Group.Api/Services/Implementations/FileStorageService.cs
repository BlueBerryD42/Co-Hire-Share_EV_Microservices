using CoOwnershipVehicle.Group.Api.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace CoOwnershipVehicle.Group.Api.Services.Implementations;

public class FileStorageService : IFileStorageService
{
    private readonly FileStorageOptions _options;
    private readonly ILogger<FileStorageService> _logger;

    public FileStorageService(IOptions<FileStorageOptions> options, ILogger<FileStorageService> logger)
    {
        _options = options.Value;
        _logger = logger;

        // Ensure storage directory exists for local storage
        if (_options.StorageType == StorageType.Local && !string.IsNullOrEmpty(_options.LocalStoragePath))
        {
            Directory.CreateDirectory(_options.LocalStoragePath);
        }
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string storageKey, string contentType)
    {
        try
        {
            if (_options.StorageType == StorageType.Local)
            {
                return await UploadToLocalStorageAsync(fileStream, storageKey);
            }
            else if (_options.StorageType == StorageType.AzureBlob)
            {
                return await UploadToAzureBlobAsync(fileStream, storageKey, contentType);
            }
            else if (_options.StorageType == StorageType.AwsS3)
            {
                return await UploadToAwsS3Async(fileStream, storageKey, contentType);
            }

            throw new NotSupportedException($"Storage type {_options.StorageType} is not supported");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload file with storage key: {StorageKey}", storageKey);
            throw;
        }
    }

    public async Task<Stream> DownloadFileAsync(string storageKey)
    {
        try
        {
            if (_options.StorageType == StorageType.Local)
            {
                return await DownloadFromLocalStorageAsync(storageKey);
            }
            else if (_options.StorageType == StorageType.AzureBlob)
            {
                return await DownloadFromAzureBlobAsync(storageKey);
            }
            else if (_options.StorageType == StorageType.AwsS3)
            {
                return await DownloadFromAwsS3Async(storageKey);
            }

            throw new NotSupportedException($"Storage type {_options.StorageType} is not supported");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download file with storage key: {StorageKey}", storageKey);
            throw;
        }
    }

    public async Task DeleteFileAsync(string storageKey)
    {
        try
        {
            if (_options.StorageType == StorageType.Local)
            {
                await DeleteFromLocalStorageAsync(storageKey);
            }
            else if (_options.StorageType == StorageType.AzureBlob)
            {
                await DeleteFromAzureBlobAsync(storageKey);
            }
            else if (_options.StorageType == StorageType.AwsS3)
            {
                await DeleteFromAwsS3Async(storageKey);
            }
            else
            {
                throw new NotSupportedException($"Storage type {_options.StorageType} is not supported");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file with storage key: {StorageKey}", storageKey);
            throw;
        }
    }

    public async Task<string> GetSecureUrlAsync(string storageKey, TimeSpan? expiresIn = null)
    {
        var expiration = expiresIn ?? TimeSpan.FromHours(1);

        if (_options.StorageType == StorageType.Local)
        {
            // For local storage, return a controller endpoint URL
            return $"/api/document/download/{storageKey}";
        }
        else if (_options.StorageType == StorageType.AzureBlob)
        {
            return await GetAzureBlobSasUrlAsync(storageKey, expiration);
        }
        else if (_options.StorageType == StorageType.AwsS3)
        {
            return await GetAwsS3PresignedUrlAsync(storageKey, expiration);
        }

        throw new NotSupportedException($"Storage type {_options.StorageType} is not supported");
    }

    public async Task<bool> FileExistsAsync(string storageKey)
    {
        if (_options.StorageType == StorageType.Local)
        {
            var filePath = Path.Combine(_options.LocalStoragePath!, storageKey);
            return File.Exists(filePath);
        }
        else if (_options.StorageType == StorageType.AzureBlob)
        {
            return await AzureBlobExistsAsync(storageKey);
        }
        else if (_options.StorageType == StorageType.AwsS3)
        {
            return await AwsS3ObjectExistsAsync(storageKey);
        }

        throw new NotSupportedException($"Storage type {_options.StorageType} is not supported");
    }

    public async Task<FileMetadata> GetFileMetadataAsync(string storageKey)
    {
        if (_options.StorageType == StorageType.Local)
        {
            return await GetLocalFileMetadataAsync(storageKey);
        }
        else if (_options.StorageType == StorageType.AzureBlob)
        {
            return await GetAzureBlobMetadataAsync(storageKey);
        }
        else if (_options.StorageType == StorageType.AwsS3)
        {
            return await GetAwsS3MetadataAsync(storageKey);
        }

        throw new NotSupportedException($"Storage type {_options.StorageType} is not supported");
    }

    // Local Storage Implementation
    private async Task<string> UploadToLocalStorageAsync(Stream fileStream, string storageKey)
    {
        _logger.LogInformation("Uploading to local storage. Stream length: {StreamLength}", fileStream.Length);
        var filePath = Path.Combine(_options.LocalStoragePath!, storageKey);
        var directory = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var fileOutput = File.Create(filePath);
        await fileStream.CopyToAsync(fileOutput);

        return storageKey;
    }

    private Task<Stream> DownloadFromLocalStorageAsync(string storageKey)
    {
        var filePath = Path.Combine(_options.LocalStoragePath!, storageKey);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {storageKey}");
        }

        Stream stream = File.OpenRead(filePath);
        return Task.FromResult(stream);
    }

    private Task DeleteFromLocalStorageAsync(string storageKey)
    {
        var filePath = Path.Combine(_options.LocalStoragePath!, storageKey);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }

    private Task<FileMetadata> GetLocalFileMetadataAsync(string storageKey)
    {
        var filePath = Path.Combine(_options.LocalStoragePath!, storageKey);
        var fileInfo = new FileInfo(filePath);

        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException($"File not found: {storageKey}");
        }

        return Task.FromResult(new FileMetadata
        {
            Size = fileInfo.Length,
            ContentType = GetContentType(storageKey),
            LastModified = fileInfo.LastWriteTimeUtc,
            ETag = fileInfo.LastWriteTimeUtc.Ticks.ToString()
        });
    }

    // Azure Blob Storage Placeholders
    private Task<string> UploadToAzureBlobAsync(Stream fileStream, string storageKey, string contentType)
    {
        throw new NotImplementedException("Azure Blob Storage requires Azure.Storage.Blobs package");
    }

    private Task<Stream> DownloadFromAzureBlobAsync(string storageKey)
    {
        throw new NotImplementedException("Azure Blob Storage requires Azure.Storage.Blobs package");
    }

    private Task DeleteFromAzureBlobAsync(string storageKey)
    {
        throw new NotImplementedException("Azure Blob Storage requires Azure.Storage.Blobs package");
    }

    private Task<string> GetAzureBlobSasUrlAsync(string storageKey, TimeSpan expiration)
    {
        throw new NotImplementedException("Azure Blob Storage requires Azure.Storage.Blobs package");
    }

    private Task<bool> AzureBlobExistsAsync(string storageKey)
    {
        throw new NotImplementedException("Azure Blob Storage requires Azure.Storage.Blobs package");
    }

    private Task<FileMetadata> GetAzureBlobMetadataAsync(string storageKey)
    {
        throw new NotImplementedException("Azure Blob Storage requires Azure.Storage.Blobs package");
    }

    // AWS S3 Placeholders
    private Task<string> UploadToAwsS3Async(Stream fileStream, string storageKey, string contentType)
    {
        throw new NotImplementedException("AWS S3 requires AWSSDK.S3 package");
    }

    private Task<Stream> DownloadFromAwsS3Async(string storageKey)
    {
        throw new NotImplementedException("AWS S3 requires AWSSDK.S3 package");
    }

    private Task DeleteFromAwsS3Async(string storageKey)
    {
        throw new NotImplementedException("AWS S3 requires AWSSDK.S3 package");
    }

    private Task<string> GetAwsS3PresignedUrlAsync(string storageKey, TimeSpan expiration)
    {
        throw new NotImplementedException("AWS S3 requires AWSSDK.S3 package");
    }

    private Task<bool> AwsS3ObjectExistsAsync(string storageKey)
    {
        throw new NotImplementedException("AWS S3 requires AWSSDK.S3 package");
    }

    private Task<FileMetadata> GetAwsS3MetadataAsync(string storageKey)
    {
        throw new NotImplementedException("AWS S3 requires AWSSDK.S3 package");
    }

    private string GetContentType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".doc" => "application/msword",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            _ => "application/octet-stream"
        };
    }
}

public class FileStorageOptions
{
    public StorageType StorageType { get; set; } = StorageType.Local;
    public string? LocalStoragePath { get; set; }
    public string? AzureConnectionString { get; set; }
    public string? AzureContainerName { get; set; }
    public string? AwsAccessKey { get; set; }
    public string? AwsSecretKey { get; set; }
    public string? AwsBucketName { get; set; }
    public string? AwsRegion { get; set; }
}

public enum StorageType
{
    Local,
    AzureBlob,
    AwsS3
}