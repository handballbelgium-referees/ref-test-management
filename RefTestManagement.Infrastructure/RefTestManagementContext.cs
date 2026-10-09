using Handball.Belgium.RefTestManagement.Application.Abstractions;
using Handball.Belgium.RefTestManagement.Application.Abstractions.Persistence;
using Handball.Belgium.RefTestManagement.AuditLog;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Domain.Privacy;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;
using Handball.Belgium.RefTestManagement.Infrastructure.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Infrastructure;

public class RefTestManagementContext(DbContextOptions<RefTestManagementContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<RefTest> RefTests { get; set; } = null!;
    public DbSet<RefTestTitle> RefTestTitles { get; set; } = null!;
    public DbSet<Job> Jobs { get; set; } = null!;
    public DbSet<PersonalDataExportRequest> PersonalDataExportRequests { get; set; } = null!;
    public DbSet<PrivacyWithdrawalChallenge> PrivacyWithdrawalChallenges { get; set; } = null!;
    public DbSet<PrivacyWithdrawalBatch> PrivacyWithdrawalBatches { get; set; } = null!;
    public DbSet<PrivacyWithdrawalBatchTarget> PrivacyWithdrawalBatchTargets { get; set; } = null!;
    public DbSet<AuditEvent> AuditEvents { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new RefTestTitleConfiguration());
        modelBuilder.ApplyConfiguration(new RefTestConfiguration());
        modelBuilder.ApplyConfiguration(new JobConfiguration());
        modelBuilder.ApplyConfiguration(new PersonalDataExportRequestConfiguration());
        modelBuilder.ApplyConfiguration(new PrivacyWithdrawalChallengeConfiguration());
        modelBuilder.ApplyConfiguration(new PrivacyWithdrawalBatchConfiguration());
        modelBuilder.ApplyConfiguration(new PrivacyWithdrawalBatchTargetConfiguration());
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

    IQueryable<Job> IJobPersistenceContext.Jobs => Jobs;

    void IJobPersistenceContext.AddJob(Job job) => Jobs.Add(job);

    IReadOnlySet<Guid> IUnitOfWork.CaptureStagedJobIds() =>
        Jobs.Local.Select(job => job.Id).ToHashSet();

    void IUnitOfWork.DiscardJobsStagedSince(IReadOnlySet<Guid> checkpoint)
    {
        foreach (var job in Jobs.Local.Where(job => !checkpoint.Contains(job.Id)).ToList())
            Entry(job).State = EntityState.Detached;
    }

    void IUnitOfWork.DiscardTrackedChanges() => ChangeTracker.Clear();

    async Task<RefTest?> IUnitOfWork.RestoreRefTestAsync(
        RefTest refTest, CancellationToken cancellationToken)
    {
        var refTestId = refTest.Id;
        refTest.ClearDomainEvents();
        Entry(refTest).State = EntityState.Detached;
        return await RefTests.FirstOrDefaultAsync(candidate => candidate.Id == refTestId, cancellationToken);
    }

    async Task<RefTest?> IUnitOfWork.RestoreChangesAsync(
        RefTest refTest, CancellationToken cancellationToken)
    {
        var refTestId = refTest.Id;
        var changedEntries = ChangeTracker.Entries()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        try
        {
            foreach (var entry in changedEntries)
            {
                if (entry.State == EntityState.Added)
                    entry.State = EntityState.Detached;
                else
                    await entry.ReloadAsync(cancellationToken);
            }
        }
        finally
        {
            // Domain events are transient and are not restored by EF's ReloadAsync.
            refTest.ClearDomainEvents();
        }

        // Reload does not restore RefTest's transient IssuedToken. Detach it and query a clean
        // aggregate so a failed token rotation cannot leak into a later item in this batch.
        Entry(refTest).State = EntityState.Detached;
        return await RefTests.FirstOrDefaultAsync(candidate => candidate.Id == refTestId, cancellationToken);
    }

    public Task<int> SaveChangesWithRetryAsync(CancellationToken cancellationToken = default)
    {
        var strategy = Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(cancellationToken, SaveChangesAsync);
    }
}
