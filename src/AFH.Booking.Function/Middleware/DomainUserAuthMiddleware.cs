
using AFH.Booking.Application.Abstractions.Auth;
using AFH.Booking.Domain;
using AFH.Booking.Function.Auth;
using AFH.Booking.Function.Http;
using AFH.Booking.Function.Security;
using AFH.Booking.Infrastructure.Logging;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace AFH.Booking.Function.Middleware;

public sealed class DomainUserAuthMiddleware : IFunctionsWorkerMiddleware
{
    private readonly ILogger<DomainUserAuthMiddleware> _logger;

    public DomainUserAuthMiddleware(
        ILogger<DomainUserAuthMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var request = await context.GetHttpRequestDataAsync();
        if (request is null)
        {
            await next(context);
            return;
        }

        var requirement = EndpointAccessPolicies.GetRequirement(context.FunctionDefinition.Name);
        if (requirement.Policy is not EndpointAccessPolicy.UserAuthenticated)
        {
            await next(context);
            return;
        }

        var validator = context.InstanceServices.GetRequiredService<IEntraTokenValidator>();
        var permissions = context.InstanceServices.GetRequiredService<ICurrentUserPermissionClient>();
        var access = await DomainUserAccessAuthorizer.AuthorizeAsync(
            request,
            requirement,
            validator,
            permissions,
            CancellationToken.None);

        if (!access.IsAllowed)
        {
            if (!string.IsNullOrWhiteSpace(access.RequiredPermission))
            {
                await WriteAuthorizationDecisionLogAsync(
                    context,
                    request,
                    access.User?.Email ?? (access.Principal is null ? null : GetEmail(access.Principal)),
                    access.User?.UserId ?? (access.Principal is null ? null : GetUserId(access.Principal)),
                    access.RequiredPermission,
                    authorised: false);
            }

            context.GetInvocationResult().Value = access.FailureResponse;
            return;
        }

        context.SetDomainUserPrincipal(access.Principal!, access.User);

        if (!string.IsNullOrWhiteSpace(access.RequiredPermission))
        {
            await WriteAuthorizationDecisionLogAsync(
                context,
                request,
                access.User?.Email ?? GetEmail(access.Principal!),
                access.User?.UserId ?? GetUserId(access.Principal!),
                access.RequiredPermission,
                authorised: true);
        }

        await next(context);
    }

    private Task WriteFailureLogAsync(
        FunctionContext context,
        HttpRequestData request,
        HttpStatusCode statusCode,
        string failureCode,
        string detail)
    {
        var correlationId = context.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var value)
            ? value?.ToString()
            : null;

        try
        {
            var sink = context.InstanceServices.GetService<IApplicationLogSink>();
            var loggingOptions = context.InstanceServices.GetService<IOptions<ApplicationLoggingOptions>>()?.Value;
            if (sink is null || loggingOptions is null)
                return Task.CompletedTask;

            return sink.WriteAsync(new ApplicationLogEntry
            {
                OccurredUtc = DateTime.UtcNow,
                Level = statusCode == HttpStatusCode.InternalServerError ? "Error" : "Warning",
                Category = "Authorization",
                Operation = context.FunctionDefinition.Name,
                CorrelationId = correlationId,
                ContextId = context.InvocationId,
                EventType = failureCode,
                Result = "Failure",
                Message = detail,
                PayloadJson = ApplicationLogPayloadHelper.Serialize(new
                {
                    FailureSource = nameof(DomainUserAuthMiddleware),
                    FailureCode = failureCode,
                    StatusCode = (int)statusCode,
                    Path = request.Url.AbsolutePath,
                    Method = request.Method,
                    CorrelationId = correlationId
                }, loggingOptions)
            }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to persist domain-user auth failure log. Function={FunctionName} CorrelationId={CorrelationId}",
                context.FunctionDefinition.Name,
                correlationId);
            return Task.CompletedTask;
        }
    }

    private Task WriteAuthorizationDecisionLogAsync(
        FunctionContext context,
        HttpRequestData request,
        string? email,
        string? userId,
        string requiredPermission,
        bool authorised)
    {
        var correlationId = context.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var value)
            ? value?.ToString()
            : null;

        _logger.LogInformation(
            "Booking domain-user authorization decision. Function={FunctionName} UserEmail={UserEmail} UserId={UserId} RequiredPermission={RequiredPermission} Authorised={Authorised}",
            context.FunctionDefinition.Name,
            email,
            userId,
            requiredPermission,
            authorised);

        try
        {
            var sink = context.InstanceServices.GetService<IApplicationLogSink>();
            var loggingOptions = context.InstanceServices.GetService<IOptions<ApplicationLoggingOptions>>()?.Value;
            if (sink is null || loggingOptions is null)
                return Task.CompletedTask;

            return sink.WriteAsync(new ApplicationLogEntry
            {
                OccurredUtc = DateTime.UtcNow,
                Level = authorised ? "Information" : "Warning",
                Category = "Authorization",
                Operation = context.FunctionDefinition.Name,
                CorrelationId = correlationId,
                UserId = email ?? userId,
                ContextId = context.InvocationId,
                EventType = "DomainUserPermission",
                Result = authorised ? "Success" : "Failure",
                Message = authorised ? "Domain user permission granted." : "Domain user permission denied.",
                PayloadJson = ApplicationLogPayloadHelper.Serialize(new
                {
                    FailureSource = nameof(DomainUserAuthMiddleware),
                    Path = request.Url.AbsolutePath,
                    Method = request.Method,
                    UserEmail = email,
                    UserId = userId,
                    RequiredPermission = requiredPermission,
                    Authorised = authorised,
                    CorrelationId = correlationId
                }, loggingOptions)
            }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to persist domain-user authorization decision log. Function={FunctionName} CorrelationId={CorrelationId}",
                context.FunctionDefinition.Name,
                correlationId);
            return Task.CompletedTask;
        }
    }

    private static string GetEmail(ClaimsPrincipal principal) =>
        GetClaimValue(principal, ClaimTypes.Upn, "preferred_username", "upn", ClaimTypes.Email, "email")
        ?? string.Empty;

    private static string GetUserId(ClaimsPrincipal principal) =>
        GetClaimValue(
            principal,
            "oid",
            "http://schemas.microsoft.com/identity/claims/objectidentifier",
            ClaimTypes.NameIdentifier)
        ?? GetEmail(principal);

    private static string? GetClaimValue(ClaimsPrincipal principal, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var claim = principal.FindFirst(claimType);
            if (!string.IsNullOrWhiteSpace(claim?.Value))
                return claim.Value;
        }

        return null;
    }
}
