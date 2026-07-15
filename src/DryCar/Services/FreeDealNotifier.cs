using System;
using System.Net;
using System.Threading.Tasks;
using DryCar.Data;
using DryCar.Models;
using Microsoft.Extensions.Configuration;

namespace DryCar.Services;

public class FreeDealNotifier
{
    private readonly ApplicationDbContext _db;

    private readonly IEmailSender _email;

    private readonly IConfiguration _config;

    public FreeDealNotifier(ApplicationDbContext db, IEmailSender email, IConfiguration config)
    {
        _db = db;
        _email = email;
        _config = config;
    }

    private string GetBaseUrl()
    {
        string baseUrl = _config["App:BaseUrl"] ?? "";
        baseUrl = baseUrl.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return "";
        }
        return baseUrl.TrimEnd('/');
    }

    private string BuildLoginUrl()
    {
        string baseUrl = GetBaseUrl();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return "";
        }
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return "";
        }
        return WebUtility.HtmlEncode(baseUrl + "/Account/Login");
    }

    public async Task SendInitialAsync(User user)
    {
        if (user == null)
        {
            return;
        }
        DateTime nowUtc = DateTime.UtcNow;
        string expiresLocal = user.FreeDealExpiresAt?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? "-";
        string loginUrl = BuildLoginUrl();
        string buttonHtml = (string.IsNullOrWhiteSpace(loginUrl) ? "" : $"\n<a href='{loginUrl}'\n   style='display:inline-block;background:#2563eb;color:#fff;text-decoration:none;\n          padding:12px 18px;border-radius:10px;font-weight:700;margin-top:10px'>\n  Uygulamaya Gir & Randevu Al\n</a>\n<p style='margin:10px 0 0 0;font-size:12px;color:#64748b'>\n  Link açılmazsa kopyalayın: {loginUrl}\n</p>");
        string subject = "DRYCAR - 4. İç&Dış Yıkama Hediye";
        string body = $"\n<div style='font-family:Arial,Helvetica,sans-serif;font-size:15px;line-height:1.6;color:#0f172a'>\n  <h2 style='margin:0 0 10px 0'>\ud83c\udf89 Tebrikler!</h2>\n  <p style='margin:0 0 10px 0'>\n    3 kez <b>İç &amp; Dış Yıkama</b> aldığınız için <b>1 adet hediye yıkama</b> tanımlandı.\n  </p>\n  <p style='margin:0 0 10px 0'><b>Son kullanım:</b> {expiresLocal}</p>\n  {buttonHtml}\n  <hr style='border:none;border-top:1px solid #e5e7eb;margin:14px 0' />\n  <p style='margin:0;font-weight:700'>DRYCAR</p>\n</div>";
        _db.Notifications.Add(new Notification
        {
            UserId = user.Id,
            Message = "Tebrikler! Hediye İç&Dış yıkama tanımlandı. Son kullanım: " + expiresLocal,
            IsRead = false,
            CreatedAt = nowUtc
        });
        await _db.SaveChangesAsync();
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return;
        }
        try
        {
            await _email.SendEmailAsync(user.Email, subject, body);
        }
        catch
        {
            _db.Notifications.Add(new Notification
            {
                UserId = user.Id,
                Message = "Mail gönderilemedi (Gmail token/yetki hatası).",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }
    }

    public async Task SendReminderAsync(User user)
    {
        if (user == null)
        {
            return;
        }
        DateTime nowUtc = DateTime.UtcNow;
        string expiresLocal = user.FreeDealExpiresAt?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? "-";
        string loginUrl = BuildLoginUrl();
        string buttonHtml = (string.IsNullOrWhiteSpace(loginUrl) ? "" : $"\n<a href='{loginUrl}'\n   style='display:inline-block;background:#f59e0b;color:#111827;text-decoration:none;\n          padding:12px 18px;border-radius:10px;font-weight:800;margin-top:10px'>\n  Hemen Randevu Al\n</a>\n<p style='margin:10px 0 0 0;font-size:12px;color:#64748b'>\n  Link açılmazsa kopyalayın: {loginUrl}\n</p>");
        string subject = "DRYCAR - Hediye Yıkama Hatırlatma";
        string body = $"\n<div style='font-family:Arial,Helvetica,sans-serif;font-size:15px;line-height:1.6;color:#0f172a'>\n  <h2 style='margin:0 0 10px 0'>⏰ Hatırlatma</h2>\n  <p style='margin:0 0 10px 0'>\n    Hediye <b>İç &amp; Dış Yıkama</b> hakkınızın süresi dolmak üzere.\n  </p>\n  <p style='margin:0 0 10px 0'><b>Son tarih:</b> {expiresLocal}</p>\n  {buttonHtml}\n  <hr style='border:none;border-top:1px solid #e5e7eb;margin:14px 0' />\n  <p style='margin:0;font-weight:700'>DRYCAR</p>\n</div>";
        _db.Notifications.Add(new Notification
        {
            UserId = user.Id,
            Message = "Hatırlatma: Hediye İç&Dış yıkama hakkınızın son tarihi: " + expiresLocal,
            IsRead = false,
            CreatedAt = nowUtc
        });
        await _db.SaveChangesAsync();
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return;
        }
        try
        {
            await _email.SendEmailAsync(user.Email, subject, body);
        }
        catch
        {
            _db.Notifications.Add(new Notification
            {
                UserId = user.Id,
                Message = "Hatırlatma mail gönderilemedi (Gmail token/yetki hatası).",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }
    }
}
