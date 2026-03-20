using AFH.Booking.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AFH.Booking.Infrastructure.Persistence.Configurations;

public sealed class DuplicateClientCaseModelConfiguration : IEntityTypeConfiguration<DuplicateClientCaseModel>
{
    public void Configure(EntityTypeBuilder<DuplicateClientCaseModel> b)
    {
        b.ToTable("DuplicateClientCases");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasMaxLength(64).IsRequired();
        b.Property(x => x.PrimaryTransactionRef).HasMaxLength(256).IsRequired();
        b.Property(x => x.DuplicateTransactionRef).HasMaxLength(256).IsRequired();
        b.Property(x => x.Status).HasMaxLength(32).IsRequired();
        b.Property(x => x.Notes).HasMaxLength(2048);
        b.Property(x => x.RaisedBy).HasMaxLength(128);
        b.Property(x => x.Resolution).HasMaxLength(512);
        b.Property(x => x.ResolvedBy).HasMaxLength(128);

        b.HasIndex(x => new { x.Status, x.RaisedUtc });
    }
}
