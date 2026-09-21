using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Configurations;

public class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.HasKey(j => j.Id);

        // See RefTestConfiguration for the rationale. The worker's claim is a conditional UPDATE
        // that does not go through the change tracker, so this token only guards the tracked
        // completion, failure and cancellation writes.
        builder.Property(j => j.Version)
            .IsConcurrencyToken();

        builder.Property(j => j.JobType)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(j => j.Payload)
            .IsRequired();

        builder.Property(j => j.Status)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(j => j.Attempts)
            .IsRequired();

        builder.Property(j => j.CreatedAt)
            .IsRequired();

        builder.Property(j => j.ExecuteAfter)
            .IsRequired();

        builder.Property(j => j.CompletedAt)
            .IsRequired(false);

        builder.Property(j => j.LockedUntil)
            .IsRequired(false);

        builder.Property(j => j.ErrorMessage)
            .IsRequired(false);

        // Indexes for efficient querying
        builder.HasIndex(j => new { j.Status, j.ExecuteAfter, j.LockedUntil })
            .HasDatabaseName("IX_Jobs_Status_ExecuteAfter_LockedUntil");

        builder.HasIndex(j => j.CreatedAt)
            .HasDatabaseName("IX_Jobs_CreatedAt");
    }
}
