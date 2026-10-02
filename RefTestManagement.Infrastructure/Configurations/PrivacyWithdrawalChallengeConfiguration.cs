using Handball.Belgium.RefTestManagement.Domain.Privacy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Configurations;

public sealed class PrivacyWithdrawalChallengeConfiguration : IEntityTypeConfiguration<PrivacyWithdrawalChallenge>
{
    public void Configure(EntityTypeBuilder<PrivacyWithdrawalChallenge> builder)
    {
        builder.ToTable("PrivacyWithdrawalChallenges");
        builder.HasKey(challenge => challenge.Id);

        builder.Property(challenge => challenge.Version)
            .IsConcurrencyToken();

        builder.Property(challenge => challenge.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(challenge => challenge.NormalizedEmailHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(challenge => challenge.KeyHash)
            .IsRequired(false)
            .HasMaxLength(64);

        builder.Property(challenge => challenge.ProtectedDeliveryKey)
            .IsRequired(false)
            .HasMaxLength(2048);

        builder.Property(challenge => challenge.CreatedAt)
            .IsRequired();

        builder.Property(challenge => challenge.ExpiresAt)
            .IsRequired();

        builder.Property(challenge => challenge.ChallengeEmailSentAt)
            .IsRequired(false);

        builder.Property(challenge => challenge.LastDeliveryAttemptAt)
            .IsRequired(false);

        builder.Property(challenge => challenge.DeliveryAttemptCount)
            .IsRequired();

        builder.Property(challenge => challenge.VerifiedAt)
            .IsRequired(false);

        builder.HasIndex(challenge => challenge.NormalizedEmailHash)
            .IsUnique()
            .HasDatabaseName("IX_PrivacyWithdrawalChallenges_NormalizedEmailHash");

        builder.HasIndex(challenge => challenge.KeyHash)
            .HasDatabaseName("IX_PrivacyWithdrawalChallenges_KeyHash");

        builder.HasIndex(challenge => new { challenge.VerifiedAt, challenge.ExpiresAt })
            .HasDatabaseName("IX_PrivacyWithdrawalChallenges_VerifiedAt_ExpiresAt");
    }
}
