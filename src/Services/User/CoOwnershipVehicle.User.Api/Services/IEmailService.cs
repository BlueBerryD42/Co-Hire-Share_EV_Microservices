namespace CoOwnershipVehicle.User.Api.Services;

public interface IEmailService
{
    Task<bool> SendKycApprovedEmailAsync(string email, string firstName, string kycUrl);
    Task<bool> SendKycRejectedEmailAsync(string email, string firstName, string reason, string kycUrl);
    Task<bool> SendEmailAsync(string to, string subject, string body, bool isHtml = true);
}

