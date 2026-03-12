using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Handball.Belgium.RefTestManagement.Permissions.AuditLog;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Action)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(x => x.UserEmail)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.UserName)
            .HasMaxLength(256);

        builder.Property(x => x.ResourceId)
            .HasMaxLength(2000);

        builder.Property(x => x.Details)
            .HasMaxLength(4000);

        builder.Property(x => x.PerformedAt)
            .IsRequired();

        builder.HasIndex(x => x.PerformedAt);
        builder.HasIndex(x => x.UserEmail);
        builder.HasIndex(x => x.Action);
    }
}
