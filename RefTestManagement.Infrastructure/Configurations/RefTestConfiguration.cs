using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Configurations;

public class RefTestConfiguration : IEntityTypeConfiguration<RefTest>
{
    public void Configure(EntityTypeBuilder<RefTest> builder)
    {
        builder.HasKey(x => x.Id);
        
        builder.HasOne(x => x.Title)
            .WithMany()
            .HasForeignKey(x => x.TitleId)
            .IsRequired();

        builder.Property(x => x.FirstName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.LastName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.SendInvitationsAutomatically);
        builder.Property(x => x.InvitationSentAt);

        builder.Property(x => x.Token)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(x => x.Token)
            .IsUnique();

        builder.Property(x => x.NumberOfQuestions)
            .IsRequired();

        builder.Property(x => x.MaxTimeInMinutes)
            .IsRequired();

        builder.Property(x => x.QuestionIds)
            .HasConversion(
                v => string.Join(',', v.Select(g => g.ToString())),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .ToList())
            .HasMaxLength(4000)
            .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                (c1, c2) => c1!.SequenceEqual(c2!),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToList()));

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.ExpiredAt);

        builder.Property(x => x.Percentage);
        
        builder.Property(x => x.SelectedAnswerIds)
            .HasConversion(
                v => string.Join(',', v.Select(g => g.ToString())),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .ToList())
            .HasMaxLength(4000)
            .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                (c1, c2) => c1!.SequenceEqual(c2!),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToList()));

        builder.Property(x => x.QuestionScore);
        builder.Property(x => x.StartedAt);
        builder.Property(x => x.WrongQuestionIds)
            .HasConversion(
                v => string.Join(',', v.Select(g => g.ToString())),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .ToList())
            .HasMaxLength(4000)
            .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                (c1, c2) => c1!.SequenceEqual(c2!),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToList()));
        
        builder.Property(x => x.WrongAnswerIds)
            .HasConversion(
                v => string.Join(',', v.Select(g => g.ToString())),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .ToList())
            .HasMaxLength(4000)
            .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                (c1, c2) => c1!.SequenceEqual(c2!),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToList()));

        builder.Property(x => x.SendResultsAutomatically);
        builder.Property(x => x.ResultsSentAt);

        builder.Property(x => x.PrivacyNoticeVersion)
            .HasMaxLength(32);

        builder.Property(x => x.PrivacyNoticeAcceptedAt);

        builder.Property(x => x.RejectionReason)
            .HasMaxLength(1000);

        builder.Property(x => x.ScheduledAt);

        builder.Property(x => x.CreatorName)
            .HasMaxLength(256)
            .HasDefaultValue(string.Empty);

        builder.Property(x => x.CreatorEmail)
            .HasMaxLength(256)
            .HasDefaultValue(string.Empty);

        builder.HasIndex(x => x.Email);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.CreatedAt);
    }
}