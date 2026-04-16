using AFH.Acs.Application.Models;
using AFH.Acs.Domain.Entities;

namespace AFH.Acs.Application.Abstractions;

public interface IMeetingLinkService
{
    Task<MeetingLink> CreateAsync(CreateMeetingLinkCommand command, CancellationToken ct = default);
}
