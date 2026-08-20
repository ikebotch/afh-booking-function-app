using AFH.Booking.Infrastructure.Composition;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace AFH.Booking.Infrastructure.Persistence;

public sealed class NotificationPolicyDbContextFactory : IDesignTimeDbContextFactory<NotificationPolicyDbContext>
{
    public NotificationPolicyDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();
        var functionsLocalSettings = FindUpwards(
            basePath,
            Path.Combine("AFH.Booking.Function", "local.settings.json"));

        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(basePath);

        if (!string.IsNullOrWhiteSpace(functionsLocalSettings))
            configBuilder.AddJsonFile(functionsLocalSettings, optional: true);

        var config = configBuilder
            .AddEnvironmentVariables()
            .Build();

        var connectionString = ServiceCollectionExtensions.ResolveNotificationDbConnectionString(config)
            ?? "Server=(localdb)\\mssqllocaldb;Database=AFHNotificationPolicyDesignTime;Trusted_Connection=True;TrustServerCertificate=True";

        var optionsBuilder = new DbContextOptionsBuilder<NotificationPolicyDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new NotificationPolicyDbContext(optionsBuilder.Options);
    }

    private static string? FindUpwards(string startDir, string relativePath)
    {
        var dir = new DirectoryInfo(startDir);

        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        return null;
    }
}
