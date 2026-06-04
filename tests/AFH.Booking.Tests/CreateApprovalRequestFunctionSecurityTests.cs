using AFH.Booking.Application.Abstractions.Approvals;
using AFH.Booking.Application.Models.Approvals;
using AFH.Booking.Application.Models.Auth;
using AFH.Booking.Domain.Auth;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Function.Auth;
using AFH.Booking.Function.Functions.V1.Bookings;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Moq;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;

namespace AFH.Booking.Tests;

public sealed class CreateApprovalRequestFunctionSecurityTests
{
    [Fact]
    public async Task Run_UnauthenticatedRequest_IsRejected()
    {
        var approvals = new Mock<IApprovalWorkflowService>();
        var sut = new CreateApprovalRequestFunction(approvals.Object);
        var request = CreateJsonRequest("""{"changeType":"Cancel","reasonCode":"CLIENT_REQUEST"}""");

        var response = await sut.Run(request, request.FunctionContext, "booking-1", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        approvals.Verify(x => x.CreateAsync(It.IsAny<CreateApprovalWorkflowRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Run_UserWithoutCreatePermission_IsRejected()
    {
        var approvals = new Mock<IApprovalWorkflowService>();
        var sut = new CreateApprovalRequestFunction(approvals.Object);
        var request = CreateJsonRequest("""{"changeType":"Cancel","reasonCode":"CLIENT_REQUEST"}""");
        SetDomainUser(request, permissions: [BookingPermissionNames.ApprovalsRead]);

        var response = await sut.Run(request, request.FunctionContext, "booking-1", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        approvals.Verify(x => x.CreateAsync(It.IsAny<CreateApprovalWorkflowRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Run_AdviserWithCreatePermission_CanCreateApprovalRequest()
    {
        CreateApprovalWorkflowRequest? captured = null;
        var approvals = CreateApprovals(request => captured = request);
        var sut = new CreateApprovalRequestFunction(approvals.Object);
        var request = CreateJsonRequest("""{"changeType":"Cancel","reasonCode":"CLIENT_REQUEST","reasonDetail":"Client asked"}""");
        request.Headers.Add("x-correlation-id", "corr-1");
        SetDomainUser(request, userId: "adviser-auth", displayName: "Ada Adviser");

        var response = await sut.Run(request, request.FunctionContext, "booking-1", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(captured);
        Assert.Equal("adviser-auth", captured!.RequesterId);
        Assert.Equal("adviser-auth", captured.ActorContext?.ActorId);
        Assert.Equal(BookingActorContext.ActorAdviser, captured.ActorContext?.ActorType);
        Assert.Equal(BookingActorContext.SourceAdviserPortal, captured.ActorContext?.SourceApplication);
        Assert.Equal("corr-1", captured.ActorContext?.CorrelationId);
    }

    [Fact]
    public async Task Run_RequestBodyActorSpoofing_DoesNotOverrideAuthenticatedActor()
    {
        CreateApprovalWorkflowRequest? captured = null;
        var approvals = CreateApprovals(request => captured = request);
        var sut = new CreateApprovalRequestFunction(approvals.Object);
        var request = CreateJsonRequest(
            """{"changeType":"Cancel","requestedBy":"Manager","requesterId":"spoofed-adviser","reasonCode":"CLIENT_REQUEST"}""");
        SetDomainUser(request, userId: "adviser-auth");

        var response = await sut.Run(request, request.FunctionContext, "booking-1", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(captured);
        Assert.Equal("Adviser", captured!.RequestedBy);
        Assert.Equal("adviser-auth", captured.RequesterId);
        Assert.Equal("adviser-auth", captured.ActorContext?.ActorId);
    }

    [Fact]
    public void FunctionNameAndRoute_RemainUnchanged()
    {
        var method = typeof(CreateApprovalRequestFunction).GetMethod(nameof(CreateApprovalRequestFunction.Run));
        Assert.NotNull(method);
        var function = method.GetCustomAttribute<FunctionAttribute>();
        var trigger = method.GetParameters()[0].GetCustomAttribute<HttpTriggerAttribute>();

        Assert.NotNull(trigger);
        Assert.NotNull(trigger.Methods);
        Assert.Equal("Bookings_CreateApprovalRequest", function?.Name);
        Assert.Equal(AuthorizationLevel.Anonymous, trigger.AuthLevel);
        Assert.Equal("v1/bookings/{bookingId}/approval-requests", trigger.Route);
        Assert.Contains("post", trigger.Methods, StringComparer.OrdinalIgnoreCase);
    }

    private static Mock<IApprovalWorkflowService> CreateApprovals(Action<CreateApprovalWorkflowRequest> capture)
    {
        var approvals = new Mock<IApprovalWorkflowService>();
        approvals.Setup(x => x.CreateAsync(It.IsAny<CreateApprovalWorkflowRequest>(), It.IsAny<CancellationToken>()))
            .Callback<CreateApprovalWorkflowRequest, CancellationToken>((request, _) => capture(request))
            .ReturnsAsync((CreateApprovalWorkflowRequest request, CancellationToken _) => new ApprovalRequestResponse
            {
                RequestId = "approval-1",
                BookingId = request.BookingId,
                TransactionId = "tx-1",
                ChangeType = request.ChangeType,
                RequestedBy = request.ActorContext?.ActorType ?? request.RequestedBy,
                RequesterId = request.ActorContext?.ActorId ?? request.RequesterId,
                Status = "Pending",
                RequestedUtc = DateTime.UtcNow
            });
        return approvals;
    }

    private static TestHttpRequestData CreateJsonRequest(string json)
    {
        var request = TestHttpRequestData.Create(method: "POST");
        var bytes = Encoding.UTF8.GetBytes(json);
        request.Body.Write(bytes, 0, bytes.Length);
        request.Body.Position = 0;
        return request;
    }

    private static void SetDomainUser(
        TestHttpRequestData request,
        string userId = "adviser-1",
        string displayName = "Adviser One",
        IReadOnlyList<string>? permissions = null)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("oid", userId),
            new Claim("name", displayName)
        ], "Test"));

        request.FunctionContext.SetDomainUserPrincipal(
            principal,
            new AdviserUserContext
            {
                UserId = userId,
                DisplayName = displayName,
                Email = $"{userId}@example.test",
                Permissions = permissions ?? [BookingPermissionNames.ApprovalRequestsCreate],
                Roles = ["Adviser"]
            });
    }
}
