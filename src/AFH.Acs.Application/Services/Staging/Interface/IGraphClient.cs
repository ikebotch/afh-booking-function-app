using AFH.Acs.Recorder.Models;
using System.Threading.Tasks;

namespace AFH.Acs.Recorder.Services.Interface;

public interface IGraphClient
{
    Task<string> GetUsersAsync();
    Task<string> GetCalendarAsync(string start, string end, string calendarUser);
    Task<string> CreateAdviserMeetingAsync(
    GraphMeetingCreateRequest request,
    CancellationToken ct = default);
}
