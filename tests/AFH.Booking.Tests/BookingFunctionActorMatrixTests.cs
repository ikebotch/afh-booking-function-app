using System.Net;
using System.Security.Claims;
using System.Text;
using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Application.Models.Auth;
using AFH.Booking.Contracts.V1.Requests;
using AFH.Booking.Domain.Auth;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Function.Auth;
using AFH.Booking.Function.Functions.V1.Bookings;
using Microsoft.Extensions.Logging.Abstractions;

namespace AFH.Booking.Tests;

public sealed class BookingFunctionActorMatrixTests
{
    [Fact]
    public async Task LeadTechCancel_MapsLeadTechActorAndUsesSharedCancelService()
    {
        var service = new CapturingCancelBookingService();
        var sut = new LeadTechCancelBookingFunction(service);
        var request = CreateJsonRequest("""{"reasonCode":"LEADTECH_REQUEST","reasonDetail":"Client called LeadTech"}""");
        request.Headers.Add("x-correlation-id", "corr-leadtech");

        var response = await sut.Run(request, " booking-1 ", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("booking-1", service.LastCommand?.BookingId);
        Assert.Equal(LifecycleActors.LeadTech, service.LastCommand?.RequestedBy);
        Assert.Equal(BookingActorContext.SourceLeadTech, service.LastCommand?.ActorContext?.SourceApplication);
        Assert.Equal(LifecycleActors.LeadTech, service.LastCommand?.ActorContext?.ActorType);
        Assert.Equal("corr-leadtech", service.LastCommand?.CorrelationId);
        Assert.Equal("LEADTECH_REQUEST", service.LastCommand?.ReasonCode);
    }

    [Fact]
    public async Task ManagerCancel_MapsAuthenticatedManagerActorAndUsesSharedCancelService()
    {
        var service = new CapturingCancelBookingService();
        var sut = new CancelBookingFunction(service, new StubBookingDetailsService(), NullLogger<CancelBookingFunction>.Instance);
        var request = CreateJsonRequest("""{"requestedBy":"Client","reasonCode":"ADMIN_REQUEST","reasonDetail":"Back office change"}""");
        request.Headers.Add("x-correlation-id", "corr-admin");
        SetDomainUser(request, "manager-1", "Mina Manager", [BookingPermissionNames.CancelDirect], ["Manager"]);

        var response = await sut.Run(request, request.FunctionContext, " booking-1 ", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("booking-1", service.LastCommand?.BookingId);
        Assert.Equal(BookingActorContext.ActorManager, service.LastCommand?.RequestedBy);
        Assert.NotEqual(LifecycleActors.Client, service.LastCommand?.RequestedBy);
        Assert.Equal("manager-1", service.LastCommand?.ActorContext?.ActorId);
        Assert.Equal(BookingActorContext.SourceManagerPortal, service.LastCommand?.ActorContext?.SourceApplication);
        Assert.Equal(BookingActorContext.ActorManager, service.LastCommand?.ActorContext?.ActorType);
        Assert.Equal("corr-admin", service.LastCommand?.CorrelationId);
    }

    [Fact]
    public async Task AdminCancel_MapsAuthenticatedAdminActorAndUsesSharedCancelService()
    {
        var service = new CapturingCancelBookingService();
        var sut = new CancelBookingFunction(service, new StubBookingDetailsService(), NullLogger<CancelBookingFunction>.Instance);
        var request = CreateJsonRequest("""{"reasonCode":"ADMIN_REQUEST","reasonDetail":"Back office change"}""");
        SetDomainUser(request, "admin-1", "Ada Admin", [BookingPermissionNames.CancelDirect], ["Admin"]);

        var response = await sut.Run(request, request.FunctionContext, "booking-1", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(BookingActorContext.ActorInternalAdmin, service.LastCommand?.RequestedBy);
        Assert.Equal("admin-1", service.LastCommand?.ActorContext?.ActorId);
        Assert.Equal(BookingActorContext.SourceInternalAdmin, service.LastCommand?.ActorContext?.SourceApplication);
        Assert.Equal(BookingActorContext.ActorInternalAdmin, service.LastCommand?.ActorContext?.ActorType);
    }

    [Fact]
    public async Task ManagerCancel_RequiresReasonCode()
    {
        var service = new CapturingCancelBookingService();
        var sut = new CancelBookingFunction(service, new StubBookingDetailsService(), NullLogger<CancelBookingFunction>.Instance);
        var request = CreateJsonRequest("""{"reasonDetail":"Back office change"}""");
        SetDomainUser(request, "manager-1", "Mina Manager", [BookingPermissionNames.CancelDirect], ["Manager"]);

        var response = await sut.Run(request, request.FunctionContext, "booking-1", CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(service.LastCommand);
    }

    [Fact]
    public async Task AdviserWithoutDirectCancelPermission_IsRejected()
    {
        var service = new CapturingCancelBookingService();
        var sut = new CancelBookingFunction(service, new StubBookingDetailsService(), NullLogger<CancelBookingFunction>.Instance);
        var request = CreateJsonRequest("""{"reasonCode":"ADMIN_REQUEST"}""");
        SetDomainUser(request, "adviser-1", "Ava Adviser", [BookingPermissionNames.ApprovalRequestsCreate], ["Adviser"]);

        var response = await sut.Run(request, request.FunctionContext, "booking-1", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(service.LastCommand);
    }

    [Fact]
    public async Task UnauthenticatedDirectCancel_IsRejected()
    {
        var service = new CapturingCancelBookingService();
        var sut = new CancelBookingFunction(service, new StubBookingDetailsService(), NullLogger<CancelBookingFunction>.Instance);
        var request = CreateJsonRequest("""{"reasonCode":"ADMIN_REQUEST"}""");

        var response = await sut.Run(request, request.FunctionContext, "booking-1", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(service.LastCommand);
    }

    [Fact]
    public async Task LeadTechRearrange_MapsLeadTechActorAndUsesSharedRearrangeService()
    {
        var service = new CapturingRearrangeBookingService();
        var sut = new LeadTechRearrangeBookingFunction(service);
        var request = CreateJsonRequest("""{"newSlotId":"slot-new","reasonCode":"LEADTECH_RESCHEDULE"}""");
        request.Headers.Add("x-correlation-id", "corr-leadtech");

        var response = await sut.Run(request, " booking-old ", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("booking-old", service.LastCommand?.BookingId);
        Assert.Equal("slot-new", service.LastCommand?.NewSlotId);
        Assert.Equal(LifecycleActors.LeadTech, service.LastCommand?.RequestedBy);
        Assert.Equal(BookingActorContext.SourceLeadTech, service.LastCommand?.ActorContext?.SourceApplication);
        Assert.Equal(LifecycleActors.LeadTech, service.LastCommand?.ActorContext?.ActorType);
        Assert.Equal("corr-leadtech", service.LastCommand?.CorrelationId);
    }

    [Fact]
    public async Task ManagerRearrange_MapsAuthenticatedManagerActorAndUsesSharedRearrangeService()
    {
        var service = new CapturingRearrangeBookingService();
        var sut = new RearrangeBookingFunction(service, new StubBookingDetailsService());
        var request = CreateJsonRequest("""{"newSlotId":"slot-new","requestedBy":"Client","reasonCode":"ADMIN_RESCHEDULE"}""");
        request.Headers.Add("x-correlation-id", "corr-admin");
        SetDomainUser(request, "manager-1", "Mina Manager", [BookingPermissionNames.RearrangeDirect], ["Manager"]);

        var response = await sut.Run(request, request.FunctionContext, " booking-old ", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("booking-old", service.LastCommand?.BookingId);
        Assert.Equal("slot-new", service.LastCommand?.NewSlotId);
        Assert.Equal(BookingActorContext.ActorManager, service.LastCommand?.RequestedBy);
        Assert.NotEqual(LifecycleActors.Client, service.LastCommand?.RequestedBy);
        Assert.Equal("manager-1", service.LastCommand?.ActorContext?.ActorId);
        Assert.Equal(BookingActorContext.SourceManagerPortal, service.LastCommand?.ActorContext?.SourceApplication);
        Assert.Equal(BookingActorContext.ActorManager, service.LastCommand?.ActorContext?.ActorType);
        Assert.Equal("corr-admin", service.LastCommand?.CorrelationId);
    }

    [Fact]
    public async Task AdminRearrange_MapsAuthenticatedAdminActorAndUsesSharedRearrangeService()
    {
        var service = new CapturingRearrangeBookingService();
        var sut = new RearrangeBookingFunction(service, new StubBookingDetailsService());
        var request = CreateJsonRequest("""{"newSlotId":"slot-new","reasonCode":"ADMIN_RESCHEDULE"}""");
        SetDomainUser(request, "admin-1", "Ada Admin", [BookingPermissionNames.RearrangeDirect], ["Admin"]);

        var response = await sut.Run(request, request.FunctionContext, "booking-old", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(BookingActorContext.ActorInternalAdmin, service.LastCommand?.RequestedBy);
        Assert.Equal("admin-1", service.LastCommand?.ActorContext?.ActorId);
        Assert.Equal(BookingActorContext.SourceInternalAdmin, service.LastCommand?.ActorContext?.SourceApplication);
        Assert.Equal(BookingActorContext.ActorInternalAdmin, service.LastCommand?.ActorContext?.ActorType);
    }

    [Fact]
    public async Task ManagerRearrange_RequiresReasonCode()
    {
        var service = new CapturingRearrangeBookingService();
        var sut = new RearrangeBookingFunction(service, new StubBookingDetailsService());
        var request = CreateJsonRequest("""{"newSlotId":"slot-new"}""");
        SetDomainUser(request, "manager-1", "Mina Manager", [BookingPermissionNames.RearrangeDirect], ["Manager"]);

        var response = await sut.Run(request, request.FunctionContext, "booking-old", CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(service.LastCommand);
    }

    [Fact]
    public async Task AdviserWithoutDirectRearrangePermission_IsRejected()
    {
        var service = new CapturingRearrangeBookingService();
        var sut = new RearrangeBookingFunction(service, new StubBookingDetailsService());
        var request = CreateJsonRequest("""{"newSlotId":"slot-new","reasonCode":"ADMIN_RESCHEDULE"}""");
        SetDomainUser(request, "adviser-1", "Ava Adviser", [BookingPermissionNames.ApprovalRequestsCreate], ["Adviser"]);

        var response = await sut.Run(request, request.FunctionContext, "booking-old", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(service.LastCommand);
    }

    [Fact]
    public async Task UnauthenticatedDirectRearrange_IsRejected()
    {
        var service = new CapturingRearrangeBookingService();
        var sut = new RearrangeBookingFunction(service, new StubBookingDetailsService());
        var request = CreateJsonRequest("""{"newSlotId":"slot-new","reasonCode":"ADMIN_RESCHEDULE"}""");

        var response = await sut.Run(request, request.FunctionContext, "booking-old", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(service.LastCommand);
    }

    private static TestHttpRequestData CreateJsonRequest(string json)
    {
        var request = TestHttpRequestData.Create(method: "POST");
        using var writer = new StreamWriter(request.Body, Encoding.UTF8, leaveOpen: true);
        writer.Write(json);
        writer.Flush();
        request.Body.Position = 0;
        return request;
    }

    private static void SetDomainUser(
        TestHttpRequestData request,
        string userId,
        string displayName,
        IReadOnlyList<string> permissions,
        IReadOnlyList<string> roles)
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
                AdviserId = "adv-1",
                DisplayName = displayName,
                Email = $"{userId}@example.test",
                Permissions = permissions,
                Roles = roles
            });
    }

    private sealed class CapturingCancelBookingService : ICancelBookingService
    {
        public CancelBookingCommand? LastCommand { get; private set; }

        public Task<Result<CancelBookingResponse>> HandleAsync(CancelBookingCommand cmd, CancellationToken ct)
        {
            LastCommand = cmd;
            return Task.FromResult(Result<CancelBookingResponse>.Ok(new CancelBookingResponse
            {
                BookingId = cmd.BookingId,
                CancelledUtc = new DateTime(2026, 06, 01, 8, 0, 0, DateTimeKind.Utc),
                Status = "Cancelled"
            }));
        }
    }

    private sealed class StubBookingDetailsService : IBookingDetailsService
    {
        public Task<Result<BookingDetailsResponse>> HandleAsync(GetBookingDetailsQuery query, CancellationToken ct)
            => Task.FromResult(Result<BookingDetailsResponse>.Ok(new BookingDetailsResponse
            {
                BookingId = query.BookingId,
                SlotId = "slot-1",
                TransactionId = "tx-1",
                TransactionRef = "TRX-1",
                AdviserId = "adv-1",
                AdviserName = "Adviser One",
                StartUtc = new DateTime(2026, 06, 01, 9, 0, 0, DateTimeKind.Utc),
                EndUtc = new DateTime(2026, 06, 01, 10, 0, 0, DateTimeKind.Utc),
                DurationMinutes = 60,
                Status = "Confirmed"
            }));
    }

    private sealed class CapturingRearrangeBookingService : IRearrangeBookingService
    {
        public RearrangeBookingCommand? LastCommand { get; private set; }

        public Task<Result<RearrangeBookingResponse>> HandleAsync(RearrangeBookingCommand cmd, CancellationToken ct)
        {
            LastCommand = cmd;
            return Task.FromResult(Result<RearrangeBookingResponse>.Ok(new RearrangeBookingResponse
            {
                PreviousBookingId = cmd.BookingId,
                NewBookingId = "booking-new",
                NewSlotId = cmd.NewSlotId,
                PreviousAdviserId = "adv-old",
                PreviousAdviserName = "Old Adviser",
                PreviousStartUtc = new DateTime(2026, 06, 01, 9, 0, 0, DateTimeKind.Utc),
                PreviousEndUtc = new DateTime(2026, 06, 01, 10, 0, 0, DateTimeKind.Utc),
                NewAdviserId = "adv-new",
                NewAdviserName = "New Adviser",
                NewStartUtc = new DateTime(2026, 06, 02, 9, 0, 0, DateTimeKind.Utc),
                NewEndUtc = new DateTime(2026, 06, 02, 10, 0, 0, DateTimeKind.Utc),
                NotificationSummary = "Rescheduled"
            }));
        }
    }
}
