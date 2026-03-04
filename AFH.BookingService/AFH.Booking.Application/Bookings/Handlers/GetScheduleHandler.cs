using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Application.Calendar.Queries;
using AFH.Booking.Application.Common;
using AFH.Booking.Contracts.Dtos;
using AFH.Booking.Contracts.Responses;
using Microsoft.Extensions.Logging;

namespace AFH.Booking.Application.Bookings.Handlers
{
    public sealed class GetScheduleHandler : IGetScheduleHandler
    {
        private readonly IBookingRepository _repo;
        private readonly ILogger<GetScheduleHandler> _logger;

        public GetScheduleHandler(IBookingRepository repo, ILogger<GetScheduleHandler> logger)
        {
            _repo = repo;
            _logger = logger;
        }

        public async Task<Result<ScheduleDto>> HandleAsync(GetScheduleQuery query, CancellationToken ct)
        {
            var bookings = await _repo.GetScheduleAsync(query.AdviserId, query.StartUtc, query.EndUtc, ct);

            var dto = new ScheduleDto
            {
                AdviserId = query.AdviserId,
                StartUtc = query.StartUtc,
                EndUtc = query.EndUtc,
                Bookings = bookings.Select(b => new BookingSummaryDto
                {
                    BookingId = b.Id.Value,
                    Subject = b.Subject,
                    StartUtc = b.StartUtc,
                    EndUtc = b.EndUtc,
                    Status = b.Status.ToString()
                }).ToList()
            };

            _logger.LogInformation("Fetched schedule for AdviserId={AdviserId}", query.AdviserId);

            return Result<ScheduleDto>.Ok(dto);
        }
    }
}
