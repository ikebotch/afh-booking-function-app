using AFH.Booking.Application.Models.AdviserProjection;
using AFH.Booking.Infrastructure.Persistence;
using AFH.Booking.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AFH.Booking.Tests;

public sealed class AdviserProfileProjectionRepositoryTests
{
    [Fact]
    public async Task UpsertRangeAsync_PopulatesAndReconcilesNormalizedAdviserSkills()
    {
        await using var db = CreateDbContext();
        var repository = new AdviserProfileProjectionRepository(db);
        var firstSync = new DateTime(2026, 07, 16, 9, 0, 0, DateTimeKind.Utc);
        var secondSync = firstSync.AddHours(1);

        await repository.UpsertRangeAsync(
        [
            new AdviserProfileProjectionRecord
            {
                AdviserId = "adv-1",
                DisplayName = "Adviser One",
                IsActive = true,
                Skills = ["Protection", " Equity   Release "],
                LastSyncedUtc = firstSync,
                SourceVersion = "sync-1"
            }
        ], CancellationToken.None);
        await db.SaveChangesAsync();

        var firstRows = await db.AdviserSkillProjections
            .Where(x => x.AdviserId == "adv-1")
            .OrderBy(x => x.SkillCode)
            .ToListAsync();

        Assert.Equal(["Equity Release", "Protection"], firstRows.Select(x => x.SkillCode).ToArray());
        Assert.All(firstRows, row => Assert.True(row.IsActive));

        await repository.UpsertRangeAsync(
        [
            new AdviserProfileProjectionRecord
            {
                AdviserId = "adv-1",
                DisplayName = "Adviser One",
                IsActive = true,
                Skills = ["Protection", "Pensions"],
                LastSyncedUtc = secondSync,
                SourceVersion = "sync-2"
            }
        ], CancellationToken.None);
        await db.SaveChangesAsync();

        var secondRows = await db.AdviserSkillProjections
            .Where(x => x.AdviserId == "adv-1")
            .OrderBy(x => x.SkillCode)
            .ToListAsync();

        Assert.Equal(["Equity Release", "Pensions", "Protection"], secondRows.Select(x => x.SkillCode).ToArray());
        Assert.False(secondRows.Single(x => x.SkillCode == "Equity Release").IsActive);
        Assert.True(secondRows.Single(x => x.SkillCode == "Pensions").IsActive);
        Assert.True(secondRows.Single(x => x.SkillCode == "Protection").IsActive);
        Assert.Equal(secondSync, secondRows.Single(x => x.SkillCode == "Equity Release").UpdatedUtc);
    }

    private static BookingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new BookingDbContext(options);
    }
}
