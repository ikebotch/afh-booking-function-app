using AFH.Acs.Function.Functions.V1.System;
using AFH.Acs.Function.Middleware;
using AFH.Acs.Function.Security;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using System.Reflection;
using AFH.Acs.Domain;

namespace AFH.Acs.Tests;

public sealed class InternalApiAuthMiddlewareTests
{
    [Theory]
    [InlineData("v1-health", EndpointAccessPolicy.Public)]
    [InlineData("v1-openapi-json", EndpointAccessPolicy.Public)]
    [InlineData("v1-scalar-ui", EndpointAccessPolicy.Public)]
    [InlineData("v1-meetings-create", EndpointAccessPolicy.InternalOnly)]
    [InlineData("v1-meetings-get-by-id", EndpointAccessPolicy.InternalOnly)]
    [InlineData("v1-meetings-get-by-group", EndpointAccessPolicy.InternalOnly)]
    [InlineData("v1-meetings-consent", EndpointAccessPolicy.InternalOnly)]
    [InlineData("v1-meetings-join-token", EndpointAccessPolicy.InternalOnly)]
    [InlineData("v1-meetings-identity-token", EndpointAccessPolicy.InternalOnly)]
    [InlineData("v1-meetings-link", EndpointAccessPolicy.InternalOnly)]
    [InlineData("v1-recordings-start", EndpointAccessPolicy.InternalOnly)]
    [InlineData("v1-recordings-stop", EndpointAccessPolicy.InternalOnly)]
    [InlineData("v1-recordings-list", EndpointAccessPolicy.InternalOnly)]
    [InlineData("v1-recordings-get", EndpointAccessPolicy.InternalOnly)]
    [InlineData("v1-meetings-transcriptions-submit", EndpointAccessPolicy.InternalOnly)]
    [InlineData("v1-transcriptions-status", EndpointAccessPolicy.InternalOnly)]
    [InlineData("v1-transcriptions-files", EndpointAccessPolicy.InternalOnly)]
    [InlineData("v1-transcriptions-content", EndpointAccessPolicy.InternalOnly)]
    [InlineData("v1-transcriptions-speaker-content", EndpointAccessPolicy.InternalOnly)]
    [InlineData("v1-transcriptions-cancel", EndpointAccessPolicy.InternalOnly)]
    [InlineData("v1-transcriptions-delete", EndpointAccessPolicy.InternalOnly)]
    public void EndpointAccessPolicies_ClassifiesFunctions(string functionName, EndpointAccessPolicy expected)
    {
        Assert.Equal(expected, EndpointAccessPolicies.GetPolicy(functionName));
    }

    [Fact]
    public void EndpointAccessPolicies_CoversEveryHttpTriggeredFunctionExplicitly()
    {
        var httpFunctionNames = typeof(HealthFunctions).Assembly
            .GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            .Where(method => method.GetCustomAttribute<FunctionAttribute>() is not null)
            .Where(method => method.GetParameters().Any(parameter => parameter.GetCustomAttributes<HttpTriggerAttribute>(inherit: false).Any()))
            .Select(method => method.GetCustomAttribute<FunctionAttribute>()!.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        var configuredNames = EndpointAccessPolicies.KnownHttpFunctions
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(configuredNames, httpFunctionNames);
    }

    [Fact]
    public void EndpointAccessPolicies_ThrowsForUnknownFunction()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => EndpointAccessPolicies.GetPolicy("unmapped-http-function"));

        Assert.Contains("No endpoint access policy is configured", exception.Message);
    }

    [Fact]
    public void ValidateAuthorization_RejectsMissingBearer()
    {
        var failure = InternalApiAuthMiddleware.ValidateAuthorization("expected-token", null);

        Assert.NotNull(failure);
        Assert.Equal(HttpStatusCode.Unauthorized, failure?.StatusCode);
    }

    [Fact]
    public void ValidateAuthorization_RejectsInvalidBearer()
    {
        var failure = InternalApiAuthMiddleware.ValidateAuthorization("expected-token", "Bearer wrong-token");

        Assert.NotNull(failure);
        Assert.Equal(HttpStatusCode.Forbidden, failure?.StatusCode);
    }
}
