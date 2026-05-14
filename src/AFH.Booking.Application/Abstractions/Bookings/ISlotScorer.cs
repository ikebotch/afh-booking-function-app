using AFH.Booking.Domain.Bookings.Score;

namespace AFH.Booking.Application.Abstractions.Bookings;

public interface ISlotScorer
{
    SlotScoreResult Score(SlotScoringContext ctx);
}
