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
    public void Function_And_Infrastructure_RespectSharedErrorSdkBoundaries()
    {
        AssertReferences("AFH.Booking.Function", "AFH.Common.Errors");
        AssertReferences("AFH.Booking.Function", "AFH.Common.Errors.AzureFunctions");
        AssertDoesNotReference("AFH.Booking.Function", "AFH.Common.Errors.ApplicationInsights");
        AssertDoesNotReference("AFH.Booking.Function", "AFH.Common.Errors.Email");
        AssertDoesNotReference("AFH.Booking.Function", "AFH.Common.Errors.EntityFramework");

        AssertReferences("AFH.Booking.Infrastructure", "AFH.Common.Errors.EntityFramework");
        AssertReferences("AFH.Booking.Infrastructure", "AFH.Common.Errors.Email");
        AssertReferences("AFH.Booking.Infrastructure", "AFH.Common.Errors.ApplicationInsights");
        AssertDoesNotReference("AFH.Booking.Infrastructure", "AFH.Common.Errors.AzureFunctions");
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
    public void EndpointAccessPolicies_KnownHttpFunctions_ExactlyMatchDiscoveredHttpFunctions()
    {
        var functionAssembly = Assembly.Load("AFH.Booking.Function");
        var endpointPoliciesType = functionAssembly.GetType("AFH.Booking.Function.Security.EndpointAccessPolicies");
        var knownHttpFunctionsProperty = endpointPoliciesType?.GetProperty("KnownHttpFunctions", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(knownHttpFunctionsProperty);

        var knownHttpFunctions = ((IReadOnlyCollection<string>?)knownHttpFunctionsProperty!.GetValue(null))?
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.NotNull(knownHttpFunctions);
        Assert.Equal(GetHttpFunctionNames(functionAssembly), knownHttpFunctions);
    }

    [Fact]
    public void SendNotificationEndpoint_RemainsInternalOnly()
    {
        var endpointPoliciesType = Assembly.Load("AFH.Booking.Function").GetType("AFH.Booking.Function.Security.EndpointAccessPolicies");
        var getPolicy = endpointPoliciesType?.GetMethod("GetPolicy", BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(getPolicy);

        var policy = getPolicy!.Invoke(null, ["Bookings_SendNotification"]);
        Assert.Equal("InternalOnly", policy?.ToString());
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
        Assert.DoesNotContain("BuildServiceProvider(", programText);
    }

    [Fact]
    public void NotificationApplication_GenericServices_DoNotHardcodeBookingPolicies()
    {
        var projectPath = GetProjectPath("AFH.Notification.Application");
        var genericServiceFiles = new[]
        {
            Path.Combine(projectPath, "Services", "NotificationIdempotencyKeyGenerator.cs"),
            Path.Combine(projectPath, "Services", "NotificationRecipientResolver.cs"),
            Path.Combine(projectPath, "Services", "NotificationTemplateRenderer.cs")
        };

        var forbiddenTerms = new[]
        {
            "BookingId",
            "HoldId",
            "TransactionId",
            "BookingConfirmed",
            "BookingRescheduled",
            "BookingCancelled",
            "BookingHoldCreated"
        };

        foreach (var file in genericServiceFiles)
        {
            var text = File.ReadAllText(file);
            foreach (var term in forbiddenTerms)
                Assert.DoesNotContain(term, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void NotificationQueueMessage_RemainsOutboxIdOnly()
    {
        var messageType = Assembly.Load("AFH.Notification.Application")
            .GetType("AFH.Notification.Application.Models.NotificationQueueMessage");

        Assert.NotNull(messageType);
        var property = Assert.Single(messageType!.GetProperties(BindingFlags.Instance | BindingFlags.Public));
        Assert.Equal("OutboxId", property.Name);
        Assert.Equal(typeof(Guid), property.PropertyType);
    }

    [Fact]
    public void FunctionAssembly_DoesNotContainSqlTimerNotificationDispatcher()
    {
        var functionProjectPath = GetProjectPath("AFH.Booking.Function");
        var files = Directory.GetFiles(functionProjectPath, "*.cs", SearchOption.AllDirectories);

        Assert.DoesNotContain(files, file => Path.GetFileName(file).Equals("DispatchNotificationOutboxFunction.cs", StringComparison.Ordinal));

        foreach (var file in files.Where(file => file.Contains($"{Path.DirectorySeparatorChar}Notifications{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("[TimerTrigger", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void LocalSettingsTemplate_DoesNotConfigureSqlTimerNotificationDispatcher()
    {
        var templatePath = Path.Combine(GetProjectPath("AFH.Booking.Function"), "local.settings.template.json");
        var template = File.ReadAllText(templatePath);

        Assert.DoesNotContain("Notifications:Outbox:DispatchSchedule", template, StringComparison.Ordinal);
        Assert.DoesNotContain("Notifications__Outbox__DispatchSchedule", template, StringComparison.Ordinal);
    }

    [Fact]
    public void NotificationDispatchesLegacyComponents_RemainPresentDuringTransition()
    {
        var bookingInfrastructure = Assembly.Load("AFH.Booking.Infrastructure");
        var bookingApplication = Assembly.Load("AFH.Booking.Application");
        var notificationInfrastructure = Assembly.Load("AFH.Notification.Infrastructure");

        AssertLegacyTypeIsPresentAndNotObsolete(notificationInfrastructure, "AFH.Notification.Infrastructure.Persistence.Models.NotificationDispatchModel");
        AssertLegacyTypeIsPresentAndNotObsolete(bookingInfrastructure, "AFH.Booking.Infrastructure.Persistence.Repositories.NotificationDispatchRepository");
        AssertLegacyTypeIsPresentAndNotObsolete(bookingApplication, "AFH.Booking.Application.Abstractions.Persistence.INotificationDispatchRepository");
    }

    [Fact]
    public void RemovedLegacyDirectNotificationSender_RemainsRemoved()
    {
        var bookingInfrastructure = Assembly.Load("AFH.Booking.Infrastructure");
        var bookingApplication = Assembly.Load("AFH.Booking.Application");

        Assert.Null(bookingInfrastructure.GetType("AFH.Booking.Infrastructure.Clients.ClientNotificationService"));
        Assert.Null(bookingInfrastructure.GetType("AFH.Booking.Infrastructure.Clients.ComposedEmailNotificationSender"));
        Assert.Null(bookingInfrastructure.GetType("AFH.Booking.Infrastructure.Clients.OperationalNotificationService"));
        Assert.Null(bookingApplication.GetType("AFH.Booking.Application.Abstractions.Clients.IClientNotificationService"));
        Assert.Null(bookingApplication.GetType("AFH.Booking.Application.Abstractions.Clients.IEmailNotificationSender"));
        Assert.Null(bookingApplication.GetType("AFH.Booking.Application.Abstractions.Governance.IOperationalNotificationService"));
        Assert.Null(bookingApplication.GetType("AFH.Booking.Application.Abstractions.Lifecycle.INotificationService"));
    }

    [Fact]
    public void NotificationOutboxMigrations_DoNotDropNotificationDispatches()
    {
        var notificationInfrastructurePath = GetProjectPath("AFH.Notification.Infrastructure");
        var migrationFiles = Directory.GetFiles(Path.Combine(notificationInfrastructurePath, "Migrations"), "*.cs", SearchOption.TopDirectoryOnly);

        foreach (var file in migrationFiles)
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("DropTable(\n                name: \"NotificationDispatches\"", text, StringComparison.Ordinal);
            Assert.DoesNotContain("DROP TABLE [NotificationDispatches]", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("DROP TABLE [dbo].[NotificationDispatches]", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void LifecycleNotificationFlows_DoNotDependOnLegacyBookingNotificationService()
    {
        var application = Assembly.Load("AFH.Booking.Application");
        var bookingNotificationStep = application.GetType("AFH.Booking.Application.Abstractions.Lifecycle.IBookingNotificationStep");

        Assert.Null(application.GetType("AFH.Booking.Application.Abstractions.Lifecycle.INotificationService"));
        Assert.NotNull(bookingNotificationStep);

        AssertUsesNotificationStepWithoutLegacyNotificationService(application, "AFH.Booking.Application.Holds.ConfirmBookingService", bookingNotificationStep!);
        AssertUsesNotificationStepWithoutLegacyNotificationService(application, "AFH.Booking.Application.Bookings.CancellationOrchestrator", bookingNotificationStep!);
        AssertUsesNotificationStepWithoutLegacyNotificationService(application, "AFH.Booking.Application.Bookings.RearrangementOrchestrator", bookingNotificationStep!);
    }

    [Fact]
    public void ManualSendEndpoint_DoesNotDependOnLegacyClientNotificationService()
    {
        var functionAssembly = Assembly.Load("AFH.Booking.Function");
        var application = Assembly.Load("AFH.Booking.Application");
        var functionType = functionAssembly.GetType("AFH.Booking.Function.Functions.V1.Bookings.SendBookingNotificationFunction");
        var manualNotificationService = application.GetType("AFH.Booking.Application.Abstractions.Notifications.IBookingNotificationRequestService");

        Assert.NotNull(functionType);
        Assert.Null(application.GetType("AFH.Booking.Application.Abstractions.Clients.IClientNotificationService"));
        Assert.NotNull(manualNotificationService);

        var constructorParameterTypes = functionType!
            .GetConstructors()
            .SelectMany(ctor => ctor.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        Assert.Contains(manualNotificationService, constructorParameterTypes);
        Assert.DoesNotContain(constructorParameterTypes, type => type.FullName == "AFH.Booking.Application.Abstractions.Clients.IClientNotificationService");
    }

    [Fact]
    public void NotificationContract_RemainsSourceNeutral()
    {
        var contractProjectPath = GetProjectPath("AFH.Notification.Contract");
        var forbiddenTerms = new[]
        {
            "BookingId",
            "HoldId",
            "TransactionId",
            "ClientId",
            "AdviserName",
            "ClientName"
        };

        foreach (var file in Directory.GetFiles(contractProjectPath, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            foreach (var term in forbiddenTerms)
                Assert.DoesNotContain(term, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void DbContexts_RespectNotificationTableOwnership()
    {
        var bookingDbContext = Assembly.Load("AFH.Booking.Infrastructure")
            .GetType("AFH.Booking.Infrastructure.Persistence.BookingDbContext");
        var notificationDbContext = Assembly.Load("AFH.Notification.Infrastructure")
            .GetType("AFH.Notification.Infrastructure.Persistence.NotificationDbContext");

        Assert.NotNull(bookingDbContext);
        Assert.NotNull(notificationDbContext);

        var bookingDbSetNames = bookingDbContext!.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(x => x.Name)
            .ToArray();
        var notificationDbSetNames = notificationDbContext!.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(x => x.Name)
            .ToArray();

        Assert.DoesNotContain("NotificationOutbox", bookingDbSetNames);
        Assert.DoesNotContain("NotificationDispatches", bookingDbSetNames);
        Assert.DoesNotContain("EmailBounceEvents", bookingDbSetNames);
        Assert.Contains("BookingNotificationRules", bookingDbSetNames);
        Assert.Contains("BookingNotificationRuleChannels", bookingDbSetNames);
        Assert.Contains("BookingNotificationRuleRecipients", bookingDbSetNames);

        Assert.Contains("NotificationOutbox", notificationDbSetNames);
        Assert.Contains("NotificationDispatches", notificationDbSetNames);
        Assert.Contains("EmailBounceEvents", notificationDbSetNames);
        Assert.Contains("NotificationTemplates", notificationDbSetNames);
        Assert.DoesNotContain("BookingNotificationRules", notificationDbSetNames);
        Assert.DoesNotContain("BookingNotificationRuleChannels", notificationDbSetNames);
        Assert.DoesNotContain("BookingNotificationRuleRecipients", notificationDbSetNames);
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

    private static void AssertLegacyTypeIsPresentAndNotObsolete(Assembly assembly, string typeName)
    {
        var type = assembly.GetType(typeName);

        Assert.NotNull(type);
        Assert.Null(type!.GetCustomAttribute<ObsoleteAttribute>());
    }

    private static void AssertUsesNotificationStepWithoutLegacyNotificationService(
        Assembly assembly,
        string typeName,
        Type bookingNotificationStep)
    {
        var type = assembly.GetType(typeName);
        Assert.NotNull(type);

        var constructorParameterTypes = type!
            .GetConstructors()
            .SelectMany(ctor => ctor.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        Assert.Contains(bookingNotificationStep, constructorParameterTypes);
        Assert.DoesNotContain(constructorParameterTypes, parameterType => parameterType.FullName == "AFH.Booking.Application.Abstractions.Lifecycle.INotificationService");
        Assert.DoesNotContain(type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic), field => field.FieldType.FullName == "AFH.Booking.Application.Abstractions.Lifecycle.INotificationService");
    }

    private static void AssertDoesNotReferencePrefix(string assemblyName, string forbiddenPrefix)
    {
        var references = Assembly.Load(assemblyName).GetReferencedAssemblies().Select(reference => reference.Name).ToArray();
        Assert.DoesNotContain(references, reference => reference?.StartsWith(forbiddenPrefix, StringComparison.Ordinal) == true);
    }

    private static string GetProgramPath(string functionProjectName)
    {
        return Path.Combine(GetProjectPath(functionProjectName), "Program.cs");
    }

    private static string GetProjectPath(string projectName)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "src", projectName);
            if (Directory.Exists(candidate))
                return candidate;
        }

        throw new DirectoryNotFoundException($"Could not locate project directory for {projectName}.");
    }
}
