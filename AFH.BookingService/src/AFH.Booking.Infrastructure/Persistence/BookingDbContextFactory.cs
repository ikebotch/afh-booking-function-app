using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.Text.Json;

namespace AFH.Booking.Infrastructure.Persistence;

public sealed class BookingDbContextFactory : IDesignTimeDbContextFactory<BookingDbContext>
{
    public BookingDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();

        // Support running from:
        // 1) .../src
        // 2) .../src/AFH.Booking.Infrastructure
        // 3) .../src/AFH.Booking.Functions
        var localSettingsCandidates = new[]
        {
            Path.Combine(basePath, "local.settings.json"),
            Path.Combine(basePath, "AFH.Booking.Functions", "local.settings.json"),
            Path.Combine(basePath, "..", "AFH.Booking.Functions", "local.settings.json")
        };

        var conn =
            Environment.GetEnvironmentVariable("BookingDb__ConnectionString") ??
            Environment.GetEnvironmentVariable("ConnectionStrings__BookingDb");

        if (string.IsNullOrWhiteSpace(conn))
        {
            foreach (var file in localSettingsCandidates)
            {
                conn = TryReadConnectionString(file);
                if (!string.IsNullOrWhiteSpace(conn))
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(conn))
            throw new InvalidOperationException(
                "BookingDb:ConnectionString is missing. Set it in local.settings.json or environment variables.");

        var optionsBuilder = new DbContextOptionsBuilder<BookingDbContext>();
        optionsBuilder.UseSqlServer(conn);

        return new BookingDbContext(optionsBuilder.Options);
    }

    private static string? TryReadConnectionString(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        try
        {
            using var stream = File.OpenRead(filePath);
            using var doc = JsonDocument.Parse(stream, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
            var root = doc.RootElement;

            // local.settings-style: { "BookingDb": { "ConnectionString": "..." } }
            if (root.TryGetProperty("BookingDb", out var bookingDb) &&
                bookingDb.ValueKind == JsonValueKind.Object &&
                bookingDb.TryGetProperty("ConnectionString", out var cs) &&
                cs.ValueKind == JsonValueKind.String)
            {
                return cs.GetString();
            }

            // appsettings-style: { "ConnectionStrings": { "BookingDb": "..." } }
            if (root.TryGetProperty("ConnectionStrings", out var connStrings) &&
                connStrings.ValueKind == JsonValueKind.Object &&
                connStrings.TryGetProperty("BookingDb", out var bookingCs) &&
                bookingCs.ValueKind == JsonValueKind.String)
            {
                return bookingCs.GetString();
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
