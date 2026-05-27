using AFH.Booking.Infrastructure.Persistence;
using AFH.Notification.Application.Models;
using AFH.Notification.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AFH.Booking.Tests;

public class NotificationOutboxStoreTests : IAsyncLifetime
{
    private NotificationDbContext _dbContext = default!;
    private NotificationOutboxStore _sut = default!;
    private bool _dbAvailable;
    private readonly DbContextOptions<NotificationDbContext> _options;

    public NotificationOutboxStoreTests()
    {
        _options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseSqlServer("Server=localhost;Database=AFH.Booking.Test;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
    }

    public async Task InitializeAsync()
    {
        try
        {
            _dbContext = new NotificationDbContext(_options);
            await _dbContext.Database.EnsureDeletedAsync();
            await _dbContext.Database.EnsureCreatedAsync();

            // Add schema manually if EnsureCreated doesn't pick up the exact schema we want
            // but EnsureCreated should create the NotificationOutbox table via NotificationDbContext
            _dbAvailable = true;
            _sut = new NotificationOutboxStore(_dbContext, NullLogger<NotificationOutboxStore>.Instance);
        }
        catch (Exception)
        {
            _dbAvailable = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (_dbAvailable)
        {
            await _dbContext.Database.EnsureDeletedAsync();
            await _dbContext.DisposeAsync();
        }
    }

    private bool SkipIfNoDb() => !_dbAvailable;

    [Fact]
    public async Task CreateOrGetAsync_CreatesNewItem_ReturnsCreatedTrue()
    {
        if (SkipIfNoDb()) return;
        var item = new NotificationOutboxItem(
            Guid.NewGuid(),
            "Booking",
            "BookingConfirmed",
            $"Booking:BookingConfirmed:{Guid.NewGuid()}",
            "{}",
            NotificationDispatchStatus.Pending,
            null,
            0,
            null,
            DateTime.UtcNow,
            DateTime.UtcNow,
            null);

        var result = await _sut.CreateOrGetAsync(item, CancellationToken.None);

        Assert.True(result.Created);
        Assert.Equal(item.Id, result.Item.Id);

        var loaded = await _sut.GetAsync(item.Id, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal(NotificationDispatchStatus.Pending, loaded.Status);
    }

    [Fact]
    public async Task CreateOrGetAsync_DuplicateIdempotencyKey_ReturnsExistingItem_CreatedFalse()
    {
        if (SkipIfNoDb()) return;
        var idempotency = $"Booking:Duplicate:{Guid.NewGuid()}";
        var firstId = Guid.NewGuid();
        var item1 = new NotificationOutboxItem(
            firstId, "App", "Type", idempotency, "{}", NotificationDispatchStatus.Pending, null, 0, null, DateTime.UtcNow, DateTime.UtcNow, null);

        await _sut.CreateOrGetAsync(item1, CancellationToken.None);

        var item2 = new NotificationOutboxItem(
            Guid.NewGuid(), "App2", "Type2", idempotency, "{}", NotificationDispatchStatus.Pending, null, 0, null, DateTime.UtcNow, DateTime.UtcNow, null);

        var result = await _sut.CreateOrGetAsync(item2, CancellationToken.None);

        Assert.False(result.Created);
        Assert.Equal(firstId, result.Item.Id);
        Assert.Equal("App", result.Item.SourceApplication);
    }

    [Fact]
    public async Task TryMarkProcessingAsync_ClaimsValidStatus_IncrementsAttemptCount()
    {
        if (SkipIfNoDb()) return;
        var item = new NotificationOutboxItem(Guid.NewGuid(), "A", "T", Guid.NewGuid().ToString(), "{}", NotificationDispatchStatus.Pending, null, 0, null, DateTime.UtcNow, DateTime.UtcNow, null);
        await _sut.CreateOrGetAsync(item, CancellationToken.None);

        var claimed = await _sut.TryMarkProcessingAsync(item.Id, CancellationToken.None);
        Assert.True(claimed);

        var loaded = await _sut.GetAsync(item.Id, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal(NotificationDispatchStatus.Processing, loaded.Status);
        Assert.Equal(1, loaded.AttemptCount);
    }

    [Fact]
    public async Task TryMarkProcessingAsync_AlreadyProcessingOrSent_ReturnsFalse()
    {
        if (SkipIfNoDb()) return;
        var item = new NotificationOutboxItem(Guid.NewGuid(), "A", "T", Guid.NewGuid().ToString(), "{}", NotificationDispatchStatus.Pending, null, 0, null, DateTime.UtcNow, DateTime.UtcNow, null);
        await _sut.CreateOrGetAsync(item, CancellationToken.None);

        // Claim it
        await _sut.TryMarkProcessingAsync(item.Id, CancellationToken.None);

        // Try to claim again
        var claimedAgain = await _sut.TryMarkProcessingAsync(item.Id, CancellationToken.None);
        Assert.False(claimedAgain);
    }

    [Fact]
    public async Task MarkSentAsync_SetsProcessedUtc_AndStatus()
    {
        if (SkipIfNoDb()) return;
        var item = new NotificationOutboxItem(Guid.NewGuid(), "A", "T", Guid.NewGuid().ToString(), "{}", NotificationDispatchStatus.Pending, null, 0, null, DateTime.UtcNow, DateTime.UtcNow, null);
        await _sut.CreateOrGetAsync(item, CancellationToken.None);

        await _sut.MarkSentAsync(item.Id, CancellationToken.None);

        var loaded = await _sut.GetAsync(item.Id, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal(NotificationDispatchStatus.Sent, loaded.Status);
        Assert.NotNull(loaded.ProcessedUtc);
    }

    [Fact]
    public async Task MarkFailedAsync_RecordsLastError()
    {
        if (SkipIfNoDb()) return;
        var item = new NotificationOutboxItem(Guid.NewGuid(), "A", "T", Guid.NewGuid().ToString(), "{}", NotificationDispatchStatus.Pending, null, 0, null, DateTime.UtcNow, DateTime.UtcNow, null);
        await _sut.CreateOrGetAsync(item, CancellationToken.None);

        await _sut.MarkFailedAsync(item.Id, "error-details", CancellationToken.None);

        var loaded = await _sut.GetAsync(item.Id, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal(NotificationDispatchStatus.Failed, loaded.Status);
        Assert.Equal("error-details", loaded.LastError);
    }

    [Fact]
    public async Task MarkDeadLetteredAsync_SetsProcessedUtc_RecordsLastError()
    {
        if (SkipIfNoDb()) return;
        var item = new NotificationOutboxItem(Guid.NewGuid(), "A", "T", Guid.NewGuid().ToString(), "{}", NotificationDispatchStatus.Pending, null, 0, null, DateTime.UtcNow, DateTime.UtcNow, null);
        await _sut.CreateOrGetAsync(item, CancellationToken.None);

        await _sut.MarkDeadLetteredAsync(item.Id, "dead-error", CancellationToken.None);

        var loaded = await _sut.GetAsync(item.Id, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal(NotificationDispatchStatus.DeadLettered, loaded.Status);
        Assert.Equal("dead-error", loaded.LastError);
        Assert.NotNull(loaded.ProcessedUtc);
    }
}
