using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace DryCar.Services;

public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _config;

    public SmtpEmailSender(IConfiguration config)
    {
        _config = config;
    }

    public async Task SendEmailAsync(string to, string subject, string htmlBody)
    {
        IConfigurationSection smtp = _config.GetSection("Smtp");
        using SmtpClient client = new SmtpClient(smtp["Host"], int.Parse(smtp["Port"]))
        {
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(smtp["User"], smtp["Pass"]),
            EnableSsl = true
        };
        using MailMessage msg = new MailMessage
        {
            From = new MailAddress(smtp["User"], "DryCar Oto Yıkama"),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        msg.To.Add(to);
        await client.SendMailAsync(msg);
    }
}
