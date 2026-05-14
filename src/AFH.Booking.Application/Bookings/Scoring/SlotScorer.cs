using AFH.Booking.Application.Abstractions.Bookings;
using AFH.Booking.Domain.Bookings.Score;

namespace AFH.Booking.Application.Bookings.Scoring;

public sealed class SlotScorer : ISlotScorer
{
    private readonly ScoreWeights _weights;

    public SlotScorer(ScoreWeights weights) => _weights = weights;

    public SlotScoreResult Score(SlotScoringContext ctx)
    {
        var breakdown = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // base
        breakdown["base"] = _weights.BaseScore;

        // time-of-day boost
        if (ctx.StartUtc.Hour is >= 10 and <= 15)
            breakdown["timeOfDay"] = +1;

        // Monday/Friday penalty
        if (ctx.StartUtc.DayOfWeek is DayOfWeek.Monday or DayOfWeek.Friday)
            breakdown["dayOfWeek"] = -1;

        // travel penalty (only if not remote)
        if (!ctx.IsRemote && ctx.TravelMinutes is not null)
        {
            var t = ctx.TravelMinutes.Value;

            if (t > 60) breakdown["travelMinutes"] = -2;
            else if (t > 30) breakdown["travelMinutes"] = -1;
            else breakdown["travelMinutes"] = 0;
        }

        // preference boost
        if (ctx.AdviserPreferred)
            breakdown["preferred"] = +1;

        var raw = breakdown.Values.Sum();
        var final = Math.Clamp(raw, 1, 5);

     
        if (final != raw)
            breakdown["clamped"] = final - raw;

        return new SlotScoreResult
        {
            Score = final,
            Breakdown = breakdown
        };
    }
}