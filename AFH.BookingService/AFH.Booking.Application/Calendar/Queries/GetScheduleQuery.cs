
namespace AFH.Booking.Application.Calendar.Queries;
public sealed record GetScheduleQuery(string AdviserId, DateTime StartUtc, DateTime EndUtc);
