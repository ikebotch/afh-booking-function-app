using AFH.Booking.Infrastructure.Composition;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace AFH.Booking.Infrastructure.Persistence;

public sealed class BookingDbContextFactory : IDesignTimeDbContextFactory<BookingDbContext>
{
    public BookingDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();

        // Find src/AFH.Booking.Function/local.settings.json no matter where EF is run from
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

        // Try the most common Azure Functions patterns
        var connectionString = ServiceCollectionExtensions.ResolveBookingDbConnectionString(config)
            ?? config["ConnectionStrings:BookingDb"]
            ?? config["BookingDb:ConnectionString"];

        if (string.IsNullOrWhiteSpace(connectionString))
            connectionString = "Server=(localdb)\\mssqllocaldb;Database=AFHBookingDesignTime;Trusted_Connection=True;TrustServerCertificate=True";

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // Dump what EF can actually see for debugging
            var visible = config.AsEnumerable()
                .Where(kv => kv.Key.Contains("BookingDb", StringComparison.OrdinalIgnoreCase)
                          || kv.Key.Contains("ConnectionStrings", StringComparison.OrdinalIgnoreCase))
                .Select(kv => $"{kv.Key}={(string.IsNullOrWhiteSpace(kv.Value) ? "<empty>" : "<set>")}")
                .ToArray();

            throw new InvalidOperationException(
                "Missing SQL connection string for BookingDbContext.\n" +
                $"WorkingDirectory: {basePath}\n" +
                $"Functions local.settings.json resolved to: {(functionsLocalSettings ?? "<NOT FOUND>")}\n" +
                "Looked for:\n" +
                "- Values:BookingDb:ConnectionString\n" +
                "- Values:ConnectionStrings:BookingDb\n" +
                "- ConnectionStrings:BookingDb\n" +
                "- BookingDb:ConnectionString\n\n" +
                "Keys visible to EF (filtered):\n" +
                (visible.Length == 0 ? "<none>" : string.Join("\n", visible)));
        }

        var optionsBuilder = new DbContextOptionsBuilder<BookingDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new BookingDbContext(optionsBuilder.Options);
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
