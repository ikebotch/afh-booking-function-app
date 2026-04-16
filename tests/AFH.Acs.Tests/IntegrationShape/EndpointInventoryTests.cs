using Xunit;
using System.IO;
using System.Linq;

namespace AFH.Acs.Tests.IntegrationShape;

public sealed class EndpointInventoryTests
{
    [Fact]
    public void FunctionSurface_Contains_OnlyMeetingAndTranscriptionFolders()
    {
        var root = LocateFunctionRoot();
        Assert.True(Directory.EnumerateFiles(Path.Combine(root, "Meetings"), "*.cs", SearchOption.AllDirectories).Any());
        Assert.True(Directory.EnumerateFiles(Path.Combine(root, "Recordings"), "*.cs", SearchOption.AllDirectories).Any());
        Assert.True(Directory.EnumerateFiles(Path.Combine(root, "Transcription"), "*.cs", SearchOption.AllDirectories).Any());
        Assert.True(Directory.EnumerateFiles(Path.Combine(root, "System"), "*.cs", SearchOption.AllDirectories).Any());
        Assert.False(Directory.EnumerateFiles(Path.Combine(root, "Lookup"), "*.cs", SearchOption.AllDirectories).Any());
        Assert.False(Directory.EnumerateFiles(Path.Combine(root, "Acs"), "*.cs", SearchOption.AllDirectories).Any());
        Assert.False(Directory.EnumerateFiles(Path.Combine(root, "Workspace"), "*.cs", SearchOption.AllDirectories).Any());
        Assert.False(Directory.EnumerateFiles(Path.Combine(root, "Graph"), "*.cs", SearchOption.AllDirectories).Any());
    }

    private static string LocateFunctionRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "src", "AFH.Acs.Function", "Functions", "V1");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the AFH.Acs.Function V1 folder.");
    }
}
