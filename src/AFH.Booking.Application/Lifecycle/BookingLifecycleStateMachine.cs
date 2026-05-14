using AFH.Booking.Application.Common;

namespace AFH.Booking.Application.Lifecycle;

public static class BookingLifecycleStateMachine
{
    private static readonly HashSet<string> ValidStates = new(StringComparer.OrdinalIgnoreCase)
    {
        LifecycleStates.Booked,
        LifecycleStates.Rearranged,
        LifecycleStates.Cancelled,
        LifecycleStates.NoShow
    };

    private static readonly Dictionary<string, HashSet<string>> AllowedTransitions = new(StringComparer.OrdinalIgnoreCase)
    {
        [LifecycleStates.Booked] =
        [
            LifecycleStates.Rearranged,
            LifecycleStates.Cancelled,
            LifecycleStates.NoShow
        ],
        [LifecycleStates.Rearranged] =
        [
            LifecycleStates.Rearranged,
            LifecycleStates.Cancelled,
            LifecycleStates.NoShow
        ],
        [LifecycleStates.Cancelled] = [],
        [LifecycleStates.NoShow] = []
    };

    public static void Validate(string? previousState, string newState)
    {
        if (string.IsNullOrWhiteSpace(newState))
            throw new InvalidOperationException("Lifecycle new state is required.");

        if (!ValidStates.Contains(newState))
            throw new InvalidOperationException($"Lifecycle state '{newState}' is not valid.");

        if (string.IsNullOrWhiteSpace(previousState))
            return;

        if (!ValidStates.Contains(previousState))
            throw new InvalidOperationException($"Lifecycle previous state '{previousState}' is not valid.");

        if (string.Equals(previousState, newState, StringComparison.OrdinalIgnoreCase))
            return;

        if (!AllowedTransitions.TryGetValue(previousState, out var allowed) ||
            !allowed.Contains(newState))
        {
            throw new InvalidOperationException(
                $"Lifecycle transition '{previousState}' -> '{newState}' is not valid.");
        }
    }

    public static string ResolveStateForEventType(string eventType)
    {
        return eventType switch
        {
            LifecycleEventTypes.Booked => LifecycleStates.Booked,
            LifecycleEventTypes.Rearranged => LifecycleStates.Rearranged,
            LifecycleEventTypes.Cancelled => LifecycleStates.Cancelled,
            LifecycleEventTypes.NoShow => LifecycleStates.NoShow,
            _ => throw new InvalidOperationException($"Lifecycle event type '{eventType}' does not map to a valid lifecycle state.")
        };
    }
}