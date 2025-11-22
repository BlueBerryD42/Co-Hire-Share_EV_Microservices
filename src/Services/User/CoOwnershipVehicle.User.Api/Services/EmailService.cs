using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using CoOwnershipVehicle.Shared.Configuration;

namespace CoOwnershipVehicle.User.Api.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> SendKycApprovedEmailAsync(string email, string firstName, string kycUrl)
    {
        var subject = " KYC của bạn đã được phê duyệt";
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
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h2> KYC Đã Được Phê Duyệt</h2>
        </div>
        <div class='content'>
            <p>Xin chào {firstName},</p>
            
            <p>Chúng tôi vui mừng thông báo rằng <strong>KYC (Xác thực danh tính) của bạn đã được phê duyệt</strong>!</p>
            
            <div class='info-box'>
                <p><strong>Trạng thái:</strong> Đã phê duyệt</p>
                <p><strong>Ngày phê duyệt:</strong> {DateTime.UtcNow:dd/MM/yyyy HH:mm} UTC</p>
            </div>
            
            <p>Bạn có thể bắt đầu sử dụng đầy đủ các tính năng của hệ thống Co-Ownership Vehicle.</p>
            
            <a href='{kycUrl}' class='button'>Xem Hồ Sơ KYC</a>
            
            <p style='margin-top: 20px; font-size: 12px; color: #666;'>
                Đây là email tự động từ hệ thống Co-Ownership Vehicle.
            </p>
        </div>
    </div>
</body>
</html>";

        return await SendEmailAsync(email, subject, body);
    }

    public async Task<bool> SendKycRejectedEmailAsync(string email, string firstName, string reason, string kycUrl)
    {
        var subject = " KYC của bạn cần cập nhật";
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
        .action-box {{ background-color: #FFF3CD; padding: 15px; border-left: 4px solid #FFC107; margin: 15px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h2> KYC Cần Cập Nhật</h2>
        </div>
        <div class='content'>
            <p>Xin chào {firstName},</p>
            
            <p>Chúng tôi rất tiếc thông báo rằng <strong>KYC (Xác thực danh tính) của bạn cần được cập nhật</strong>.</p>
            
            <div class='info-box'>
                <p><strong>Trạng thái:</strong> Cần cập nhật</p>
                <p><strong>Ngày xem xét:</strong> {DateTime.UtcNow:dd/MM/yyyy HH:mm} UTC</p>
            </div>
            
            <div class='action-box'>
                <p><strong>Lý do:</strong></p>
                <p>{reason ?? "Tài liệu KYC không đáp ứng yêu cầu. Vui lòng kiểm tra và cập nhật lại."}</p>
            </div>
            
            <p><strong>Vui lòng thực hiện các bước sau:</strong></p>
            <ol>
                <li>Kiểm tra lại các tài liệu KYC đã upload</li>
                <li>Cập nhật các tài liệu không đạt yêu cầu</li>
                <li>Upload lại các tài liệu mới nếu cần</li>
                <li>Gửi lại để chúng tôi xem xét</li>
            </ol>
            
            <a href='{kycUrl}' class='button'>Cập Nhật KYC Ngay</a>
            
            <p style='margin-top: 20px; font-size: 12px; color: #666;'>
                Đây là email tự động từ hệ thống Co-Ownership Vehicle.
            </p>
        </div>
    </div>
</body>
</html>";

        return await SendEmailAsync(email, subject, body);
    }

    public async Task<bool> SendEmailAsync(string to, string subject, string body, bool isHtml = true)
    {
        try
        {
            var emailConfig = EnvironmentHelper.GetEmailConfigParams(_configuration);
            
            if (string.IsNullOrEmpty(emailConfig.SmtpHost) || string.IsNullOrEmpty(emailConfig.FromEmail))
            {
                _logger.LogWarning("Email configuration is incomplete. SmtpHost: '{SmtpHost}', FromEmail: '{FromEmail}'. Email not sent to {Email}", 
                    emailConfig.SmtpHost ?? "NULL", emailConfig.FromEmail ?? "NULL", to);
                return false;
            }

            using var client = new SmtpClient(emailConfig.SmtpHost, emailConfig.SmtpPort);
            client.EnableSsl = emailConfig.UseSsl;
            client.Credentials = new NetworkCredential(emailConfig.SmtpUsername, emailConfig.SmtpPassword);

            var message = new MailMessage
            {
                From = new MailAddress(emailConfig.FromEmail, emailConfig.FromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = isHtml
            };

            message.To.Add(to);

            await client.SendMailAsync(message);
            _logger.LogInformation("Email sent successfully to {Email}", to);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", to);
            return false;
        }
    }
}

