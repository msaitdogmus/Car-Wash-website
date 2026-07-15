using System.Threading.Tasks;

namespace DryCar.Services;

public interface IEmailSender
{
    Task SendEmailAsync(string to, string subject, string htmlBody);
}
