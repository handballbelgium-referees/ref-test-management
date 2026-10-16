using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Handball.Belgium.RefTestManagement.AuditLog;

public class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.HasKey(a => a.SeqId);

        builder.Property(a => a.SeqId)
            .ValueGeneratedOnAdd();

        builder.Property(a => a.Id)
            .IsRequired();

        builder.Property(a => a.StreamId)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(a => a.Version)
            .IsRequired();

        builder.Property(a => a.Type)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(a => a.Data)
            .IsRequired(false);

        builder.Property(a => a.ActorName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(a => a.ActorEmail)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(a => a.Headers)
            .IsRequired(false);

        builder.Property(a => a.IsArchived)
            .IsRequired();

        builder.Property(a => a.RedactedAt)
            .IsRequired(false);

        builder.HasIndex(a => a.StreamId);
        builder.HasIndex(a => a.Type);
        builder.HasIndex(a => a.Timestamp);
        builder.HasIndex(a => new { a.StreamId, a.Version }).IsUnique();
        builder.HasIndex(a => a.IsArchived);

        // The retention sweep pages through rows past the cutoff that have not been redacted yet.
        builder.HasIndex(a => new { a.Timestamp, a.RedactedAt });
    }
}
