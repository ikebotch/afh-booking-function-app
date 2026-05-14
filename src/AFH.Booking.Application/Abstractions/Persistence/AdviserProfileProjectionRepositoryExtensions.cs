namespace AFH.Booking.Application.Abstractions.Persistence;

public static class AdviserProfileProjectionRepositoryExtensions
{
    public static async Task<string> ResolveCalendarUserIdAsync(
        this IAdviserProfileProjectionRepository profiles,
        string adviserIdOrMailboxUserId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(adviserIdOrMailboxUserId))
            return adviserIdOrMailboxUserId;

        var value = adviserIdOrMailboxUserId.Trim();
        var profile = await profiles.GetAsync(value, ct);
        if (!string.IsNullOrWhiteSpace(profile?.MailboxUserId))
            return profile.MailboxUserId.Trim();

        return value;
    }
}
