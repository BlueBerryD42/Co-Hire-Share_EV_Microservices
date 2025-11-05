using CoOwnershipVehicle.Domain.Entities;

namespace CoOwnershipVehicle.Group.Api.Services.Interfaces;

public interface INotificationService
{
    /// <summary>
    /// Send signature reminder to a signer
    /// </summary>
    Task<bool> SendSignatureReminderAsync(
        User signer,
        Document document,
        string signingUrl,
        ReminderType reminderType,
        string? customMessage = null);

    /// <summary>
    /// Send notification when signature is provided
    /// </summary>
    Task<bool> SendSignatureProvidedNotificationAsync(
        User documentOwner,
        User signer,
        Document document);

    /// <summary>
    /// Send notification when all signatures are collected
    /// </summary>
    Task<bool> SendAllSignedNotificationAsync(
        List<User> signers,
        User documentOwner,
        Document document);

    /// <summary>
    /// Send notification to next signer (sequential mode)
    /// </summary>
    Task<bool> SendNextSignerTurnNotificationAsync(
        User nextSigner,
        Document document,
        string signingUrl,
        int currentSignatureCount,
        int totalSigners);

    /// <summary>
    /// Send notification when signature is expiring
    /// </summary>
    Task<bool> SendSignatureExpiringNotificationAsync(
        User signer,
        Document document,
        string signingUrl,
        int daysRemaining);

    /// <summary>
    /// Send notification when signature has expired
    /// </summary>
    Task<bool> SendSignatureExpiredNotificationAsync(
        User signer,
        User documentOwner,
        Document document);

    /// <summary>
    /// Send document deleted notification
    /// </summary>
    Task<bool> SendDocumentDeletedNotificationAsync(
        List<User> groupAdmins,
        Document document,
        User deletedBy);

    /// <summary>
    /// Send document restored notification
    /// </summary>
    Task<bool> SendDocumentRestoredNotificationAsync(
        List<User> groupAdmins,
        Document document,
        User restoredBy);
}
