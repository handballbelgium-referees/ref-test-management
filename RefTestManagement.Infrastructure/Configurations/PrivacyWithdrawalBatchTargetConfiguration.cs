using Handball.Belgium.RefTestManagement.Domain.Privacy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Configurations;

public sealed class PrivacyWithdrawalBatchTargetConfiguration : IEntityTypeConfiguration<PrivacyWithdrawalBatchTarget>
{
    public void Configure(EntityTypeBuilder<PrivacyWithdrawalBatchTarget> builder)
    {
        builder.ToTable("PrivacyWithdrawalBatchTargets");
        builder.HasKey(target => target.Id);

        builder.Property(target => target.Version)
            .IsConcurrencyToken();

        builder.Property(target => target.BatchId)
            .IsRequired();

        builder.Property(target => target.RefTestId)
            .IsRequired();

        builder.Property(target => target.ErasureStartedAt)
            .IsRequired(false);

        builder.Property(target => target.AttemptCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(target => target.NextAttemptAt)
            .IsRequired(false);

        builder.Property(target => target.RetryExhaustedAt)
            .IsRequired(false);

        builder.Property(target => target.FailureCode)
            .IsRequired(false);

        builder.Property(target => target.CompletedAt)
            .IsRequired(false);

        builder.HasOne<PrivacyWithdrawalBatch>()
            .WithMany()
            .HasForeignKey(target => target.BatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(target => new { target.BatchId, target.RefTestId })
            .IsUnique()
            .HasDatabaseName("IX_PrivacyWithdrawalBatchTargets_BatchId_RefTestId");

        builder.HasIndex(target => new { target.BatchId, target.CompletedAt })
            .HasDatabaseName("IX_PrivacyWithdrawalBatchTargets_BatchId_CompletedAt");
    }
}
