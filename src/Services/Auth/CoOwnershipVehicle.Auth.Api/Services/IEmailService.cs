using System.Threading.Tasks;

namespace CoOwnershipVehicle.Auth.Api.Services
{
    public interface IEmailService
    {
        Task<bool> SendEmailConfirmationAsync(string email, string confirmationLink);
        Task<bool> SendPasswordResetAsync(string email, string resetLink);
        Task<bool> SendWelcomeEmailAsync(string email, string firstName);
        Task<bool> SendEmailAsync(string to, string subject, string body, bool isHtml = true);
    }
}
