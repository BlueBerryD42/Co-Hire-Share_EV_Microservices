namespace CoOwnershipVehicle.Booking.Api.Configuration;

public class QrCodeOptions
{
    public const string SectionName = "QrCode";

    /// <summary>
    /// Base64 encoded encryption key used for QR payload encryption (16, 24, or 32 bytes once decoded).
    /// </summary>
    public string EncryptionKey { get; set; } = string.Empty;

    /// <summary>
    /// Optional base64 encoded HMAC key for future expansion. Defaults to EncryptionKey if not provided.
    /// </summary>
    public string? SigningKey { get; set; }

    /// <summary>
    /// Minutes a generated QR code remains valid for validation.
    /// </summary>
    public int ExpirationMinutes { get; set; } = 60;

    /// <summary>
    /// Minutes QR images are cached in memory to avoid regeneration.
    /// </summary>
    public int CacheMinutes { get; set; } = 30;

    /// <summary>
    /// Minutes before the booking start time that checkout can be validated.
    /// </summary>
    public int CheckoutLeadMinutes { get; set; } = 30;

    /// <summary>
    /// Minutes after the booking start time that checkout validation remains valid.
    /// </summary>
    public int CheckoutGraceMinutes { get; set; } = 30;

    /// <summary>
    /// Minutes after the booking end time that check-in validation remains valid.
    /// </summary>
    public int CheckinGraceMinutes { get; set; } = 60;

    /// <summary>
    /// Number of random bytes used for the validation token.
    /// </summary>
    public int TokenLength { get; set; } = 16;

    /// <summary>
    /// Pixel size for QR code generation.
    /// </summary>
    public int PixelsPerModule { get; set; } = 10;

    /// <summary>
    /// Background color of the QR code in hex (RRGGBB).
    /// </summary>
    public string BackgroundColorHex { get; set; } = "FFFFFF";

    /// <summary>
    /// Foreground color of the QR code in hex (RRGGBB).
    /// </summary>
    public string ForegroundColorHex { get; set; } = "000000";

    /// <summary>
    /// Whether to include quiet zones when rendering the QR image.
    /// </summary>
    public bool DrawQuietZones { get; set; } = true;
}
