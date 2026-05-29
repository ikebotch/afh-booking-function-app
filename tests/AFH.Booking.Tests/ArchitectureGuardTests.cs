using System.Reflection;

namespace AFH.Booking.Tests;

public sealed class ArchitectureGuardTests
{
    [Theory]
    [InlineData("AFH.Booking.Application")]
    [InlineData("AFH.Booking.Infrastructure")]
    public void BookingProjects_DoNotReferenceNotificationProjects(string projectName)
    {
        var projectPath = GetProjectPath(projectName);
        var projectText = File.ReadAllText(projectPath);

        Assert.DoesNotContain("AFH.Notification.", projectText, StringComparison.Ordinal);

        var sourceRoot = Path.GetDirectoryName(projectPath)!;
        var sourceFiles = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToArray();

        foreach (var file in sourceFiles)
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("using AFH.Notification.", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void NotificationInfrastructure_DoesNotOwnBookingHttpPublisher()
    {
        var projectRoot = Path.GetDirectoryName(GetProjectPath("AFH.Notification.Infrastructure"))!;
        var sourceFiles = Directory.GetFiles(projectRoot, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToArray();

        foreach (var file in sourceFiles)
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("HttpNotificationPublisher", text, StringComparison.Ordinal);
            Assert.DoesNotContain("HttpNotificationPublisherOptions", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void LocalSettingsTemplate_DocumentsBookingNotificationHttpPublisher()
    {
        var templatePath = Path.Combine(Path.GetDirectoryName(GetProjectPath("AFH.Booking.Function"))!, "local.settings.template.json");
        var template = File.ReadAllText(templatePath);

        Assert.Contains("Booking:Notifications:Http:BaseUrl", template, StringComparison.Ordinal);
        Assert.Contains("Booking:Notifications:Http:FunctionKey", template, StringComparison.Ordinal);
        Assert.Contains("Booking:Notifications:Http:InternalToken", template, StringComparison.Ordinal);
        Assert.DoesNotContain("Notifications:Integration:Http:BaseUrl", template, StringComparison.Ordinal);
    }

    private static string GetProjectPath(string projectName)
    {
        var root = GetRepositoryRoot();
        return Path.Combine(root, "src", projectName, $"{projectName}.csproj");
    }

    private static string GetRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AFH.Booking.sln")))
            dir = dir.Parent;

        return dir?.FullName
            ?? throw new InvalidOperationException("Could not locate AFH.Booking.sln.");
    }
}
