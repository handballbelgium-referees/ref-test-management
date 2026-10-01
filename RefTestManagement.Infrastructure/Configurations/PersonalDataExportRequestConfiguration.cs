using Handball.Belgium.RefTestManagement.Domain.Privacy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Configurations;

public sealed class PersonalDataExportRequestConfiguration : IEntityTypeConfiguration<PersonalDataExportRequest>
{
    public void Configure(EntityTypeBuilder<PersonalDataExportRequest> builder)
    {
        builder.ToTable("PersonalDataExportRequests");
        builder.HasKey(request => request.Id);

        builder.Property(request => request.Version)
            .IsConcurrencyToken();

        builder.Property(request => request.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(request => request.KeyHash)
            .IsRequired(false)
            .HasMaxLength(64);

        builder.Property(request => request.ProtectedDeliveryKey)
            .IsRequired(false)
            .HasMaxLength(2048);

        builder.Property(request => request.CreatedAt)
            .IsRequired();

        builder.Property(request => request.ExpiresAt)
            .IsRequired();

        builder.Property(request => request.ChallengeEmailSentAt)
            .IsRequired(false);

        builder.Property(request => request.LastDeliveryAttemptAt)
            .IsRequired(false);

        builder.Property(request => request.DeliveryAttemptCount)
            .IsRequired();

        builder.Property(request => request.VerifiedAt)
            .IsRequired(false);

        builder.HasIndex(request => request.KeyHash)
            .HasDatabaseName("IX_PersonalDataExportRequests_KeyHash");

        builder.HasIndex(request => new { request.VerifiedAt, request.ExpiresAt })
            .HasDatabaseName("IX_PersonalDataExportRequests_VerifiedAt_ExpiresAt");
    }
}
