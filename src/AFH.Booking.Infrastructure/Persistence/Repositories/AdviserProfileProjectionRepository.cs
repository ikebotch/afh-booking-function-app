using AFH.Booking.Application.Abstractions.Persistence;
using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AFH.Booking.Infrastructure.Persistence.Repositories;

public sealed class AdviserProfileProjectionRepository : IAdviserProfileProjectionRepository
{
    private readonly BookingDbContext _db;

    public AdviserProfileProjectionRepository(BookingDbContext db)
    {
        _db = db;
    }

    public async Task UpsertRangeAsync(IReadOnlyList<AdviserProfileProjectionRecord> advisers, CancellationToken ct)
    {
        if (advisers.Count == 0)
            return;

        var ids = advisers.Select(x => x.AdviserId).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var existing = await _db.AdviserProfileProjections
            .Where(x => ids.Contains(x.AdviserId))
            .ToDictionaryAsync(x => x.AdviserId, StringComparer.OrdinalIgnoreCase, ct);

        foreach (var adviser in advisers)
        {
            if (!existing.TryGetValue(adviser.AdviserId, out var row))
            {
                await _db.AdviserProfileProjections.AddAsync(new AdviserProfileProjectionModel
                {
                    AdviserId = adviser.AdviserId,
                    DisplayName = adviser.DisplayName,
                    Region = adviser.Region,
                    HomePostcode = adviser.HomePostcode,
                    IsActive = adviser.IsActive,
                    Rating = adviser.Rating,
                    SkillsJson = JsonSerializer.Serialize(adviser.Skills),
                    CoverageRadiusMiles = adviser.CoverageRadiusMiles,
                    MaxTravelTimeMinutes = adviser.MaxTravelTimeMinutes,
                    LastSyncedUtc = adviser.LastSyncedUtc,
                    SourceVersion = adviser.SourceVersion
                }, ct);
                continue;
            }

            row.DisplayName = adviser.DisplayName;
            row.Region = adviser.Region;
            row.HomePostcode = adviser.HomePostcode;
            row.IsActive = adviser.IsActive;
            row.Rating = adviser.Rating;
            row.SkillsJson = JsonSerializer.Serialize(adviser.Skills);
            row.CoverageRadiusMiles = adviser.CoverageRadiusMiles;
            row.MaxTravelTimeMinutes = adviser.MaxTravelTimeMinutes;
            row.LastSyncedUtc = adviser.LastSyncedUtc;
            row.SourceVersion = adviser.SourceVersion;
        }
    }
}
