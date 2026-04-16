using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace AFH.Acs.Recorder.Infrastructure;

public sealed class SqlServerDbContextFactory : IDesignTimeDbContextFactory<MeetingDbContext>
{
    public MeetingDbContext CreateDbContext(string[] args)
    {
        var cfg = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("local.settings.json", optional: true)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var values = cfg.GetSection("Values");
        Console.WriteLine($"[DesignTime] local.settings.json present: {File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "local.settings.json"))}");
        Console.WriteLine($"[DesignTime] Values:MSSQL_CONN: {values?["MSSQL_CONN"] != null}");
        Console.WriteLine($"[DesignTime] Env MSSQL_CONN set: {Environment.GetEnvironmentVariable("MSSQL_CONN") != null}");

        var conn =
            Environment.GetEnvironmentVariable("MSSQL_CONN") ?? values["MSSQL_CONN"]
            ?? throw new InvalidOperationException("Missing SQL Server connection string for design-time (MSSQL_CONN or Values:MSSQL_CONN).");

        var options = new DbContextOptionsBuilder<MeetingDbContext>()
            .UseSqlServer(conn)
            .Options;

        return new MeetingDbContext(options);
    }
}
