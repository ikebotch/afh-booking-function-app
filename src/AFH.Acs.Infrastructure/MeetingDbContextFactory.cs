//using Microsoft.EntityFrameworkCore;
//using Microsoft.EntityFrameworkCore.Design;
//using Microsoft.Extensions.Configuration;


//namespace AFH.Acs.Recorder.Infrastructure;

//public sealed class SnowflakeDbContextFactory : IDesignTimeDbContextFactory<MeetingDbContext>
//{
//    public MeetingDbContext CreateDbContext(string[] args)
//    {
//        var cfg = new ConfigurationBuilder()
//            .SetBasePath(Directory.GetCurrentDirectory())
//            .AddJsonFile("local.settings.json", optional: true)
//            .AddJsonFile("appsettings.json", optional: true)
//            .AddEnvironmentVariables()
//            .Build();
//        var values = cfg.GetSection("Values");
//        Console.WriteLine($"[DesignTime] local.settings.json present: {File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "local.settings.json"))}");
//        Console.WriteLine($"[DesignTime] Values:SNOWFLAKE_CONN: {values?["SNOWFLAKE_CONN"] != null}");
//        Console.WriteLine($"[DesignTime] Env SNOWFLAKE_CONN set: {Environment.GetEnvironmentVariable("SNOWFLAKE_CONN") != null}");

//        var conn =
//            Environment.GetEnvironmentVariable("SNOWFLAKE_CONN") ?? values["SNOWFLAKE_CONN"]
//            ?? throw new InvalidOperationException("Missing SNOWFLAKE connection string for design-time (SNOWFLAKE_CONN or Values:SNOWFLAKE_CONN).");

//        var options = new DbContextOptionsBuilder<MeetingDbContext>()
//            .UseSnowflake(conn)
//            .Options;

//        return new MeetingDbContext(options);
//    }
//}