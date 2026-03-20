using AFH.Booking.Contracts.V1.Dtos;
using AFH.Booking.Contracts.V1.Dtos.Availability;
using AFH.Booking.Contracts.V1.Responses;
using AFH.Booking.Domain.Calendar;
using AFH.Booking.Domain.Transactions;

namespace AFH.Booking.Application.Bookings.Mappings;

public static class AvailabilityResponseMapping
{
    public static List<AdviserSlotsDto> ToDayGroups(
IEnumerable<(string AdviserKey, string AdviserEmail, string AdviserName, bool GoldStar, BookingSlot Slot)> rows,
int adviserLimit = 10)
    {
        return rows
            .GroupBy(r => r.AdviserKey, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key)
            .Select(adviserGroup =>
            {
                var adviserSlots = new AdviserSlotsDto
                {
                    Id = adviserGroup.FirstOrDefault().AdviserEmail,
                    Name = adviserGroup.FirstOrDefault().AdviserName,
                    GoldStar = adviserGroup.FirstOrDefault().GoldStar,

                    Slots = adviserGroup
                        .Select(x => new SlotDto
                        {
                            SlotId = x.Slot.Id,
                            StartUtc = x.Slot.StartUtc,
                            EndUtc = x.Slot.EndUtc,
                            Rating = x.Slot.Score,
                            ScoreBreakdown = x.Slot.ScoreBreakdown,
                            TravelMinutes = x.Slot.TravelMinutes,
                            CompanyBufferMinutes = x.Slot.CompanyBufferMinutes,
                            DistanceMiles = x.Slot.DistanceMiles,
                            TravelStatus = x.Slot.TravelStatus,
                            TravelMessage = x.Slot.TravelMessage
                        })
                        .OrderBy(s => s.StartUtc)
                        .ToList()
                };

                return adviserSlots;
            })
            .ToList();
    }



    public static List<AvailabilityDayGroupDto> ToDayGroups(
       IEnumerable<(DateOnly DateUtc, string AdviserId, string AdviserName, SlotDto Slot)> rows,
       int adviserLimit = 10)
    {
        return rows
            .GroupBy(r => r.DateUtc)
            .OrderBy(g => g.Key)
            .Select(dayGroup =>
            {
                var advisers = dayGroup
                     .GroupBy(x => x.AdviserName + x.AdviserId, StringComparer.OrdinalIgnoreCase)

                    .Select(adviserGroup =>
                    {
                        var first = adviserGroup.First();

                        return new AvailabilityAdviserDto
                        {
                            AdviserId = adviserGroup.Key,
                            AdviserName = first.AdviserName, // snapshot for display only
                            Slots = adviserGroup
                                .Select(x => x.Slot)
                                .OrderBy(s => s.StartUtc)
                                .ToList()
                        };
                    })
                    .OrderBy(a => a.AdviserName)
                    .Take(adviserLimit)
                    .ToList();

                return new AvailabilityDayGroupDto
                {
                    DateUtc = dayGroup.Key,
                    Advisers = advisers,
                    TotalAdvisers = advisers.Count,
                    TotalSlots = advisers.Sum(a => a.Slots.Count)
                };
            })
            .ToList();
    }
}