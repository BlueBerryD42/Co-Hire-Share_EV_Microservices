using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CoOwnershipVehicle.Booking.Api.Configuration;
using CoOwnershipVehicle.Booking.Api.Contracts;
using CoOwnershipVehicle.Booking.Api.DTOs;
using CoOwnershipVehicle.Booking.Api.Repositories;
using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QRCoder;
using BookingEntity = CoOwnershipVehicle.Domain.Entities.Booking;

namespace CoOwnershipVehicle.Booking.Api.Services;

internal sealed class VehicleQrService : IQrCodeService
{
    private readonly IBookingRepository _bookingRepository;
    private readonly ILogger<VehicleQrService> _logger;
    private readonly IMemoryCache _cache;
    private readonly IOptionsMonitor<QrCodeOptions> _optionsMonitor;
    private byte[] _encryptionKey;
    private byte[] _signingKey;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public VehicleQrService(
        IBookingRepository bookingRepository,
        ILogger<VehicleQrService> logger,
        IMemoryCache cache,
        IOptionsMonitor<QrCodeOptions> optionsMonitor)
    {
        _bookingRepository = bookingRepository ?? throw new ArgumentNullException(nameof(bookingRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
        (_encryptionKey, _signingKey) = ResolveKeys(_optionsMonitor.CurrentValue);
        _optionsMonitor.OnChange(opts =>
        {
            (_encryptionKey, _signingKey) = ResolveKeys(opts);
            _logger?.LogInformation("QR code cryptographic keys reloaded due to configuration change.");
        });
    }

    public async Task<VehicleQrCodeResult> GetVehicleQrCodeAsync(Guid vehicleId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (vehicleId == Guid.Empty)
        {
            throw new ArgumentException("VehicleId is required for QR code generation.", nameof(vehicleId));
        }

        var options = _optionsMonitor.CurrentValue;
        var cacheKey = GetCacheKey(vehicleId);
        if (_cache.TryGetValue<VehicleQrCacheEntry>(cacheKey, out var cached) && cached != null && cached.PayloadExpiresAt > DateTime.UtcNow)
        {
            return BuildResultFromCache(cached);
        }

        var now = DateTime.UtcNow;
        var payload = new VehicleQrPayload
        {
            VehicleId = vehicleId,
            Token = GenerateToken(options.TokenLength),
            IssuedAt = now,
            ExpiresAt = now.AddMinutes(Math.Max(1, options.ExpirationMinutes)),
            Version = 1
        };

        var encrypted = EncryptPayload(payload);
        var image = GenerateQrImage(encrypted, options);

        cached = new VehicleQrCacheEntry
        {
            VehicleId = vehicleId,
            Payload = payload,
            PayloadExpiresAt = payload.ExpiresAt,
            EncryptedPayload = encrypted,
            GeneratedAt = now,
            ImageBytes = image.ImageBytes,
            DataUrl = image.DataUrl
        };

        var cacheLifetimeMinutes = Math.Max(options.CacheMinutes, options.ExpirationMinutes);
        var absoluteExpiration = TimeSpan.FromMinutes(Math.Max(1, cacheLifetimeMinutes));

        _cache.Set(cacheKey, cached, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = absoluteExpiration
        });

        _logger.LogInformation("Generated QR code for vehicle {VehicleId} (expires at {ExpiresAt:O})", vehicleId, payload.ExpiresAt);

        return BuildResultFromCache(cached);
    }

    public async Task<QrCodeValidationResponseDto> ValidateAsync(QrCodeValidationRequestDto request, Guid userId, CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            throw new ArgumentException("Request payload is required.");
        }

        if (string.IsNullOrWhiteSpace(request.QrCodeData))
        {
            throw new ArgumentException("QR code data must be provided.");
        }

        var options = _optionsMonitor.CurrentValue;
        var payload = DecryptPayload(request.QrCodeData.Trim());

        if (payload.ExpiresAt < DateTime.UtcNow)
        {
            throw new ArgumentException("QR code has expired.");
        }

        var cacheKey = GetCacheKey(payload.VehicleId);
        if (!_cache.TryGetValue<VehicleQrCacheEntry>(cacheKey, out var cached) || cached == null)
        {
            throw new ArgumentException("QR code is no longer valid or has been rotated.");
        }

        if (!string.Equals(cached.Payload.Token, payload.Token, StringComparison.Ordinal) ||
            cached.PayloadExpiresAt < DateTime.UtcNow)
        {
            throw new ArgumentException("QR code is no longer valid or has been rotated.");
        }

        var action = ParseAction(request.Action);

        var now = DateTime.UtcNow;
        BookingEntity? booking;

        if (action == QrValidationAction.Checkout)
        {
            var windowStart = now.AddMinutes(-options.CheckoutLeadMinutes);
            var windowEnd = now.AddMinutes(options.CheckoutGraceMinutes);

            booking = await _bookingRepository.GetBookingForCheckoutWindowAsync(payload.VehicleId, userId, windowStart, windowEnd, cancellationToken)
                      ?? throw new ArgumentException("No active booking found for checkout within the allowed window.");
        }
        else
        {
            var windowEnd = now.AddMinutes(options.CheckinGraceMinutes);

            booking = await _bookingRepository.GetBookingForCheckinWindowAsync(payload.VehicleId, userId, now, windowEnd, cancellationToken);

            if (booking == null || now < booking.StartAt)
            {
                throw new ArgumentException("No active booking found for check-in.");
            }
        }

        var bookingDto = MapBookingToDto(booking);

        return new QrCodeValidationResponseDto
        {
            IsValid = true,
            Message = "QR code validated successfully.",
            Action = action.ToString().ToLowerInvariant(),
            Booking = bookingDto,
            ValidatedAt = now,
            CheckoutWindowOpensAt = action == QrValidationAction.Checkout ? booking!.StartAt.AddMinutes(-options.CheckoutLeadMinutes) : null,
            CheckoutWindowClosesAt = action == QrValidationAction.Checkout ? booking!.StartAt.AddMinutes(options.CheckoutGraceMinutes) : null,
            CheckinWindowClosesAt = action == QrValidationAction.Checkin ? booking!.EndAt.AddMinutes(options.CheckinGraceMinutes) : null
        };
    }

    private VehicleQrCodeResult BuildResultFromCache(VehicleQrCacheEntry cached)
        => new(cached.VehicleId, cached.ImageBytes, cached.DataUrl, cached.EncryptedPayload, cached.PayloadExpiresAt);

    private string EncryptPayload(VehicleQrPayload payload)
    {
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(payload, SerializerOptions);

        Span<byte> iv = stackalloc byte[12];
        RandomNumberGenerator.Fill(iv);

        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(_encryptionKey, tag.Length);
        aes.Encrypt(iv, plaintext, ciphertext, tag, _signingKey);

        var buffer = new byte[iv.Length + tag.Length + ciphertext.Length];
        iv.CopyTo(buffer);
        tag.CopyTo(buffer.AsSpan(iv.Length));
        ciphertext.CopyTo(buffer.AsSpan(iv.Length + tag.Length));

        return Convert.ToBase64String(buffer);
    }

    private VehicleQrPayload DecryptPayload(string data)
    {
        var normalized = NormalizeQrPayload(data);
        byte[] buffer;
        try
        {
            buffer = Convert.FromBase64String(normalized);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("QR code data is not valid base64.", ex);
        }

        if (buffer.Length < 29)
        {
            throw new ArgumentException("QR code payload is malformed.");
        }

        var iv = buffer.AsSpan(0, 12);
        var tag = buffer.AsSpan(12, 16);
        var ciphertext = buffer.AsSpan(28);

        var plaintext = new byte[ciphertext.Length];

        try
        {
            using var aes = new AesGcm(_encryptionKey, tag.Length);
            aes.Decrypt(iv, ciphertext, tag, plaintext, _signingKey);
        }
        catch (CryptographicException ex)
        {
            throw new ArgumentException("Unable to decrypt QR code payload.", ex);
        }

        try
        {
            return JsonSerializer.Deserialize<VehicleQrPayload>(plaintext, SerializerOptions)
                   ?? throw new ArgumentException("QR code payload cannot be deserialized.");
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("QR code payload format is invalid.", ex);
        }
    }

    private static string NormalizeQrPayload(string data)
    {
        var trimmed = data.Trim();
        if (trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var commaIndex = trimmed.IndexOf(',');
            if (commaIndex < 0 || commaIndex == trimmed.Length - 1)
            {
                throw new ArgumentException("QR data URL is invalid.");
            }

            trimmed = trimmed[(commaIndex + 1)..];
        }

        var builder = new StringBuilder(trimmed.Length);
        foreach (var ch in trimmed)
        {
            if (!char.IsWhiteSpace(ch))
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }

    private VehicleQrImage GenerateQrImage(string encryptedPayload, QrCodeOptions options)
    {
        using var generator = new QRCodeGenerator();
        using var qrData = generator.CreateQrCode(encryptedPayload, QRCodeGenerator.ECCLevel.Q);

        var foreground = ParseColor(options.ForegroundColorHex, 0, 0, 0);
        var background = ParseColor(options.BackgroundColorHex, 255, 255, 255);

        var qrCode = new PngByteQRCode(qrData);
        var pngBytes = qrCode.GetGraphic(
            Math.Max(1, options.PixelsPerModule),
            foreground,
            background,
            drawQuietZones: options.DrawQuietZones);

        var dataUrl = $"data:image/png;base64,{Convert.ToBase64String(pngBytes)}";

        return new VehicleQrImage(pngBytes, dataUrl);
    }

    private static byte[] ParseColor(string colorHex, byte defaultR, byte defaultG, byte defaultB)
    {
        if (string.IsNullOrWhiteSpace(colorHex))
        {
            return new[] { defaultR, defaultG, defaultB };
        }

        var sanitized = colorHex.Trim().TrimStart('#');
        if (sanitized.Length != 6)
        {
            return new[] { defaultR, defaultG, defaultB };
        }

        try
        {
            var r = Convert.ToByte(sanitized[..2], 16);
            var g = Convert.ToByte(sanitized.Substring(2, 2), 16);
            var b = Convert.ToByte(sanitized.Substring(4, 2), 16);
            return new[] { r, g, b };
        }
        catch
        {
            return new[] { defaultR, defaultG, defaultB };
        }
    }

    private static QrValidationAction ParseAction(string? action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            throw new ArgumentException("QR validation action is required.");
        }

        var normalized = action.Trim().ToLowerInvariant();
        return normalized switch
        {
            "checkout" => QrValidationAction.Checkout,
            "checkin" => QrValidationAction.Checkin,
            _ => throw new ArgumentException("Unsupported QR validation action. Use 'checkout' or 'checkin'.")
        };
    }

    private static string GenerateToken(int length)
    {
        var effectiveLength = Math.Clamp(length, 8, 64);
        var buffer = new byte[effectiveLength];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToHexString(buffer);
    }

    private static (byte[] EncryptionKey, byte[] SigningKey) ResolveKeys(QrCodeOptions options)
    {
        var encryptionKey = DecodeKey(options.EncryptionKey, nameof(options.EncryptionKey));
        var signingKeySource = string.IsNullOrWhiteSpace(options.SigningKey) ? options.EncryptionKey : options.SigningKey!;
        var signingKey = DecodeKey(signingKeySource, nameof(options.SigningKey));

        return (encryptionKey, signingKey);
    }

    private static byte[] DecodeKey(string value, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"QR code option '{propertyName}' must be configured.");
        }

        byte[] key;
        try
        {
            key = Convert.FromBase64String(value);
        }
        catch (FormatException ex)
        {
            // Fallback to UTF8
            key = Encoding.UTF8.GetBytes(value);
            if (key.Length != 16 && key.Length != 24 && key.Length != 32)
            {
                throw new InvalidOperationException($"QR code option '{propertyName}' must be a base64 string or raw key with 16, 24, or 32 bytes.", ex);
            }
            return key;
        }

        if (key.Length != 16 && key.Length != 24 && key.Length != 32)
        {
            throw new InvalidOperationException($"QR code option '{propertyName}' must decode to 16, 24, or 32 bytes.");
        }

        return key;
    }

    private static string GetCacheKey(Guid vehicleId) => $"vehicle-qr::{vehicleId:N}";

    private static BookingDto MapBookingToDto(BookingEntity booking)
    {
        return new BookingDto
        {
            Id = booking.Id,
            VehicleId = booking.VehicleId,
            VehicleModel = string.Empty,
            VehiclePlateNumber = string.Empty,
            GroupId = booking.GroupId,
            GroupName = string.Empty,
            UserId = booking.UserId,
            UserFirstName = string.Empty,
            UserLastName = string.Empty,
            StartAt = booking.StartAt,
            EndAt = booking.EndAt,
            Status = booking.Status,
            PriorityScore = booking.PriorityScore,
            Notes = booking.Notes,
            Purpose = booking.Purpose,
            IsEmergency = booking.IsEmergency,
            Priority = booking.Priority,
            RequiresDamageReview = booking.RequiresDamageReview,
            CreatedAt = booking.CreatedAt
        };
    }

    private record VehicleQrPayload
    {
        public Guid VehicleId { get; set; }
        public string Token { get; set; } = string.Empty;
        public DateTime IssuedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public int Version { get; set; }
    }

    private record VehicleQrImage(byte[] ImageBytes, string DataUrl);

    private sealed class VehicleQrCacheEntry
    {
        public Guid VehicleId { get; init; }
        public VehicleQrPayload Payload { get; init; } = new();
        public DateTime PayloadExpiresAt { get; init; }
        public DateTime GeneratedAt { get; init; }
        public string EncryptedPayload { get; init; } = string.Empty;
        public byte[] ImageBytes { get; init; } = Array.Empty<byte>();
        public string DataUrl { get; init; } = string.Empty;
    }

    private enum QrValidationAction
    {
        Checkout,
        Checkin
    }
}
