using AFH.Booking.Application.Abstractions.Availability;
using AFH.Booking.Application.Mapping.Availability;
using AFH.Booking.Contracts.V1.Common;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Availability;
using AFH.Booking.Domain.Common;

namespace AFH.Booking.Application.Availability;

public sealed class AvailabilityResponseBuilder : IAvailabilityResponseBuilder
{
    public Result<GetAvailabilityResponse> Success(
        GetAvailabilityQuery query,
        string transactionId,
        IReadOnlyList<AvailabilitySlotResult> slots,
        string? nextCursor)
    {
        var pageSize = query.Limit <= 0 ? 10 : query.Limit;

        var mappedSlots = slots
            .Select(x => (x.Key, x.AdviserId, x.Name, x.GoldStar, x.Slot))
            .ToList();

        var dayGroups = AvailabilityResponseMapping.ToDayGroups(mappedSlots, pageSize);

        return Result<GetAvailabilityResponse>.Ok(new GetAvailabilityResponse
        {
            TransactionId = transactionId,
            Advisers = dayGroups,
            Paging = new PageResultDto<object>
            {
                NextCursor = nextCursor,
                PageSize = pageSize,
                ReturnedCount = dayGroups?.Count ?? 0
            }
        });
    }

    public Result<GetAvailabilityResponse> Empty(string? nextCursor)
    {
        return Result<GetAvailabilityResponse>.Ok(new GetAvailabilityResponse
        {
            Advisers = new(),
            Paging = new PageResultDto<object>
            {
                NextCursor = nextCursor,
                PageSize = 0,
                ReturnedCount = 0
            }
        });
    }
}
