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
                row = new AdviserProfileProjectionModel
                {
                    AdviserId = adviser.AdviserId
                };
                await _db.AdviserProfileProjections.AddAsync(row, ct);
            }

            row.DisplayName = adviser.DisplayName;
            row.MailboxUserId = adviser.MailboxUserId;
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

    public async Task<IReadOnlyList<AdviserProfileProjectionRecord>> ListAsync(DateTime? sinceUtc, int take, CancellationToken ct)
    {
        take = Math.Clamp(take, 1, 500);

        IQueryable<AdviserProfileProjectionModel> query = _db.AdviserProfileProjections.AsNoTracking();
        if (sinceUtc.HasValue)
            query = query.Where(x => x.LastSyncedUtc > sinceUtc.Value);

        var rows = await query
            .OrderBy(x => x.LastSyncedUtc)
            .Take(take)
            .ToListAsync(ct);

        return rows.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<AdviserProfileProjectionRecord>> ListActiveAsync(CancellationToken ct)
    {
        var rows = await _db.AdviserProfileProjections
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.DisplayName)
            .ThenBy(x => x.AdviserId)
            .ToListAsync(ct);

        return rows.Select(Map).ToList();
    }

    public async Task<AdviserProfileProjectionRecord?> GetAsync(string adviserId, CancellationToken ct)
    {
        var row = await _db.AdviserProfileProjections
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.AdviserId == adviserId, ct);

        return row is null ? null : Map(row);
    }

    private static AdviserProfileProjectionRecord Map(AdviserProfileProjectionModel row)
        => new()
        {
            AdviserId = row.AdviserId,
            DisplayName = row.DisplayName,
            MailboxUserId = row.MailboxUserId,
            Region = row.Region,
            HomePostcode = row.HomePostcode,
            IsActive = row.IsActive,
            Rating = row.Rating,
            Skills = DeserializeSkills(row.SkillsJson).ToList(),
            CoverageRadiusMiles = row.CoverageRadiusMiles,
            MaxTravelTimeMinutes = row.MaxTravelTimeMinutes,
            LastSyncedUtc = row.LastSyncedUtc,
            SourceVersion = row.SourceVersion
        };

    private static IReadOnlyList<string> DeserializeSkills(string skillsJson)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(skillsJson) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
