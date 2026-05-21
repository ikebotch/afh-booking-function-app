using AFH.Booking.Application.Models.Common;

namespace AFH.Booking.Application.Models.Availability;

public sealed class GetAvailabilityResponse
{
    public string TransactionId { get; set; } = default!;
    public List<AdviserSlotsDto> Advisers { get; set; } = new();
    public PageResult<object> Paging { get; set; } = new();
}
