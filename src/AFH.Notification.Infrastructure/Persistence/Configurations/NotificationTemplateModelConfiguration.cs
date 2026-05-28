using AFH.Notification.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AFH.Notification.Infrastructure.Persistence.Configurations;

public sealed class NotificationTemplateModelConfiguration : IEntityTypeConfiguration<NotificationTemplateModel>
{
    private static readonly DateTime SeededUtc = new(2026, 5, 28, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<NotificationTemplateModel> b)
    {
        b.ToTable("NotificationTemplates", "dbo");
        b.HasKey(x => x.Id);

        b.Property(x => x.TemplateKey).HasMaxLength(150).IsRequired();
        b.Property(x => x.TemplateVersion).HasMaxLength(50).IsRequired();
        b.Property(x => x.Channel).HasMaxLength(50).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.SubjectTemplate).HasMaxLength(500);
        b.Property(x => x.BodyTemplate).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(50).IsRequired();
        b.Property(x => x.CreatedBy).HasMaxLength(150);
        b.Property(x => x.UpdatedBy).HasMaxLength(150);

        b.HasIndex(x => new { x.TemplateKey, x.TemplateVersion, x.Channel }).IsUnique();

        b.HasData(
            Template(
                "10000000-0000-0000-0000-000000000001",
                "booking-confirmed",
                "Booking confirmed",
                "AFH Booking: Booking Confirmed",
                """
                <p>Hello,</p>

                <p>Your booking is now confirmed.</p>

                <p>
                Transaction reference: {{transactionRef}}<br>
                Booking ID: {{bookingId}}<br>
                Adviser: {{adviserName}}<br>
                Meeting type: {{meetingType}}<br>
                When: {{when}}<br>
                {{whereLine}}
                </p>

                <p>{{travelLine}}</p>

                <p>Manage your booking:</p>
                <ul>
                <li><a href="{{viewBookingUrl}}">View booking</a></li>
                <li><a href="{{cancelBookingUrl}}">Cancel booking</a></li>
                <li><a href="{{rescheduleBookingUrl}}">Reschedule booking</a></li>
                </ul>

                <p>This is an automated AFH booking notification.</p>
                """,
                "text/html"),
            Template(
                "10000000-0000-0000-0000-000000000002",
                "booking-rescheduled",
                "Booking rescheduled",
                "AFH Booking: Appointment Rescheduled",
                """
                Hello {{greetingName}},

                Your booking has been updated: Appointment Rescheduled.
                When: {{whenLine}}
                Adviser: {{adviserName}}
                Meeting type: {{locationLine}}

                {{note}}
                {{manageBookingLinks}}

                This is an automated AFH booking notification.
                """),
            Template(
                "10000000-0000-0000-0000-000000000003",
                "booking-cancelled",
                "Booking cancelled",
                "AFH Booking: Appointment Cancelled",
                """
                Hello {{greetingName}},

                Your booking has been updated: Appointment Cancelled.
                When: {{whenLine}}
                Adviser: {{adviserName}}
                Meeting type: {{locationLine}}

                {{note}}
                {{manageBookingLinks}}

                This is an automated AFH booking notification.
                """),
            Template(
                "10000000-0000-0000-0000-000000000004",
                "booking-hold",
                "Booking hold created",
                "AFH Booking: Hold Created",
                """
                Hello,

                We have placed a temporary hold on your requested booking while it is being confirmed.

                Transaction reference: {{transactionRef}}
                Hold ID: {{holdId}}
                Adviser: {{adviserName}}
                Meeting type: {{meetingType}}
                When: {{when}}
                Hold expires: {{holdExpires}}

                {{travelLine}}
                {{companyLine}}
                {{manageBookingLinks}}

                This is an automated AFH booking notification.
                """));
    }

    private static NotificationTemplateModel Template(
        string id,
        string templateKey,
        string name,
        string subject,
        string body,
        string contentType = "text/plain")
        => new()
        {
            Id = Guid.Parse(id),
            TemplateKey = templateKey,
            TemplateVersion = "v1",
            Channel = "Email",
            Name = name,
            Description = null,
            SubjectTemplate = subject,
            BodyTemplate = body,
            ContentType = contentType,
            IsActive = true,
            CreatedBy = "System",
            UpdatedBy = "System",
            CreatedUtc = SeededUtc,
            UpdatedUtc = SeededUtc
        };
}
