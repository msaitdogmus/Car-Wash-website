using System;
using System.Net;
using System.Net.Http;
using DryCar.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DryCar;

public class Program
{
    public static void Main(string[] args)
    {
        CreateHostBuilder(args).Build().Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args).ConfigureServices(delegate (HostBuilderContext hostContext, IServiceCollection services)
        {
            services.AddHttpClient("kirsehirhaberturk", delegate (HttpClient client)
            {
                client.BaseAddress = new Uri("https://www.kirsehirhaberturk.com/");
                client.Timeout = TimeSpan.FromSeconds(15.0);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122 Safari/537.36");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "tr-TR,tr;q=0.9,en-US;q=0.8,en;q=0.7");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate");
            }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AutomaticDecompression = (DecompressionMethods.GZip | DecompressionMethods.Deflate)
            });
            services.AddHttpClient("openmeteo", delegate (HttpClient client)
            {
                client.BaseAddress = new Uri("https://api.open-meteo.com/");
                client.Timeout = TimeSpan.FromSeconds(10.0);
                client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate");
            }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AutomaticDecompression = (DecompressionMethods.GZip | DecompressionMethods.Deflate)
            });
            services.AddScoped<INewsService, KirsehirLiveNewsService>();
            services.AddScoped<IWeatherService, KirsehirWeatherService>();
        }).ConfigureWebHostDefaults(delegate (IWebHostBuilder webBuilder)
        {
            webBuilder.ConfigureKestrel(delegate (KestrelServerOptions options)
            {
                options.Limits.MaxRequestBodySize = 52428800L;
            });
            webBuilder.UseStartup<Startup>();
        });
    }
}
