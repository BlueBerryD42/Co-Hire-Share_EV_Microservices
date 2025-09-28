using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using CoOwnershipVehicle.Shared.Configuration;

namespace CoOwnershipVehicle.Auth.Api.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<bool> SendEmailConfirmationAsync(string email, string confirmationLink)
        {
            var subject = "Confirm Your Email - Co-Ownership Vehicle";
            var body = $@"
                <html>
                <body style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                    <h2 style='color: #2c3e50;'>Welcome to Co-Ownership Vehicle!</h2>
                    <p>Thank you for registering with us. Please confirm your email address by clicking the button below:</p>
                    <div style='text-align: center; margin: 30px 0;'>
                        <a href='{confirmationLink}' 
                           style='background-color: #3498db; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block;'>
                            Confirm Email Address
                        </a>
                    </div>
                    <p>If the button doesn't work, you can copy and paste this link into your browser:</p>
                    <p style='word-break: break-all; color: #7f8c8d;'>{confirmationLink}</p>
                    <p>This link will expire in 24 hours.</p>
                    <hr style='margin: 30px 0; border: none; border-top: 1px solid #ecf0f1;'>
                    <p style='color: #7f8c8d; font-size: 12px;'>
                        If you didn't create an account with us, please ignore this email.
                    </p>
                </body>
                </html>";

            return await SendEmailAsync(email, subject, body);
        }

        public async Task<bool> SendPasswordResetAsync(string email, string resetLink)
        {
            var subject = "Reset Your Password - Co-Ownership Vehicle";
            var body = $@"
                <html>
                <body style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                    <h2 style='color: #2c3e50;'>Password Reset Request</h2>
                    <p>You requested to reset your password. Click the button below to create a new password:</p>
                    <div style='text-align: center; margin: 30px 0;'>
                        <a href='{resetLink}' 
                           style='background-color: #e74c3c; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block;'>
                            Reset Password
                        </a>
                    </div>
                    <p>If the button doesn't work, you can copy and paste this link into your browser:</p>
                    <p style='word-break: break-all; color: #7f8c8d;'>{resetLink}</p>
                    <p>This link will expire in 1 hour.</p>
                    <hr style='margin: 30px 0; border: none; border-top: 1px solid #ecf0f1;'>
                    <p style='color: #7f8c8d; font-size: 12px;'>
                        If you didn't request a password reset, please ignore this email.
                    </p>
                </body>
                </html>";

            return await SendEmailAsync(email, subject, body);
        }

        public async Task<bool> SendWelcomeEmailAsync(string email, string firstName)
        {
            var subject = "Welcome to Co-Ownership Vehicle!";
            var body = $@"
                <html>
                <body style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                    <h2 style='color: #2c3e50;'>Welcome {firstName}!</h2>
                    <p>Your email has been confirmed and your account is now active.</p>
                    <p>You can now:</p>
                    <ul>
                        <li>Access your dashboard</li>
                        <li>Manage your vehicle co-ownership</li>
                        <li>View booking schedules</li>
                        <li>Track expenses and payments</li>
                    </ul>
                    <div style='text-align: center; margin: 30px 0;'>
                        <a href='{GetFrontendUrl()}' 
                           style='background-color: #27ae60; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block;'>
                            Go to Dashboard
                        </a>
                    </div>
                    <p>Thank you for joining Co-Ownership Vehicle!</p>
                    <hr style='margin: 30px 0; border: none; border-top: 1px solid #ecf0f1;'>
                    <p style='color: #7f8c8d; font-size: 12px;'>
                        If you have any questions, please contact our support team.
                    </p>
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

        private string GetFrontendUrl() 
        {
            var emailConfig = EnvironmentHelper.GetEmailConfigParams(_configuration);
            return emailConfig.FrontendUrl;
        }
    }
}
