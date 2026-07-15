using System;
using DryCar.Data;
using DryCar.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Threading.RateLimiting;

namespace DryCar;

public class Startup
{
    public IConfiguration Configuration { get; }

    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllersWithViews(options =>
        {
            options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
        });
        services.AddDataProtection().SetApplicationName("DryCar");
        string connectionString = Configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString) || connectionString.Contains("{{SQL_CONNECTION_STRING}}"))
        {
            string environmentConnectionString = Configuration["SQL_CONNECTION_STRING"];
            if (!string.IsNullOrWhiteSpace(environmentConnectionString))
            {
                connectionString = environmentConnectionString;
            }
        }
        if (string.IsNullOrWhiteSpace(connectionString) || connectionString.Contains("{{"))
        {
            throw new InvalidOperationException("DefaultConnection yapılandırması bulunamadı.");
        }
        services.AddDbContext<ApplicationDbContext>(delegate (DbContextOptionsBuilder options)
        {
            options.UseSqlServer(connectionString);
        });
        services.AddDistributedMemoryCache();
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
        });
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
            options.AddPolicy("face", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 6,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
        });
        services.AddSession(delegate (SessionOptions options)
        {
            options.IdleTimeout = TimeSpan.FromMinutes(30.0);
            options.Cookie.Name = "__Host-DryCar.Session";
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        });
        services.Configure(delegate (HostOptions o)
        {
            o.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
        });
        services.AddHttpContextAccessor();
        services.AddScoped<IEmailSender, GmailApiEmailSender>();
        services.AddScoped<IFaceVectorProtector, FaceVectorProtector>();
        services.AddScoped<FreeDealNotifier>();
        services.AddHostedService<AdminSeedWorker>();
        services.AddHostedService<FreeDealReminderWorker>();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.UseForwardedHeaders();
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/Home/Error");
            app.UseHsts();
        }
        app.UseHttpsRedirection();
        app.Use(async (context, next) =>
        {
            context.Response.Headers.XContentTypeOptions = "nosniff";
            context.Response.Headers.XFrameOptions = "DENY";
            context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            context.Response.Headers.Append("Permissions-Policy", "camera=(self), geolocation=(), microphone=()");
            await next();
        });
        app.UseStaticFiles();
        app.UseRouting();
        app.UseRateLimiter();
        app.UseSession();
        app.UseAuthorization();
        app.UseEndpoints(delegate (IEndpointRouteBuilder endpoints)
        {
            endpoints.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        });
    }
}
