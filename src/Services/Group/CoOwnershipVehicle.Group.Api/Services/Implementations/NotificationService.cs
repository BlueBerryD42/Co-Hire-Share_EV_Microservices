using CoOwnershipVehicle.Domain.Entities;
using CoOwnershipVehicle.Group.Api.Services.Interfaces;
using CoOwnershipVehicle.Shared.Contracts.DTOs;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace CoOwnershipVehicle.Group.Api.Services.Implementations;

public class NotificationService : INotificationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificationService> _logger;
    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly string _smtpUsername;
    private readonly string _smtpPassword;
    private readonly string _fromEmail;
    private readonly string _fromName;
    private readonly bool _enableSsl;

    public NotificationService(IConfiguration configuration, ILogger<NotificationService> logger)
    {
        _configuration = configuration;
        _logger = logger;

        // Load email configuration
        _smtpHost = _configuration["EmailSettings:SmtpHost"] ?? "smtp.gmail.com";
        _smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
        _smtpUsername = _configuration["EmailSettings:Username"] ?? "";
        _smtpPassword = _configuration["EmailSettings:Password"] ?? "";
        _fromEmail = _configuration["EmailSettings:FromEmail"] ?? "noreply@coownership.com";
        _fromName = _configuration["EmailSettings:FromName"] ?? "Co-Ownership Vehicle System";
        _enableSsl = bool.Parse(_configuration["EmailSettings:EnableSsl"] ?? "true");
    }

    public async Task<bool> SendSignatureReminderAsync(
        UserInfoDto signer,
        Document document,
        string signingUrl,
        ReminderType reminderType,
        string? customMessage = null)
    {
        try
        {
            var subject = reminderType switch
            {
                ReminderType.ThreeDaysBefore => $"⏰ Reminder: Document Signature Due in 3 Days - {document.FileName}",
                ReminderType.OneDayBefore => $"⚠️ Urgent: Document Signature Due Tomorrow - {document.FileName}",
                ReminderType.Overdue => $"🔴 Overdue: Document Signature Required - {document.FileName}",
                ReminderType.Manual => $"📄 Signature Reminder: {document.FileName}",
                _ => $"📝 Signature Required: {document.FileName}"
            };

            var body = GenerateSignatureReminderEmail(signer, document, signingUrl, reminderType, customMessage);

            return await SendEmailAsync(signer.Email, subject, body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send signature reminder to {Email}", signer.Email);
            return false;
        }
    }

    public async Task<bool> SendSignatureProvidedNotificationAsync(
        UserInfoDto documentOwner,
        UserInfoDto signer,
        Document document)
    {
        try
        {
            var subject = $"✅ Document Signed: {document.FileName}";
            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #4CAF50; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
        .content {{ background-color: #f9f9f9; padding: 20px; border-radius: 0 0 5px 5px; }}
        .button {{ display: inline-block; padding: 10px 20px; background-color: #4CAF50; color: white; text-decoration: none; border-radius: 5px; margin: 10px 0; }}
        .info-box {{ background-color: white; padding: 15px; border-left: 4px solid #4CAF50; margin: 15px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h2>✅ Document Signature Received</h2>
        </div>
        <div class='content'>
            <p>Hi {documentOwner.FirstName},</p>

            <p><strong>{signer.FirstName} {signer.LastName}</strong> has signed your document!</p>

            <div class='info-box'>
                <strong>Document:</strong> {document.FileName}<br>
                <strong>Signed by:</strong> {signer.FirstName} {signer.LastName} ({signer.Email})<br>
                <strong>Signed at:</strong> {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC
            </div>

            <p>You can view the document details and signature status in your dashboard.</p>

            <p>Best regards,<br>
            Co-Ownership Vehicle Team</p>
        </div>
    </div>
</body>
</html>";

            return await SendEmailAsync(documentOwner.Email, subject, body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send signature provided notification");
            return false;
        }
    }

    public async Task<bool> SendAllSignedNotificationAsync(
        List<UserInfoDto> signers,
        UserInfoDto documentOwner,
        Document document)
    {
        try
        {
            var subject = $"🎉 All Signatures Collected: {document.FileName}";

            var recipients = signers.Select(s => s.Email).ToList();
            if (!recipients.Contains(documentOwner.Email))
            {
                recipients.Add(documentOwner.Email);
            }

            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #2196F3; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
        .content {{ background-color: #f9f9f9; padding: 20px; border-radius: 0 0 5px 5px; }}
        .success-box {{ background-color: #E8F5E9; padding: 20px; border-radius: 5px; text-align: center; margin: 20px 0; }}
        .button {{ display: inline-block; padding: 12px 30px; background-color: #2196F3; color: white; text-decoration: none; border-radius: 5px; margin: 10px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h2>🎉 Document Fully Signed!</h2>
        </div>
        <div class='content'>
            <div class='success-box'>
                <h3>Congratulations!</h3>
                <p><strong>{document.FileName}</strong> has been signed by all parties.</p>
            </div>

            <p>All required signatures have been collected. You can now download the signing certificate.</p>

            <p><strong>Total Signatures:</strong> {signers.Count}</p>
            <p><strong>Completed at:</strong> {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC</p>

            <p>The signing certificate is available for download from your dashboard.</p>

            <p>Best regards,<br>
            Co-Ownership Vehicle Team</p>
        </div>
    </div>
</body>
</html>";

            // Send to all recipients
            var results = await Task.WhenAll(
                recipients.Select(email => SendEmailAsync(email, subject, body)));

            return results.All(r => r);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send all signed notification");
            return false;
        }
    }

    public async Task<bool> SendNextSignerTurnNotificationAsync(
        UserInfoDto nextSigner,
        Document document,
        string signingUrl,
        int currentSignatureCount,
        int totalSigners)
    {
        try
        {
            var subject = $"📝 Your Turn to Sign: {document.FileName}";
            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #FF9800; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
        .content {{ background-color: #f9f9f9; padding: 20px; border-radius: 0 0 5px 5px; }}
        .button {{ display: inline-block; padding: 12px 30px; background-color: #FF9800; color: white; text-decoration: none; border-radius: 5px; margin: 10px 0; }}
        .progress {{ background-color: #E0E0E0; height: 20px; border-radius: 10px; overflow: hidden; }}
        .progress-bar {{ background-color: #4CAF50; height: 100%; text-align: center; color: white; line-height: 20px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h2>📝 It's Your Turn to Sign</h2>
        </div>
        <div class='content'>
            <p>Hi {nextSigner.FirstName},</p>

            <p>Previous signers have completed their signatures. It's now your turn to sign the document.</p>

            <p><strong>Document:</strong> {document.FileName}</p>

            <div style='margin: 20px 0;'>
                <p><strong>Signing Progress:</strong></p>
                <div class='progress'>
                    <div class='progress-bar' style='width: {(double)currentSignatureCount / totalSigners * 100}%;'>
                        {currentSignatureCount}/{totalSigners} Signed
                    </div>
                </div>
            </div>

            <p style='text-align: center;'>
                <a href='{signingUrl}' class='button'>Sign Document Now</a>
            </p>

            <p>Please complete your signature to keep the process moving forward.</p>

            <p>Best regards,<br>
            Co-Ownership Vehicle Team</p>
        </div>
    </div>
</body>
</html>";

            return await SendEmailAsync(nextSigner.Email, subject, body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send next signer turn notification");
            return false;
        }
    }

    public async Task<bool> SendSignatureExpiringNotificationAsync(
        UserInfoDto signer,
        Document document,
        string signingUrl,
        int daysRemaining)
    {
        try
        {
            var subject = $"⚠️ Signature Expiring in {daysRemaining} Day(s): {document.FileName}";
            var body = GenerateExpiringNotificationEmail(signer, document, signingUrl, daysRemaining);

            return await SendEmailAsync(signer.Email, subject, body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send signature expiring notification");
            return false;
        }
    }

    public async Task<bool> SendSignatureExpiredNotificationAsync(
        UserInfoDto signer,
        UserInfoDto documentOwner,
        Document document)
    {
        try
        {
            var subject = $"🔴 Signature Expired: {document.FileName}";

            // Notify signer
            var signerBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #F44336; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
        .content {{ background-color: #f9f9f9; padding: 20px; border-radius: 0 0 5px 5px; }}
        .warning-box {{ background-color: #FFEBEE; padding: 15px; border-left: 4px solid #F44336; margin: 15px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h2>🔴 Signature Request Expired</h2>
        </div>
        <div class='content'>
            <p>Hi {signer.FirstName},</p>

            <div class='warning-box'>
                <p>The signature request for <strong>{document.FileName}</strong> has expired.</p>
            </div>

            <p>Please contact the document owner if you still need to sign this document.</p>

            <p>Best regards,<br>
            Co-Ownership Vehicle Team</p>
        </div>
    </div>
</body>
</html>";

            // Notify document owner
            var ownerBody = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #F44336; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
        .content {{ background-color: #f9f9f9; padding: 20px; border-radius: 0 0 5px 5px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h2>🔴 Signature Request Expired</h2>
        </div>
        <div class='content'>
            <p>Hi {documentOwner.FirstName},</p>

            <p>The signature request for <strong>{document.FileName}</strong> has expired.</p>

            <p><strong>Pending Signer:</strong> {signer.FirstName} {signer.LastName} ({signer.Email})</p>

            <p>You may need to resend the signature request or contact the signer directly.</p>

            <p>Best regards,<br>
            Co-Ownership Vehicle Team</p>
        </div>
    </div>
</body>
</html>";

            var signerTask = SendEmailAsync(signer.Email, subject, signerBody);
            var ownerTask = SendEmailAsync(documentOwner.Email, subject, ownerBody);

            var results = await Task.WhenAll(signerTask, ownerTask);
            return results.All(r => r);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send signature expired notification");
            return false;
        }
    }

    public async Task<bool> SendDocumentDeletedNotificationAsync(
        List<UserInfoDto> groupAdmins,
        Document document,
        UserInfoDto deletedBy)
    {
        try
        {
            var subject = $"🗑️ Document Deleted: {document.FileName}";
            var body = $@"
<!DOCTYPE html>
<html>
<body style='font-family: Arial, sans-serif;'>
    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
        <h2>Document Deleted</h2>
        <p><strong>{document.FileName}</strong> has been deleted.</p>
        <p><strong>Deleted by:</strong> {deletedBy.FirstName} {deletedBy.LastName}</p>
        <p><strong>Deleted at:</strong> {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC</p>
        <p>The document can be restored within 30 days from the deleted documents view.</p>
    </div>
</body>
</html>";

            var tasks = groupAdmins.Select(admin => SendEmailAsync(admin.Email, subject, body));
            var results = await Task.WhenAll(tasks);
            return results.All(r => r);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send document deleted notification");
            return false;
        }
    }

    public async Task<bool> SendDocumentRestoredNotificationAsync(
        List<UserInfoDto> groupAdmins,
        Document document,
        UserInfoDto restoredBy)
    {
        try
        {
            var subject = $"♻️ Document Restored: {document.FileName}";
            var body = $@"
<!DOCTYPE html>
<html>
<body style='font-family: Arial, sans-serif;'>
    <div style='max-width: 600px; margin: 0 auto; padding: 20px;'>
        <h2>Document Restored</h2>
        <p><strong>{document.FileName}</strong> has been restored.</p>
        <p><strong>Restored by:</strong> {restoredBy.FirstName} {restoredBy.LastName}</p>
        <p><strong>Restored at:</strong> {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC</p>
        <p>The document is now available in the normal document list.</p>
    </div>
</body>
</html>";

            var tasks = groupAdmins.Select(admin => SendEmailAsync(admin.Email, subject, body));
            var results = await Task.WhenAll(tasks);
            return results.All(r => r);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send document restored notification");
            return false;
        }
    }

    public async Task<bool> SendProposalStartedNotificationAsync(
        List<UserInfoDto> groupMembers,
        string proposalTitle,
        Guid proposalId,
        Guid groupId,
        string groupName,
        DateTime votingEndDate,
        string proposalUrl)
    {
        try
        {
            var subject = $"🗳️ New Proposal: {proposalTitle}";
            var votingEndDateStr = votingEndDate.ToString("dd/MM/yyyy HH:mm");
            
            var tasks = groupMembers.Select(member =>
            {
                var body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #2196F3; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
        .content {{ background-color: #f9f9f9; padding: 20px; border-radius: 0 0 5px 5px; }}
        .button {{ display: inline-block; padding: 12px 24px; background-color: #2196F3; color: white; text-decoration: none; border-radius: 5px; margin: 15px 0; }}
        .info-box {{ background-color: white; padding: 15px; border-left: 4px solid #2196F3; margin: 15px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h2>🗳️ New Proposal Started</h2>
        </div>
        <div class='content'>
            <p>Hi {member.FirstName},</p>
            
            <p>A new proposal has been created in your group <strong>{groupName}</strong> and voting has started!</p>
            
            <div class='info-box'>
                <strong>Proposal:</strong> {proposalTitle}<br>
                <strong>Group:</strong> {groupName}<br>
                <strong>Voting ends:</strong> {votingEndDateStr}
            </div>
            
            <p>Please review the proposal and cast your vote before the voting period ends.</p>
            
            <a href='{proposalUrl}' class='button'>View & Vote on Proposal</a>
            
            <p style='margin-top: 20px; font-size: 12px; color: #666;'>
                This is an automated notification from the Co-Ownership Vehicle System.
            </p>
        </div>
    </div>
</body>
</html>";
                return SendEmailAsync(member.Email, subject, body);
            });
            
            var results = await Task.WhenAll(tasks);
            return results.All(r => r);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send proposal started notification");
            return false;
        }
    }

    public async Task<bool> SendProposalPassedNotificationAsync(
        List<UserInfoDto> groupAdmins,
        string proposalTitle,
        Guid proposalId,
        Guid groupId,
        string groupName,
        string proposalType,
        decimal? amount,
        string proposalUrl)
    {
        try
        {
            var subject = $"✅ Proposal Passed: {proposalTitle} - Action Required";
            var amountText = amount.HasValue ? amount.Value.ToString("N0") + " ₫" : "N/A";
            
            var tasks = groupAdmins.Select(admin =>
            {
                var body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #4CAF50; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
        .content {{ background-color: #f9f9f9; padding: 20px; border-radius: 0 0 5px 5px; }}
        .button {{ display: inline-block; padding: 12px 24px; background-color: #4CAF50; color: white; text-decoration: none; border-radius: 5px; margin: 15px 0; }}
        .info-box {{ background-color: white; padding: 15px; border-left: 4px solid #4CAF50; margin: 15px 0; }}
        .action-box {{ background-color: #FFF3CD; padding: 15px; border-left: 4px solid #FFC107; margin: 15px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h2> Proposal Passed</h2>
        </div>
        <div class='content'>
            <p>Hi {admin.FirstName},</p>
            
            <p>The proposal in your group <strong>{groupName}</strong> has been approved by the members!</p>
            
            <div class='info-box'>
                <strong>Proposal:</strong> {proposalTitle}<br>
                <strong>Type:</strong> {proposalType}<br>
                <strong>Amount:</strong> {amountText}<br>
                <strong>Group:</strong> {groupName}
            </div>
            
            <div class='action-box'>
                <strong>⚠️ Action Required:</strong> As a group admin, you need to take action on this approved proposal. 
                Please review the proposal details and proceed with the necessary steps.
            </div>
            
            <a href='{proposalUrl}' class='button'>View Proposal Details</a>
            
            <p style='margin-top: 20px; font-size: 12px; color: #666;'>
                This is an automated notification from the Co-Ownership Vehicle System.
            </p>
        </div>
    </div>
</body>
</html>";
                return SendEmailAsync(admin.Email, subject, body);
            });
            
            var results = await Task.WhenAll(tasks);
            return results.All(r => r);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send proposal passed notification");
            return false;
        }
    }

    public async Task<bool> SendProposalVotingReminderAsync(
        UserInfoDto member,
        string proposalTitle,
        Guid proposalId,
        Guid groupId,
        string groupName,
        DateTime votingEndDate,
        string proposalUrl)
    {
        try
        {
            var subject = $"⏰ Reminder: Vote on Proposal - {proposalTitle}";
            var votingEndDateStr = votingEndDate.ToString("dd/MM/yyyy HH:mm");
            
            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #FF9800; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
        .content {{ background-color: #f9f9f9; padding: 20px; border-radius: 0 0 5px 5px; }}
        .button {{ display: inline-block; padding: 12px 24px; background-color: #FF9800; color: white; text-decoration: none; border-radius: 5px; margin: 15px 0; }}
        .info-box {{ background-color: white; padding: 15px; border-left: 4px solid #FF9800; margin: 15px 0; }}
        .urgent-box {{ background-color: #FFF3CD; padding: 15px; border-left: 4px solid #FFC107; margin: 15px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h2>⏰ Voting Reminder</h2>
        </div>
        <div class='content'>
            <p>Hi {member.FirstName},</p>
            
            <p>You haven't voted on a proposal in your group <strong>{groupName}</strong> yet!</p>
            
            <div class='urgent-box'>
                <strong>⏰ Voting ends in less than 12 hours!</strong>
            </div>
            
            <div class='info-box'>
                <strong>Proposal:</strong> {proposalTitle}<br>
                <strong>Group:</strong> {groupName}<br>
                <strong>Voting ends:</strong> {votingEndDateStr}
            </div>
            
            <p>Please cast your vote before the voting period ends. Your participation is important for the group's decision-making process.</p>
            
            <a href='{proposalUrl}' class='button'>Vote Now</a>
            
            <p style='margin-top: 20px; font-size: 12px; color: #666;'>
                This is an automated reminder from the Co-Ownership Vehicle System.
            </p>
        </div>
    </div>
</body>
</html>";
            
            return await SendEmailAsync(member.Email, subject, body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send proposal voting reminder to {Email}", member.Email);
            return false;
        }
    }

    #region Private Helper Methods

    private async Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        try
        {
            if (string.IsNullOrEmpty(_smtpUsername) || string.IsNullOrEmpty(_smtpPassword))
            {
                _logger.LogWarning("Email settings not configured. Email not sent to {Email}", toEmail);
                return false;
            }

            using var client = new SmtpClient(_smtpHost, _smtpPort)
            {
                Credentials = new NetworkCredential(_smtpUsername, _smtpPassword),
                EnableSsl = _enableSsl
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(_fromEmail, _fromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };

            mailMessage.To.Add(toEmail);

            await client.SendMailAsync(mailMessage);

            _logger.LogInformation("Email sent successfully to {Email}: {Subject}", toEmail, subject);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
            return false;
        }
    }

    private string GenerateSignatureReminderEmail(
        UserInfoDto signer,
        Document document,
        string signingUrl,
        ReminderType reminderType,
        string? customMessage)
    {
        var urgencyMessage = reminderType switch
        {
            ReminderType.ThreeDaysBefore => "<p style='color: #FF9800;'><strong>⏰ Reminder: This document is due in 3 days.</strong></p>",
            ReminderType.OneDayBefore => "<p style='color: #F44336;'><strong>⚠️ Urgent: This document is due tomorrow!</strong></p>",
            ReminderType.Overdue => "<p style='color: #D32F2F;'><strong>🔴 This signature request is now overdue!</strong></p>",
            _ => ""
        };

        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #2196F3; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
        .content {{ background-color: #f9f9f9; padding: 20px; border-radius: 0 0 5px 5px; }}
        .button {{ display: inline-block; padding: 12px 30px; background-color: #2196F3; color: white; text-decoration: none; border-radius: 5px; margin: 10px 0; }}
        .custom-message {{ background-color: #FFF3E0; padding: 15px; border-left: 4px solid #FF9800; margin: 15px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h2>📝 Document Signature Reminder</h2>
        </div>
        <div class='content'>
            <p>Hi {signer.FirstName},</p>

            {urgencyMessage}

            <p>You have a pending document that requires your signature:</p>

            <p><strong>Document:</strong> {document.FileName}</p>

            {(!string.IsNullOrEmpty(customMessage) ? $"<div class='custom-message'><p><strong>Message:</strong><br>{customMessage}</p></div>" : "")}

            <p style='text-align: center;'>
                <a href='{signingUrl}' class='button'>Sign Document Now</a>
            </p>

            <p>If you have any questions, please contact the document owner.</p>

            <p>Best regards,<br>
            Co-Ownership Vehicle Team</p>
        </div>
    </div>
</body>
</html>";
    }

    private string GenerateExpiringNotificationEmail(
        UserInfoDto signer,
        Document document,
        string signingUrl,
        int daysRemaining)
    {
        var urgencyClass = daysRemaining <= 1 ? "background-color: #F44336;" : "background-color: #FF9800;";

        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ {urgencyClass} color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
        .content {{ background-color: #f9f9f9; padding: 20px; border-radius: 0 0 5px 5px; }}
        .button {{ display: inline-block; padding: 12px 30px; background-color: #F44336; color: white; text-decoration: none; border-radius: 5px; margin: 10px 0; }}
        .warning-box {{ background-color: #FFEBEE; padding: 15px; border-left: 4px solid #F44336; margin: 15px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h2>⚠️ Signature Request Expiring Soon</h2>
        </div>
        <div class='content'>
            <p>Hi {signer.FirstName},</p>

            <div class='warning-box'>
                <p><strong>Urgent:</strong> Your signature request will expire in <strong>{daysRemaining} day(s)</strong>!</p>
            </div>

            <p><strong>Document:</strong> {document.FileName}</p>

            <p>Please sign this document as soon as possible to avoid expiration.</p>

            <p style='text-align: center;'>
                <a href='{signingUrl}' class='button'>Sign Document Now</a>
            </p>

            <p>Best regards,<br>
            Co-Ownership Vehicle Team</p>
        </div>
    </div>
</body>
</html>";
    }

    #endregion
}
