using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Shared.Contracts.DTOs;

namespace CoOwnershipVehicle.Group.Api.Services.Interfaces;

public interface INotificationService
{
    /// <summary>
    /// Send signature reminder to a signer
    /// </summary>
    Task<bool> SendSignatureReminderAsync(
        UserInfoDto signer,
        Document document,
        string signingUrl,
        ReminderType reminderType,
        string? customMessage = null);

    /// <summary>
    /// Send notification when signature is provided
    /// </summary>
    Task<bool> SendSignatureProvidedNotificationAsync(
        UserInfoDto documentOwner,
        UserInfoDto signer,
        Document document);

    /// <summary>
    /// Send notification when all signatures are collected
    /// </summary>
    Task<bool> SendAllSignedNotificationAsync(
        List<UserInfoDto> signers,
        UserInfoDto documentOwner,
        Document document);

    /// <summary>
    /// Send notification to next signer (sequential mode)
    /// </summary>
    Task<bool> SendNextSignerTurnNotificationAsync(
        UserInfoDto nextSigner,
        Document document,
        string signingUrl,
        int currentSignatureCount,
        int totalSigners);

    /// <summary>
    /// Send notification when signature is expiring
    /// </summary>
    Task<bool> SendSignatureExpiringNotificationAsync(
        UserInfoDto signer,
        Document document,
        string signingUrl,
        int daysRemaining);

    /// <summary>
    /// Send notification when signature has expired
    /// </summary>
    Task<bool> SendSignatureExpiredNotificationAsync(
        UserInfoDto signer,
        UserInfoDto documentOwner,
        Document document);

    /// <summary>
    /// Send document deleted notification
    /// </summary>
    Task<bool> SendDocumentDeletedNotificationAsync(
        List<UserInfoDto> groupAdmins,
        Document document,
        UserInfoDto deletedBy);

    /// <summary>
    /// Send document restored notification
    /// </summary>
    Task<bool> SendDocumentRestoredNotificationAsync(
        List<UserInfoDto> groupAdmins,
        Document document,
        UserInfoDto restoredBy);

    /// <summary>
    /// Send notification when a proposal starts voting
    /// </summary>
    Task<bool> SendProposalStartedNotificationAsync(
        List<UserInfoDto> groupMembers,
        string proposalTitle,
        Guid proposalId,
        Guid groupId,
        string groupName,
        DateTime votingEndDate,
        string proposalUrl);

    /// <summary>
    /// Send notification to group admins when a proposal passes
    /// </summary>
    Task<bool> SendProposalPassedNotificationAsync(
        List<UserInfoDto> groupAdmins,
        string proposalTitle,
        Guid proposalId,
        Guid groupId,
        string groupName,
        string proposalType,
        decimal? amount,
        string proposalUrl);

    /// <summary>
    /// Send reminder to members who haven't voted yet (12 hours before voting ends)
    /// </summary>
    Task<bool> SendProposalVotingReminderAsync(
        UserInfoDto member,
        string proposalTitle,
        Guid proposalId,
        Guid groupId,
        string groupName,
        DateTime votingEndDate,
        string proposalUrl);
}
