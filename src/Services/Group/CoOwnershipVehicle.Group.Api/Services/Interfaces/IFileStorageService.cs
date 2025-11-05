namespace CoOwnershipVehicle.Group.Api.Services.Interfaces;

public interface IFileStorageService
{
    Task<string> UploadFileAsync(Stream fileStream, string storageKey, string contentType);
    Task<Stream> DownloadFileAsync(string storageKey);
    Task DeleteFileAsync(string storageKey);
    Task<string> GetSecureUrlAsync(string storageKey, TimeSpan? expiresIn = null);
    Task<bool> FileExistsAsync(string storageKey);
    Task<FileMetadata> GetFileMetadataAsync(string storageKey);
}

public class FileMetadata
{
    public long Size { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
    public string ETag { get; set; } = string.Empty;
}