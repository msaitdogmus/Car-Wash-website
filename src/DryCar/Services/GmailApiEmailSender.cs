using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Microsoft.Extensions.Configuration;
using MimeKit;

namespace DryCar.Services;

public class GmailApiEmailSender : IEmailSender
{
    private readonly IConfiguration _config;

    public GmailApiEmailSender(IConfiguration config)
    {
        _config = config;
    }

    public async Task SendEmailAsync(string to, string subject, string htmlBody)
    {
        IConfigurationSection gmail = _config.GetSection("Gmail");
        string fromEmail = gmail["FromEmail"];
        string clientId = gmail["ClientId"];
        string clientSecret = gmail["ClientSecret"];
        string refreshToken = gmail["RefreshToken"];
        if (string.IsNullOrWhiteSpace(fromEmail) || string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret) || string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new InvalidOperationException("Gmail ayarları eksik. appsettings.json içindeki Gmail bölümünü kontrol edin.");
        }
        GoogleAuthorizationCodeFlow.Initializer initializer = new GoogleAuthorizationCodeFlow.Initializer();
        initializer.ClientSecrets = new ClientSecrets
        {
            ClientId = clientId,
            ClientSecret = clientSecret
        };
        initializer.Scopes = new string[1] { GmailService.Scope.GmailSend };
        GoogleAuthorizationCodeFlow flow = new GoogleAuthorizationCodeFlow(initializer);
        TokenResponse token = new TokenResponse
        {
            RefreshToken = refreshToken
        };
        UserCredential credential = new UserCredential(flow, fromEmail, token);
        if (!(await credential.RefreshTokenAsync(CancellationToken.None)))
        {
            throw new InvalidOperationException("Refresh token ile access token alınamadı. RefreshToken geçersiz olabilir.");
        }
        GmailService service = new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "DryCar"
        });
        MimeMessage mime = new MimeMessage();
        mime.From.Add(MailboxAddress.Parse(fromEmail));
        mime.To.Add(MailboxAddress.Parse(to));
        mime.Subject = subject;
        BodyBuilder builder = new BodyBuilder
        {
            HtmlBody = htmlBody,
            TextBody = "DRYCAR bildirimi: Lütfen uygulamaya girerek detayları görüntüleyin."
        };
        mime.Body = builder.ToMessageBody();
        using MemoryStream ms = new MemoryStream();
        await mime.WriteToAsync(ms);
        string raw = Convert.ToBase64String(ms.ToArray()).Replace('+', '-').Replace('/', '_')
            .Replace("=", "");
        Message msg = new Message
        {
            Raw = raw
        };
        await service.Users.Messages.Send(msg, "me").ExecuteAsync();
    }
}
