using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DryCar.Data;
using DryCar.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DryCar.Services;

public class FreeDealReminderWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    private readonly ILogger<FreeDealReminderWorker> _logger;

    public FreeDealReminderWorker(IServiceScopeFactory scopeFactory, ILogger<FreeDealReminderWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnce(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "FreeDealReminderWorker RunOnce crashed (ignored to keep host alive).");
            }
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(30.0), stoppingToken);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private async Task RunOnce(CancellationToken ct)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        FreeDealNotifier notifier = scope.ServiceProvider.GetRequiredService<FreeDealNotifier>();
        DateTime nowUtc = DateTime.UtcNow;
        DateTime remindUntil = nowUtc.AddHours(24.0);
        List<User> users = await db.Users.Where((User user) => user.FreeDealBalance > 0 && user.FreeDealCycleEndAt != null && user.FreeDealCycleEndAt > nowUtc && user.FreeDealCycleEndAt <= remindUntil && user.FreeDealReminderSentAt == null).ToListAsync(ct);
        if (!users.Any())
        {
            return;
        }
        foreach (User u in users)
        {
            try
            {
                u.FreeDealReminderSentAt = nowUtc;
                await db.SaveChangesAsync(ct);
                await notifier.SendReminderAsync(u);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Reminder send failed for UserId={UserId}.", u.Id);
            }
        }
    }
}
