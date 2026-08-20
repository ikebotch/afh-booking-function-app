using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;

namespace AFH.Notification.Infrastructure.Persistence;

public sealed class NotificationDbContextFactory : IDesignTimeDbContextFactory<NotificationDbContext>
{
    public NotificationDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();
        var functionsLocalSettings = FindUpwards(
            basePath,
            Path.Combine("AFH.Booking.Function", "local.settings.json"));

        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddEnvironmentVariables();

        if (functionsLocalSettings is not null)
        {
            configBuilder.AddJsonFile(functionsLocalSettings, optional: true);
        }

        var configuration = configBuilder.Build();

        var connectionString = configuration.GetConnectionString("NotificationDb")
            ?? configuration["Values:ConnectionStrings:NotificationDb"]
            ?? configuration["Values:NotificationDb:ConnectionString"]
            ?? "Server=(localdb)\\mssqllocaldb;Database=AFH.Notification.DesignTime;Trusted_Connection=True;TrustServerCertificate=True";

        var optionsBuilder = new DbContextOptionsBuilder<NotificationDbContext>();
        optionsBuilder
            .UseSqlServer(connectionString)
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning));

        return new NotificationDbContext(optionsBuilder.Options);
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
