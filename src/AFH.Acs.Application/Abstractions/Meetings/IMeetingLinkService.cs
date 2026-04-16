using AFH.Acs.Application.Models;
using AFH.Acs.Domain.Entities;

namespace AFH.Acs.Application.Abstractions.Meetings;

public interface IMeetingLinkService
{
    Task<MeetingLink> CreateAsync(CreateMeetingLinkCommand command, CancellationToken ct = default);
}
