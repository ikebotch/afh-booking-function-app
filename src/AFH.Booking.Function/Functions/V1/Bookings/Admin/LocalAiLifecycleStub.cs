using AFH.Booking.Application.Models.Auth;
using AFH.Booking.Contracts.V1.Responses;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace AFH.Booking.Function.Functions.V1.Bookings;

internal static class LocalAiLifecycleStub
{
    public const string ConfigurationKey = "Booking:AiTools:EnableLocalLifecycleStub";
    public const string EventId = "local-ai-lifecycle-event";
    public const string StepId = "local-ai-lifecycle-step";
    public const string EventType = "LocalAiGatewayProof";
    public const string NewState = "LocalProof";

    public static bool IsEnabled(IHostEnvironment hostEnvironment, IConfiguration configuration)
    {
        return hostEnvironment.IsDevelopment()
            && configuration.GetValue<bool>(ConfigurationKey);
    }

    public static BookingLifecycleResponse CreateResponse(
        string bookingId,
        AdviserUserContext user,
        string? correlationId,
        DateTime utcNow)
    {
        return new BookingLifecycleResponse
        {
            Events =
            [
                new BookingLifecycleEventResponse
                {
                    Id = EventId,
                    BookingId = bookingId,
                    TransactionId = "local-ai-transaction",
                    EventType = EventType,
                    PreviousState = null,
                    NewState = NewState,
                    ActorType = "System",
                    ActorId = user.UserId,
                    ReasonCode = "LOCAL_AI_GATEWAY_PROOF",
                    ReasonNotes = "Development-only stub used to prove MCP Gateway to Booking routing without a local SQL dependency.",
                    OccurredUtc = utcNow,
                    CorrelationId = correlationId,
                    SourceSystem = "AFH.AI.McpGateway",
                    PartnerName = null,
                    RelatedBookingId = null,
                    TriggerReason = "local-dev",
                    Steps =
                    [
                        new BookingLifecycleStepResponse
                        {
                            Id = StepId,
                            LifecycleEventId = EventId,
                            StepName = "GatewayRouting",
                            Sequence = 1,
                            Status = "Succeeded",
                            StartedUtc = utcNow,
                            CompletedUtc = utcNow,
                            ErrorCode = null,
                            ErrorDetails = null,
                            CorrelationId = correlationId
                        }
                    ]
                }
            ]
        };
    }
}
