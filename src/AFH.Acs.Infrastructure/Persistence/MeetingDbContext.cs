using AFH.Acs.Infrastructure.Extensions;
using AFH.Acs.Infrastructure.Persistence.Entities;
using AFH.Common.Errors.EntityFramework.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AFH.Acs.Infrastructure.Persistence;

public sealed class MeetingDbContext(DbContextOptions<MeetingDbContext> options) : DbContext(options)
{
    public DbSet<AdviserEntity> Advisers => Set<AdviserEntity>();
    public DbSet<LeadEntity> Leads => Set<LeadEntity>();
    public DbSet<MeetingEntity> Meetings => Set<MeetingEntity>();
    public DbSet<MeetingAttendeeEntity> MeetingAttendees => Set<MeetingAttendeeEntity>();
    public DbSet<MeetingRecordingEntity> MeetingRecordings => Set<MeetingRecordingEntity>();
    public DbSet<MeetingTranscriptionEntity> MeetingTranscriptions => Set<MeetingTranscriptionEntity>();
    public DbSet<ApplicationLogEntity> ApplicationLogs => Set<ApplicationLogEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.AddErrorRecordEntity();
        modelBuilder.UseUpperSnakeCase();

        modelBuilder.Entity<AdviserEntity>().HasKey(x => x.AdviserId);
        modelBuilder.Entity<LeadEntity>().HasKey(x => x.LeadId);
        modelBuilder.Entity<MeetingEntity>().HasKey(x => x.MeetingId);

        modelBuilder.Entity<MeetingEntity>()
            .HasMany(x => x.Attendees)
            .WithOne(x => x.Meeting)
            .HasForeignKey(x => x.MeetingId);

        modelBuilder.Entity<MeetingEntity>()
            .HasMany(x => x.Recordings)
            .WithOne(x => x.Meeting)
            .HasForeignKey(x => x.MeetingId);

        modelBuilder.Entity<MeetingEntity>()
            .HasOne(x => x.Transcription)
            .WithOne(x => x.Meeting)
            .HasForeignKey<MeetingTranscriptionEntity>(x => x.MeetingId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MeetingEntity>()
            .HasOne(x => x.Adviser)
            .WithMany(x => x.Meetings)
            .HasForeignKey(x => x.AdviserId);

        modelBuilder.Entity<MeetingEntity>()
            .HasOne(x => x.Lead)
            .WithMany(x => x.Meetings)
            .HasForeignKey(x => x.LeadId);

        modelBuilder.Entity<MeetingAttendeeEntity>().HasKey(x => new { x.MeetingId, x.Email });
        modelBuilder.Entity<MeetingRecordingEntity>().HasKey(x => x.RecordingId);
        modelBuilder.Entity<MeetingTranscriptionEntity>().HasKey(x => x.TranscriptionId);
        modelBuilder.Entity<ApplicationLogEntity>(entity =>
        {
            entity.ToTable("APPLICATION_LOGS");
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
    }
}
