using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Handball.Belgium.RefTestManagement.AuditLog;

public class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.EntityType)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(a => a.EntityId)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(a => a.Action)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(a => a.Changes)
            .IsRequired(false);

        builder.Property(a => a.ActorName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(a => a.ActorEmail)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(a => a.Timestamp)
            .IsRequired();

        builder.HasIndex(a => new { a.EntityType, a.EntityId });

        builder.HasIndex(a => a.Timestamp);

        builder.HasIndex(a => a.ActorName);
    }
}
