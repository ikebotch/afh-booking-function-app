using AFH.Booking.Domain.Transactions;
using AFH.Common.Errors.EntityFramework.Persistence;
using AFH.Booking.Infrastructure.Persistence.Configurations;
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
    public DbSet<ApprovalHistoryModel> ApprovalHistory => Set<ApprovalHistoryModel>();
    public DbSet<NotificationDispatchModel> NotificationDispatches => Set<NotificationDispatchModel>();
    public DbSet<LifecycleEventModel> LifecycleEvents => Set<LifecycleEventModel>();
    public DbSet<LifecycleStepModel> LifecycleSteps => Set<LifecycleStepModel>();
    public DbSet<EmailBounceEventModel> EmailBounceEvents => Set<EmailBounceEventModel>();
    public DbSet<DuplicateClientCaseModel> DuplicateClientCases => Set<DuplicateClientCaseModel>();
    public DbSet<DownstreamUpdateModel> DownstreamUpdates => Set<DownstreamUpdateModel>();
    public DbSet<OperationalIssueModel> OperationalIssues => Set<OperationalIssueModel>();
    public DbSet<AdviserProfileProjectionModel> AdviserProfileProjections => Set<AdviserProfileProjectionModel>();
    public DbSet<MeetingTopicModel> MeetingTopics => Set<MeetingTopicModel>();
    public DbSet<IntegrationSyncStateModel> IntegrationSyncStates => Set<IntegrationSyncStateModel>();
    public DbSet<IntegrationOperationAuditModel> IntegrationOperationAudits => Set<IntegrationOperationAuditModel>();
    public DbSet<ApplicationLogModel> ApplicationLogs => Set<ApplicationLogModel>();

    public override int SaveChanges()
    {
        EnforceImmutableAuditRecords();
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnforceImmutableAuditRecords();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        EnforceImmutableAuditRecords();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnforceImmutableAuditRecords();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BookingDbContext).Assembly);
        modelBuilder.ApplyConfiguration(new AdviserProfileProjectionModelConfiguration());
        modelBuilder.AddErrorRecordEntity();

        modelBuilder.Entity<ApplicationLogModel>(entity =>
        {
            entity.ToTable("ApplicationLogs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Level).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Operation).HasMaxLength(256).IsRequired();
            entity.Property(x => x.CorrelationId).HasMaxLength(128);
            entity.Property(x => x.UserId).HasMaxLength(128);
            entity.Property(x => x.ContextId).HasMaxLength(256);
            entity.Property(x => x.EventType).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Result).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(2048).IsRequired();
            entity.Property(x => x.ExceptionType).HasMaxLength(256);
            entity.Property(x => x.ExceptionMessage).HasMaxLength(2048);
            entity.Property(x => x.PayloadJson).HasMaxLength(4096);
            entity.HasIndex(x => x.OccurredUtc);
            entity.HasIndex(x => x.CorrelationId);
            entity.HasIndex(x => new { x.Category, x.OccurredUtc });
            entity.HasIndex(x => new { x.Operation, x.OccurredUtc });
        });

        base.OnModelCreating(modelBuilder);
    }

    private void EnforceImmutableAuditRecords()
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Modified or EntityState.Deleted))
                continue;

            if (entry.Entity is LifecycleEventModel or LifecycleStepModel or ApprovalHistoryModel)
            {
                throw new InvalidOperationException(
                    $"Audit records are immutable and cannot be {entry.State.ToString().ToLowerInvariant()}.");
            }
        }
    }
}
