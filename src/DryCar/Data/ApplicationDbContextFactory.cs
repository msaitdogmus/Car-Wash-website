using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace DryCar.Data;

public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        string environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Development";
        IConfigurationRoot config = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json", optional: true, reloadOnChange: false).AddJsonFile("appsettings." + environment + ".json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();
        string connStr = config.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connStr) || connStr.Contains("{{SQL_CONNECTION_STRING}}"))
        {
            string environmentConnectionString = config["SQL_CONNECTION_STRING"];
            if (!string.IsNullOrWhiteSpace(environmentConnectionString))
            {
                connStr = environmentConnectionString;
            }
        }
        if (string.IsNullOrWhiteSpace(connStr) || connStr.Contains("{{"))
        {
            throw new InvalidOperationException("DefaultConnection yapılandırması bulunamadı.");
        }
        DbContextOptionsBuilder<ApplicationDbContext> optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer(connStr);
        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
