using Handball.Belgium.RefTestManagement.AuditLog;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;
using Handball.Belgium.RefTestManagement.Infrastructure.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Infrastructure;

public class RefTestManagementContext(DbContextOptions<RefTestManagementContext> options)
    : DbContext(options), IJobPersistenceContext
{
    public DbSet<RefTest> RefTests { get; set; } = null!;
    public DbSet<RefTestTitle> RefTestTitles { get; set; } = null!;
    public DbSet<Job> Jobs { get; set; } = null!;
    public DbSet<AuditEvent> AuditEvents { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new RefTestTitleConfiguration());
        modelBuilder.ApplyConfiguration(new RefTestConfiguration());
        modelBuilder.ApplyConfiguration(new JobConfiguration());
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuditEvent).Assembly);

        // MySQL maps VARCHAR(n) in utf8mb4 to n×4 bytes toward the 65535-byte row size limit.
        // These comma-separated ID list columns are not indexed, so LONGTEXT is safe and necessary.
        if (Database.ProviderName == "MySql.EntityFrameworkCore")
        {
            modelBuilder.Entity<RefTest>(b =>
            {
                b.Property(x => x.QuestionIds).HasColumnType("longtext");
                b.Property(x => x.SelectedAnswerIds).HasColumnType("longtext");
                b.Property(x => x.WrongQuestionIds).HasColumnType("longtext");
                b.Property(x => x.WrongAnswerIds).HasColumnType("longtext");
            });
        }

        base.OnModelCreating(modelBuilder);
    }

    public Task<int> SaveChangesWithRetryAsync(CancellationToken cancellationToken = default)
    {
        var strategy = Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(cancellationToken, SaveChangesAsync);
    }
}
