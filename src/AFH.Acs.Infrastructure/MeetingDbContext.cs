using AFH.Acs.Recorder.Infrastructure.Data.Entities;
using AFH.Acs.Recorder.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;

namespace AFH.Acs.Recorder.Infrastructure;

public class MeetingDbContext : DbContext
{
    public MeetingDbContext(DbContextOptions<MeetingDbContext> options)
        : base(options)
    {
    }
    public DbSet<ApplicationLogsEntity> ApplicationLogs => Set<ApplicationLogsEntity>();
    public DbSet<AdviserEntity> Advisers => Set<AdviserEntity>();
    public DbSet<LeadEntity> Leads => Set<LeadEntity>();
    public DbSet<MeetingEntity> Meetings => Set<MeetingEntity>();
    public DbSet<MeetingAttendeeEntity> MeetingAttendees => Set<MeetingAttendeeEntity>();
    public DbSet<MeetingRecordingEntity> MeetingRecordings => Set<MeetingRecordingEntity>();
    public DbSet<MeetingTranscriptionEntity> MeetingTranscriptions => Set<MeetingTranscriptionEntity>();
    public DbSet<MeetingNoteEntity> MeetingNotes => Set<MeetingNoteEntity>();
    public DbSet<ChecklistTemplateEntity> ChecklistTemplates => Set<ChecklistTemplateEntity>();
    public DbSet<ChecklistItemTemplateEntity> ChecklistItemTemplates => Set<ChecklistItemTemplateEntity>();
    public DbSet<MeetingChecklistItemEntity> MeetingChecklistItems => Set<MeetingChecklistItemEntity>();
    public DbSet<AtrTemplateEntity> AtrTemplates => Set<AtrTemplateEntity>();
    public DbSet<MeetingAtrAnalysisEntity> MeetingAtrAnalyses => Set<MeetingAtrAnalysisEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);


        base.OnModelCreating(modelBuilder);

        // Automatically map PascalCase entities/properties to snake_case in DB
        modelBuilder.UseUpperSnakeCase();




        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationLogsEntity>(b =>
        {
            b.HasKey(x => x.LogId);
            b.Property(x => x.TimestampUtc)
             .HasDefaultValueSql("SYSUTCDATETIME()");

            b.HasIndex(x => x.CorrelationId);
            b.HasIndex(x => x.TimestampUtc);
        });


        // ----------------- ADVISER -----------------
        modelBuilder.Entity<AdviserEntity>()
                .HasKey(x => x.AdviserId);

        modelBuilder.Entity<AdviserEntity>()
            .HasMany(a => a.Meetings)
            .WithOne(m => m.Adviser)
            .HasForeignKey(m => m.AdviserId);

        // ----------------- LEAD -----------------
        modelBuilder.Entity<LeadEntity>()
            .HasKey(x => x.LeadId);

        modelBuilder.Entity<LeadEntity>()
            .HasMany(l => l.Meetings)
            .WithOne(m => m.Lead)
            .HasForeignKey(m => m.LeadId);

        // ----------------- MEETING -----------------
        modelBuilder.Entity<MeetingEntity>()
            .HasKey(x => x.MeetingId);

        // ----------------- MEETING_ATTENDEE -----------------
        modelBuilder.Entity<MeetingAttendeeEntity>()
            .HasKey(x => new { x.MeetingId, x.Email });

        modelBuilder.Entity<MeetingAttendeeEntity>()
            .HasOne(x => x.Meeting)
            .WithMany(m => m.Attendees)
            .HasForeignKey(x => x.MeetingId);

        // ----------------- MEETING_RECORDING -----------------
        modelBuilder.Entity<MeetingRecordingEntity>()
            .HasKey(x => x.RecordingId);

        modelBuilder.Entity<MeetingRecordingEntity>()
            .HasOne(x => x.Meeting)
            .WithMany(m => m.Recordings)
            .HasForeignKey(x => x.MeetingId);

        // ----------------- MEETING_TRANSCRIPTION -----------------
        modelBuilder.Entity<MeetingTranscriptionEntity>()
            .HasKey(x => x.TranscriptionId);

        modelBuilder.Entity<MeetingTranscriptionEntity>()
            .HasOne(x => x.Meeting)
            .WithOne(m => m.Transcription)
            .HasForeignKey<MeetingTranscriptionEntity>(x => x.MeetingId)
         .OnDelete(DeleteBehavior.Restrict);

        // ----------------- MEETING_NOTE -----------------
        modelBuilder.Entity<MeetingNoteEntity>()
            .HasKey(x => x.NoteId);

        modelBuilder.Entity<MeetingNoteEntity>()
            .HasOne(x => x.Meeting)
    .WithMany()
    .HasForeignKey(x => x.MeetingId)
    .OnDelete(DeleteBehavior.Restrict);  // or DeleteBehavior.NoAction


        // ----------------- CHECKLIST_TEMPLATE -----------------
        modelBuilder.Entity<ChecklistTemplateEntity>()
            .HasKey(x => x.TemplateId);

        modelBuilder.Entity<ChecklistTemplateEntity>()
            .HasMany(t => t.Items)
            .WithOne(i => i.Template)
            .HasForeignKey(i => i.TemplateId);

        // ----------------- CHECKLIST_ITEM_TEMPLATE -----------------
        modelBuilder.Entity<ChecklistItemTemplateEntity>()
            .HasKey(x => new { x.TemplateId, x.ItemId });

        // ----------------- MEETING_CHECKLIST_ITEM -----------------
        modelBuilder.Entity<MeetingChecklistItemEntity>()
            .HasKey(x => new { x.MeetingId, x.ItemId });

        modelBuilder.Entity<MeetingChecklistItemEntity>()
            .HasOne(x => x.Meeting)
            .WithMany()
            .HasForeignKey(x => x.MeetingId);

        // ----------------- ATR_TEMPLATE -----------------
        modelBuilder.Entity<AtrTemplateEntity>()
            .HasKey(x => x.AtrId);

        // ----------------- MEETING_ATR_ANALYSIS (Keyless) -----------------
        modelBuilder.Entity<MeetingAtrAnalysisEntity>().HasKey(x => new { x.MeetingId });

        modelBuilder.Entity<MeetingAtrAnalysisEntity>()
            .HasOne(x => x.Meeting)
            .WithOne(m => m.AtrAnalysis)
            .HasForeignKey<MeetingAtrAnalysisEntity>(x => x.MeetingId);
    }
}
