using Handball.Belgium.RefTestManagement.Domain;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Configurations;

public class RefTestTitleConfiguration : IEntityTypeConfiguration<RefTestTitle>
{
    public void Configure(EntityTypeBuilder<RefTestTitle> builder)
    {
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.Value)
            .IsRequired()
            .HasMaxLength(256);
    }
}