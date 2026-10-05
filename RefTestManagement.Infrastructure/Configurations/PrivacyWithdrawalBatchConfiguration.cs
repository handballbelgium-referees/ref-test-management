using Handball.Belgium.RefTestManagement.Domain.Privacy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Configurations;

public sealed class PrivacyWithdrawalBatchConfiguration : IEntityTypeConfiguration<PrivacyWithdrawalBatch>
{
    public void Configure(EntityTypeBuilder<PrivacyWithdrawalBatch> builder)
    {
        builder.ToTable("PrivacyWithdrawalBatches");
        builder.HasKey(batch => batch.Id);

        builder.Property(batch => batch.Version)
            .IsConcurrencyToken();

        builder.Property(batch => batch.CreatedAt)
            .IsRequired();

        builder.Property(batch => batch.TargetCount)
            .IsRequired();

        builder.Property(batch => batch.LatestJobId)
            .IsRequired(false);

        builder.Property(batch => batch.CompletedAt)
            .IsRequired(false);

        builder.HasIndex(batch => batch.CompletedAt)
            .HasDatabaseName("IX_PrivacyWithdrawalBatches_CompletedAt");
    }
}
