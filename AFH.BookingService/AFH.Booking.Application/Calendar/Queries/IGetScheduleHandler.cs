using AFH.Booking.Application.Common;
using AFH.Booking.Contracts.Dtos;

namespace AFH.Booking.Application.Calendar.Queries
{
    public interface IGetScheduleHandler
    {
        Task<Result<ScheduleDto>> HandleAsync(GetScheduleQuery query, CancellationToken ct);
    }
}
