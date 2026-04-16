using AFH.Acs.Application.Abstractions.Meetings;
using AFH.Acs.Application.Models;
using AFH.Acs.Domain.Entities;

namespace AFH.Acs.Application.Services.Meetings;

public sealed class MeetingLinkService(string joinBaseUrl) : IMeetingLinkService
{
    public Task<MeetingLink> CreateAsync(CreateMeetingLinkCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (string.IsNullOrWhiteSpace(command.BookingId))
            throw new ArgumentException("BookingId is required.", nameof(command));

        var joinCode = Guid.NewGuid().ToString("N");
        var normalizedBaseUrl = string.IsNullOrWhiteSpace(joinBaseUrl) ? "http://localhost:5173" : joinBaseUrl.TrimEnd('/');

        return Task.FromResult(new MeetingLink
        {
            BookingId = command.BookingId.Trim(),
            GroupId = joinCode,
            JoinCode = joinCode,
            JoinUrl = $"{normalizedBaseUrl}/meeting/{joinCode}"
        });
    }
}
