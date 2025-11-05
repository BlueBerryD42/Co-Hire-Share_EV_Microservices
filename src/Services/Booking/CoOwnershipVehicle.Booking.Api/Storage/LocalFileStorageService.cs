using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoOwnershipVehicle.Booking.Api.Storage;

[SupportedOSPlatform("windows")]
public class LocalFileStorageService : IFileStorageService
{
    private readonly StorageOptions _options;
    private readonly ILogger<LocalFileStorageService> _logger;

    public LocalFileStorageService(IOptions<StorageOptions> options, ILogger<LocalFileStorageService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<FileStorageResult> SaveFileAsync(Stream stream, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        await using var memoryStream = await EnsureMemoryStreamAsync(stream, cancellationToken);
        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = GetDefaultExtension(contentType);
        }

        var normalizedContentType = string.IsNullOrWhiteSpace(contentType)
            ? GetContentTypeFromExtension(extension)
            : contentType;

        var uniqueName = $"{Guid.NewGuid():N}{extension}";
        var relativePath = Path.Combine(_options.CheckInFolder, "documents", uniqueName);
        var absolutePath = Path.Combine(_options.RootPath, relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

        await using (var fileStream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            memoryStream.Seek(0, SeekOrigin.Begin);
            await memoryStream.CopyToAsync(fileStream, cancellationToken);
        }

        var fileUrl = BuildPublicUrl(relativePath);

        return new FileStorageResult(
            fileUrl,
            null,
            absolutePath,
            null,
            normalizedContentType,
            null,
            null,
            null);
    }

    public async Task<FileStorageResult> SaveImageAsync(Stream stream, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        await using var memoryStream = await EnsureMemoryStreamAsync(stream, cancellationToken);
        memoryStream.Seek(0, SeekOrigin.Begin);

        using var image = Image.FromStream(memoryStream, useEmbeddedColorManagement: true, validateImageData: true);
        ApplyOrientation(image);
        var metadata = ExtractMetadata(image);

        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = GetDefaultExtension(contentType);
        }

        var normalizedContentType = string.IsNullOrWhiteSpace(contentType)
            ? GetContentTypeFromExtension(extension)
            : contentType;

        var uniqueName = $"{Guid.NewGuid():N}{extension}";
        var relativeOriginalPath = Path.Combine(_options.CheckInFolder, "original", uniqueName);
        var relativeThumbPath = Path.Combine(_options.CheckInFolder, "thumb", uniqueName);

        var absoluteOriginalPath = Path.Combine(_options.RootPath, relativeOriginalPath);
        var absoluteThumbPath = Path.Combine(_options.RootPath, relativeThumbPath);

        Directory.CreateDirectory(Path.GetDirectoryName(absoluteOriginalPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(absoluteThumbPath)!);

        image.Save(absoluteOriginalPath, image.RawFormat);
        using (var thumbnail = CreateThumbnail(image, _options.ThumbnailSize))
        {
            thumbnail.Save(absoluteThumbPath, ImageFormat.Jpeg);
        }

        var fileUrl = BuildPublicUrl(relativeOriginalPath);
        var thumbnailUrl = BuildPublicUrl(relativeThumbPath);

        return new FileStorageResult(
            fileUrl,
            thumbnailUrl,
            absoluteOriginalPath,
            absoluteThumbPath,
            normalizedContentType,
            metadata.CapturedAt,
            metadata.Latitude,
            metadata.Longitude);
    }

    public Task DeleteAsync(string storagePath, string? thumbnailPath, CancellationToken cancellationToken = default)
    {
        TryDelete(storagePath);
        if (!string.IsNullOrEmpty(thumbnailPath))
        {
            TryDelete(thumbnailPath);
        }
        return Task.CompletedTask;
    }

    private static async Task<MemoryStream> EnsureMemoryStreamAsync(Stream stream, CancellationToken cancellationToken)
    {
        if (stream is MemoryStream ms && stream.CanSeek)
        {
            return ms;
        }

        var memoryStream = new MemoryStream();
        if (stream.CanSeek)
        {
            stream.Seek(0, SeekOrigin.Begin);
        }

        await stream.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Seek(0, SeekOrigin.Begin);
        return memoryStream;
    }

    private static Bitmap CreateThumbnail(Image source, int targetSize)
    {
        var ratio = Math.Min((double)targetSize / source.Width, (double)targetSize / source.Height);
        var width = Math.Max(1, (int)Math.Round(source.Width * ratio));
        var height = Math.Max(1, (int)Math.Round(source.Height * ratio));

        var thumbnail = new Bitmap(width, height);
        thumbnail.SetResolution(source.HorizontalResolution, source.VerticalResolution);

        using (var graphics = Graphics.FromImage(thumbnail))
        {
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.DrawImage(source, new Rectangle(0, 0, width, height), new Rectangle(0, 0, source.Width, source.Height), GraphicsUnit.Pixel);
        }

        return thumbnail;
    }

    private static void ApplyOrientation(Image image)
    {
        const int orientationId = 0x0112;
        var property = TryGetProperty(image, orientationId);
        if (property?.Value == null || property.Value.Length == 0)
        {
            return;
        }

        var orientation = property.Value[0];

        try
        {
            switch (orientation)
            {
                case 2:
                    image.RotateFlip(RotateFlipType.RotateNoneFlipX);
                    break;
                case 3:
                    image.RotateFlip(RotateFlipType.Rotate180FlipNone);
                    break;
                case 4:
                    image.RotateFlip(RotateFlipType.Rotate180FlipX);
                    break;
                case 5:
                    image.RotateFlip(RotateFlipType.Rotate90FlipX);
                    break;
                case 6:
                    image.RotateFlip(RotateFlipType.Rotate90FlipNone);
                    break;
                case 7:
                    image.RotateFlip(RotateFlipType.Rotate270FlipX);
                    break;
                case 8:
                    image.RotateFlip(RotateFlipType.Rotate270FlipNone);
                    break;
            }

            image.RemovePropertyItem(orientationId);
        }
        catch (ArgumentException)
        {
            // Ignore if the property cannot be removed.
        }
    }

    private static (DateTime? CapturedAt, double? Latitude, double? Longitude) ExtractMetadata(Image image)
    {
        var capturedAt = TryGetDateTime(image, 0x9003) ?? TryGetDateTime(image, 0x0132);
        var latitude = TryGetGpsCoordinate(image, 0x0002, 0x0001);
        var longitude = TryGetGpsCoordinate(image, 0x0004, 0x0003);

        return (capturedAt, latitude, longitude);
    }

    private static DateTime? TryGetDateTime(Image image, int propertyId)
    {
        var property = TryGetProperty(image, propertyId);
        if (property?.Value == null || property.Value.Length == 0)
        {
            return null;
        }

        var rawValue = Encoding.ASCII.GetString(property.Value).Trim('\0', ' ');
        if (DateTime.TryParseExact(rawValue, "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dateTime))
        {
            return dateTime.ToUniversalTime();
        }

        return null;
    }

    private static double? TryGetGpsCoordinate(Image image, int valuePropertyId, int referencePropertyId)
    {
        var valueProperty = TryGetProperty(image, valuePropertyId);
        var referenceProperty = TryGetProperty(image, referencePropertyId);

        if (valueProperty?.Value == null || referenceProperty?.Value == null)
        {
            return null;
        }

        var reference = Encoding.ASCII.GetString(referenceProperty.Value).Trim('\0', ' ');
        var bytes = valueProperty.Value;

        if (bytes.Length < 24)
        {
            return null;
        }

        double? ToDouble(int offset)
        {
            var numerator = BitConverter.ToUInt32(bytes, offset);
            var denominator = BitConverter.ToUInt32(bytes, offset + 4);
            if (denominator == 0)
            {
                return null;
            }
            return (double)numerator / denominator;
        }

        var degrees = ToDouble(0);
        var minutes = ToDouble(8);
        var seconds = ToDouble(16);

        if (degrees == null || minutes == null || seconds == null)
        {
            return null;
        }

        var coordinate = degrees.Value + (minutes.Value / 60d) + (seconds.Value / 3600d);

        if (string.Equals(reference, "S", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(reference, "W", StringComparison.OrdinalIgnoreCase))
        {
            coordinate *= -1;
        }

        return coordinate;
    }

    private static PropertyItem? TryGetProperty(Image image, int propertyId)
    {
        try
        {
            return image.GetPropertyItem(propertyId);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private string BuildPublicUrl(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        if (!string.IsNullOrWhiteSpace(_options.PublicBaseUrl))
        {
            return $"{_options.PublicBaseUrl.TrimEnd('/')}/{normalized}";
        }

        return normalized;
    }

    private static string GetDefaultExtension(string contentType) =>
        contentType.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/bmp" => ".bmp",
            "image/webp" => ".webp",
            _ => ".jpg"
        };

    private static string GetContentTypeFromExtension(string extension) =>
        extension.ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };

    private static void TryDelete(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException ex)
        {
            // Log at debug level because these failures are non-critical.
            Console.WriteLine($"Failed to delete file '{path}': {ex.Message}");
        }
    }
}
