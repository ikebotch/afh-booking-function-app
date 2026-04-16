using AFH.Acs.Domain.Entities;

namespace AFH.Acs.Application.Abstractions;

public interface IJoinTokenIssuer
{
    Task<IssuedJoinToken> IssueForMeetingAsync(MeetingSession session, string displayName, string role, CancellationToken ct = default);
    Task<IssuedIdentityToken> IssueIdentityTokenAsync(CancellationToken ct = default);
}
