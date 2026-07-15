using BCrypt.Net;
using DryCar.Data;
using DryCar.Models;
using Microsoft.EntityFrameworkCore;

namespace DryCar.Services;

/// <summary>
/// İlk yönetici hesabını yalnızca açıkça verilen ortam değişkenleriyle oluşturur.
/// ADMIN_SEED_PASSWORD ilk başarılı çalıştırmadan sonra ortamdan kaldırılmalıdır.
/// </summary>
public sealed class AdminSeedWorker : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminSeedWorker> _logger;

    public AdminSeedWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<AdminSeedWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        string? username = _configuration["AdminSeed:Username"];
        string? password = _configuration["AdminSeed:Password"];
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (await db.Admins.AnyAsync(admin => admin.Username == username, cancellationToken))
        {
            return;
        }

        db.Admins.Add(new Admin
        {
            Username = username.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12),
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("İlk yönetici hesabı oluşturuldu: {Username}", username);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
