using AFH.Common.Errors.Abstractions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Reflection;

namespace AFH.Booking.Tests;

public sealed class ArchitectureGuardTests
{
    [Fact]
    public void Application_Infrastructure_And_Domain_DoNotReferenceFunctionAssembly()
    {
        AssertDoesNotReference("AFH.Booking.Application", "AFH.Booking.Function");
        AssertDoesNotReference("AFH.Booking.Infrastructure", "AFH.Booking.Function");
        AssertDoesNotReference("AFH.Booking.Domain", "AFH.Booking.Function");
        AssertDoesNotReference("AFH.Booking.Contract", "AFH.Booking.Function");
    }

    [Fact]
    public void Application_Domain_And_Contract_DoNotReferenceSharedErrorSdkAssemblies()
    {
        AssertDoesNotReferencePrefix("AFH.Booking.Application", "AFH.Common.Errors");
        AssertDoesNotReferencePrefix("AFH.Booking.Domain", "AFH.Common.Errors");
        AssertDoesNotReferencePrefix("AFH.Booking.Contract", "AFH.Common.Errors");
    }

    [Fact]
    public void FunctionAssembly_KeepsExceptionMapperLocal()
    {
        var functionAssembly = Assembly.Load("AFH.Booking.Function");

        var mapperTypes = functionAssembly.GetTypes()
            .Where(type => typeof(IExceptionMapper).IsAssignableFrom(type))
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .ToArray();

        var mapper = Assert.Single(mapperTypes);
        Assert.Equal("BookingExceptionMapper", mapper.Name);
        Assert.Equal("AFH.Booking.Function.Middleware", mapper.Namespace);
    }

    [Fact]
    public void EndpointAccessPolicies_RequireExplicitHttpFunctionCoverage()
    {
        var functionAssembly = Assembly.Load("AFH.Booking.Function");
        var endpointPoliciesType = functionAssembly.GetType("AFH.Booking.Function.Security.EndpointAccessPolicies");
        var getPolicy = endpointPoliciesType?.GetMethod("GetPolicy", BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(getPolicy);

        var httpFunctionNames = GetHttpFunctionNames(functionAssembly);
        foreach (var functionName in httpFunctionNames)
        {
            var exception = Record.Exception(() => getPolicy!.Invoke(null, [functionName]));
            Assert.Null(exception);
        }

        var thrown = Assert.Throws<TargetInvocationException>(() => getPolicy!.Invoke(null, ["Unmapped_Http_Function"]));
        Assert.IsType<InvalidOperationException>(thrown.InnerException);
    }

    [Fact]
    public void Program_RemainsAThinBootstrapShell()
    {
        var programText = File.ReadAllText(GetProgramPath("AFH.Booking.Function"));

        Assert.Contains("ConfigureMiddlewarePipeline(app);", programText);
        Assert.Contains("ConfigureAppConfiguration(cfg);", programText);
        Assert.Contains("ConfigureLogging(logging);", programText);
        Assert.Contains("AddSharedErrorHandling(services, ctx.Configuration", programText);
        Assert.Contains("ConfigureWorkerSerialization(services", programText);
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
