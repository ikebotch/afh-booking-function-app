using AFH.Common.Errors.Abstractions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Reflection;

namespace AFH.Acs.Tests;

public sealed class ArchitectureGuardTests
{
    [Fact]
    public void Contract_Domain_And_Function_DoNotReferenceSharedErrorSdkAssemblies()
    {
        AssertDoesNotReferencePrefix("AFH.Acs.Domain", "AFH.Common.Errors");
        AssertDoesNotReferencePrefix("AFH.Acs.Contract", "AFH.Common.Errors");
    }

    [Fact]
    public void Function_Assembly_OnlyReferencesTheExpectedSharedSdks()
    {
        AssertReferences("AFH.Acs.Function", "AFH.Common.Errors");
        AssertReferences("AFH.Acs.Function", "AFH.Common.Errors.AzureFunctions");
        AssertReferences("AFH.Acs.Function", "AFH.Common.Errors.Email");
        AssertReferences("AFH.Acs.Function", "AFH.Common.SpeechAI");
        AssertDoesNotReference("AFH.Acs.Function", "AFH.Common.Errors.ApplicationInsights");
        AssertDoesNotReference("AFH.Acs.Function", "AFH.Common.Errors.EntityFramework");
        AssertDoesNotReference("AFH.Acs.Function", "AFH.Acs.Application");
        AssertDoesNotReference("AFH.Acs.Function", "AFH.Acs.Infrastructure");
    }

    [Fact]
    public void FunctionAssembly_KeepsExceptionMapperLocal()
    {
        var functionAssembly = Assembly.Load("AFH.Acs.Function");

        var mapperTypes = functionAssembly.GetTypes()
            .Where(type => typeof(IExceptionMapper).IsAssignableFrom(type))
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .ToArray();

        var mapper = Assert.Single(mapperTypes);
        Assert.Equal("AcsExceptionMapper", mapper.Name);
        Assert.Equal("AFH.Acs.Function.Middleware", mapper.Namespace);
    }

    [Fact]
    public void EndpointAccessPolicies_RequireExplicitHttpFunctionCoverage()
    {
        var functionAssembly = Assembly.Load("AFH.Acs.Function");
        var endpointPoliciesType = functionAssembly.GetType("AFH.Acs.Function.Security.EndpointAccessPolicies");
        var getPolicy = endpointPoliciesType?.GetMethod("GetPolicy", BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(getPolicy);

        foreach (var functionName in GetHttpFunctionNames(functionAssembly))
        {
            var exception = Record.Exception(() => getPolicy!.Invoke(null, [functionName]));
            Assert.Null(exception);
        }

        var thrown = Assert.Throws<TargetInvocationException>(() => getPolicy!.Invoke(null, ["unmapped-http-function"]));
        Assert.IsType<InvalidOperationException>(thrown.InnerException);
    }

    [Fact]
    public void EndpointAccessPolicies_KnownHttpFunctions_ExactlyMatchDiscoveredHttpFunctions()
    {
        var functionAssembly = Assembly.Load("AFH.Acs.Function");
        var endpointPoliciesType = functionAssembly.GetType("AFH.Acs.Function.Security.EndpointAccessPolicies");
        var knownHttpFunctionsProperty = endpointPoliciesType?.GetProperty("KnownHttpFunctions", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(knownHttpFunctionsProperty);

        var knownHttpFunctions = ((IReadOnlyCollection<string>?)knownHttpFunctionsProperty!.GetValue(null))?
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.NotNull(knownHttpFunctions);
        Assert.Equal(GetHttpFunctionNames(functionAssembly), knownHttpFunctions);
    }

    [Fact]
    public void Program_RemainsAThinBootstrapShell()
    {
        var programText = File.ReadAllText(GetProgramPath("AFH.Acs.Function"));

        Assert.Contains("ConfigureMiddlewarePipeline(app);", programText);
        Assert.Contains("ConfigureAppConfiguration(cfg);", programText);
        Assert.Contains("AddSharedErrorHandling(services, ctx.Configuration", programText);
        Assert.Contains("services.AddRecordingServices(ctx.Configuration);", programText);
        Assert.Contains("services.AddSpeechAi(ctx.Configuration);", programText);
        Assert.Contains("services.AddSingleton<IMeetingWorkflowStore, MeetingWorkflowStore>();", programText);
        Assert.Contains("ErrorEmail:Enabled", programText);
        Assert.Contains("ConfigureWorkerSerialization(services, caseInsensitivePropertyNames: true);", programText);
        Assert.DoesNotContain("BuildServiceProvider(", programText);
        Assert.DoesNotContain("AddAfhAcsInfrastructure", programText);
    }

    private static string[] GetHttpFunctionNames(Assembly functionAssembly) =>
        functionAssembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            .Where(method => method.GetCustomAttribute<FunctionAttribute>() is not null)
            .Where(method => method.GetParameters().Any(parameter => parameter.GetCustomAttributes<HttpTriggerAttribute>(inherit: false).Any()))
            .Select(method => method.GetCustomAttribute<FunctionAttribute>()!.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

    private static void AssertDoesNotReference(string assemblyName, string forbiddenAssemblyName)
    {
        var references = Assembly.Load(assemblyName).GetReferencedAssemblies().Select(reference => reference.Name).ToArray();
        Assert.DoesNotContain(forbiddenAssemblyName, references);
    }

    private static void AssertReferences(string assemblyName, string expectedAssemblyName)
    {
        var references = Assembly.Load(assemblyName).GetReferencedAssemblies().Select(reference => reference.Name).ToArray();
        Assert.Contains(expectedAssemblyName, references);
    }

    private static void AssertDoesNotReferencePrefix(string assemblyName, string forbiddenPrefix)
    {
        var references = Assembly.Load(assemblyName).GetReferencedAssemblies().Select(reference => reference.Name).ToArray();
        Assert.DoesNotContain(references, reference => reference?.StartsWith(forbiddenPrefix, StringComparison.Ordinal) == true);
    }

    private static string GetProgramPath(string functionProjectName)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "src", functionProjectName, "Program.cs");
            if (File.Exists(candidate))
                return candidate;
        }

        throw new DirectoryNotFoundException($"Could not locate Program.cs for {functionProjectName}.");
    }
}
