using AFH.Booking.Domain.Bookings.Commands;

namespace AFH.Booking.Application.Bookings;

internal static class BookingChangeValidation
{
    private static readonly HashSet<string> SupportedActors = new(StringComparer.OrdinalIgnoreCase)
    {
        LifecycleActors.Client,
        LifecycleActors.Partner,
        LifecycleActors.Adviser,
        LifecycleActors.System,
        BookingActorContext.ActorManager,
        BookingActorContext.ActorInternalAdmin
    };

    public static Result Validate(CancelBookingCommand command)
    {
        return Validate(command.RequestedBy, command.ReasonCode, command.ReasonDetail);
    }

    public static Result Validate(RearrangeBookingCommand command)
    {
        return Validate(command.RequestedBy, command.ReasonCode, command.ReasonDetail);
    }

    private static Result Validate(string? actorType, string? reasonCode, string? reasonNotes)
    {
        var actor = string.IsNullOrWhiteSpace(actorType) ? LifecycleActors.Unknown : actorType.Trim();
        if (!SupportedActors.Contains(actor))
            return Result.Fail(HttpStatusCode.BadRequest, $"Actor path '{actor}' is not supported.", Errors.Validation);

        if (IsHumanActor(actor) && string.IsNullOrWhiteSpace(reasonCode))
            return Result.Fail(HttpStatusCode.BadRequest, "reasonCode is required for human booking changes.", Errors.ReasonCodeRequired);

        if (!string.IsNullOrWhiteSpace(reasonNotes) && reasonNotes.Trim().Length > 1000)
            return Result.Fail(HttpStatusCode.BadRequest, "reasonDetail must be 1000 characters or fewer.", Errors.Validation);

        return Result.Ok();
    }

    private static bool IsHumanActor(string actorType) =>
        actorType.Equals(LifecycleActors.Client, StringComparison.OrdinalIgnoreCase) ||
        actorType.Equals(LifecycleActors.Partner, StringComparison.OrdinalIgnoreCase) ||
        actorType.Equals(LifecycleActors.Adviser, StringComparison.OrdinalIgnoreCase) ||
        actorType.Equals(BookingActorContext.ActorManager, StringComparison.OrdinalIgnoreCase);
}
