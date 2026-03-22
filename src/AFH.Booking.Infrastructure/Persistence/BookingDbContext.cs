using AFH.Booking.Domain.Transactions;
using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace AFH.Booking.Infrastructure.Persistence;

public sealed class BookingDbContext : DbContext
{
    public BookingDbContext(DbContextOptions<BookingDbContext> options) : base(options) { }

    public DbSet<BookingTransactionModel> BookingTransactions => Set<BookingTransactionModel>();
    public DbSet<BookingSlotModel> BookingSlots => Set<BookingSlotModel>();
    public DbSet<BookingHoldModel> Holds => Set<BookingHoldModel>();
    public DbSet<ApprovalRequestModel> ApprovalRequests => Set<ApprovalRequestModel>();
    public DbSet<NotificationDispatchModel> NotificationDispatches => Set<NotificationDispatchModel>();
    public DbSet<EmailBounceEventModel> EmailBounceEvents => Set<EmailBounceEventModel>();
    public DbSet<DuplicateClientCaseModel> DuplicateClientCases => Set<DuplicateClientCaseModel>();
    public DbSet<DownstreamUpdateModel> DownstreamUpdates => Set<DownstreamUpdateModel>();


    public DbSet<CalendarSubscriptionModel> CalendarSubscriptions => Set<CalendarSubscriptionModel>();
    public DbSet<CalendarNotificationReceiptModel> CalendarNotificationReceipts => Set<CalendarNotificationReceiptModel>();
    public DbSet<CalendarEventSnapshotModel> CalendarEventSnapshots => Set<CalendarEventSnapshotModel>();
    public DbSet<AdviserAvailabilityBlockModel> AdviserAvailabilityBlocks => Set<AdviserAvailabilityBlockModel>();
    public DbSet<AdviserProfileProjectionModel> AdviserProfileProjections => Set<AdviserProfileProjectionModel>();
    public DbSet<IntegrationSyncStateModel> IntegrationSyncStates => Set<IntegrationSyncStateModel>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BookingDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
