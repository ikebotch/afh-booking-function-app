using AFH.Booking.Contracts.V1.Common;
using AFH.Booking.Contracts.V1.Dtos.Availability;
using System.Text.Json.Serialization;

namespace AFH.Booking.Contracts.V1.Responses;

public sealed class GetAvailabilityResponse
{
    public string TransactionId { get; set; } = default!;

    public List<AdviserSlotsDto> Items { get; set; } = new();

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PageResultDto<object>? Paging { get; set; }
}
