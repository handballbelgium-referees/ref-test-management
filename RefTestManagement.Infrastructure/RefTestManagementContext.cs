using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;
using Handball.Belgium.RefTestManagement.Infrastructure.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Infrastructure;

public class RefTestManagementContext(DbContextOptions<RefTestManagementContext> options)
    : DbContext(options)
{
    public DbSet<RefTest> RefTests { get; set; } = null!;
    public DbSet<RefTestTitle> RefTestTitles { get; set; } = null!;
    public DbSet<Job> Jobs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new RefTestTitleConfiguration());
        modelBuilder.ApplyConfiguration(new RefTestConfiguration());
        modelBuilder.ApplyConfiguration(new JobConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}

