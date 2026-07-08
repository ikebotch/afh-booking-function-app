using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.Models.Lifecycle;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Bookings.Commands;
using AFH.Booking.Function.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace AFH.Booking.Function.Functions.V1.Bookings;

[BookingOpenApiTag("Bookings")]
public sealed class GetBookingLifecycleFunction
{
    private readonly IBookingDetailsService _details;
    private readonly ILifecycleEventRepository _lifecycleEvents;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _hostEnvironment;

    public GetBookingLifecycleFunction(
        IBookingDetailsService details,
        ILifecycleEventRepository lifecycleEvents,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        _details = details;
        _lifecycleEvents = lifecycleEvents;
        _configuration = configuration;
        _hostEnvironment = hostEnvironment;
    }

    [Function("Bookings_GetBookingLifecycle")]
    [BookingOpenApiOperation(
        "Bookings",
        "Get booking lifecycle",
        ResponseType = typeof(BookingLifecycleResponse))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "v1/bookings/{bookingId}/lifecycle")] HttpRequestData req,
        FunctionContext context,
        string bookingId,
        CancellationToken ct)
    {
        var authResult = await BookingFunctionActorContext.BuildAuthenticatedAsync(req, context, ct);
        if (!authResult.IsSuccess)
            return authResult.Response!;

        if (LocalAiLifecycleStub.IsEnabled(_hostEnvironment, _configuration))
        {
            return await req.OkJsonAsync(
                LocalAiLifecycleStub.CreateResponse(
                    bookingId,
                    authResult.User!,
                    BookingChangeRequestContext.GetCorrelationId(req),
                    DateTime.UtcNow),
                ct);
        }

        var details = await _details.HandleAsync(new GetBookingDetailsQuery { BookingId = bookingId }, ct);
        if (!details.IsSuccess)
        {
            return await req.ProblemAsync(
                details.StatusCode,
                details.ErrorMessage ?? "Request failed.",
                ct,
                details.ErrorCode);
        }

        var forbidden = await BookingFunctionActorContext.EnsureCanAccessBookingAsync(req, authResult.User!, details.Value!, ct);
        if (forbidden is not null)
            return forbidden;

        var events = await _lifecycleEvents.ListByBookingAsync(details.Value!.BookingId, ct);
        return await req.OkJsonAsync(new BookingLifecycleResponse
        {
            Events = events.Select(Map).ToList()
        }, ct);
    }

    private static BookingLifecycleEventResponse Map(LifecycleEventRecord record)
        => new()
        {
            Id = record.Id,
            BookingId = record.BookingId,
            TransactionId = record.TransactionId,
            EventType = record.EventType,
            PreviousState = record.PreviousState,
            NewState = record.NewState,
            ActorType = record.ActorType,
            ActorId = record.ActorId,
            ReasonCode = record.ReasonCode,
            ReasonNotes = record.ReasonNotes,
            OccurredUtc = record.OccurredUtc,
            CorrelationId = record.CorrelationId,
            SourceSystem = record.SourceSystem,
            PartnerName = record.PartnerName,
            RelatedBookingId = record.RelatedBookingId,
            TriggerReason = record.TriggerReason,
            Steps = record.Steps.Select(Map).ToList()
        };

    private static BookingLifecycleStepResponse Map(LifecycleStepRecord step)
        => new()
        {
            Id = step.Id,
            LifecycleEventId = step.LifecycleEventId,
            StepName = step.StepName,
            Sequence = step.Sequence,
            Status = step.Status,
            StartedUtc = step.StartedUtc,
            CompletedUtc = step.CompletedUtc,
            ErrorCode = step.ErrorCode,
            ErrorDetails = step.ErrorDetails,
            CorrelationId = step.CorrelationId
        };

}
