using Handball.Belgium.Rules.Quiz.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace QuizManagement.Infrastructure.Configurations;

public class QuizTitleConfiguration : IEntityTypeConfiguration<QuizTitle>
{
    public void Configure(EntityTypeBuilder<QuizTitle> builder)
    {
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.Value)
            .IsRequired()
            .HasMaxLength(256);
    }
}