using Handball.Belgium.RefTestManagement.Api.Services;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Handball.Belgium.RefTestManagement.UnitTests;

/// <summary>
/// The Jobs table is an outbox. A job row is the durable promise that a side effect still owes to
/// happen, so an entity whose existence implies that side effect must be committed in the same
/// transaction as its job — otherwise a crash between the two writes leaves, for example, a
/// RefTest whose invitation is never sent.
/// </summary>
/// <remarks>
/// These tests work at the <see cref="JobEnqueueService"/> boundary rather than through the
/// GraphQL mutations, because that is where the unit-of-work decision actually lives. The
/// mutations only choose which mode to ask for.
/// </remarks>
public sealed class JobEnqueueUnitOfWorkTests
{
    private static RefTestInvitationTokenProtection TokenProtection() =>
        new(new EphemeralDataProtectionProvider());

    private static JobEnqueueService Service(RefTestManagementContext context) =>
        Service(context, TokenProtection());

    private static JobEnqueueService Service(
        RefTestManagementContext context,
        RefTestInvitationTokenProtection tokenProtection) =>
        new(context, tokenProtection, NullLogger<JobEnqueueService>.Instance);

    /// <summary>
    /// Seeds the title a RefTest points at, then builds the RefTest. The foreign key is real in
    /// SQLite, so the title has to exist before the test row can be written.
    /// </summary>
    private static async Task<RefTest> NewRefTestAsync(SqliteTestDatabase db, CancellationToken ct)
    {
        var title = RefTestTitle.Create("Season opener");

        await using (var seeder = db.CreateContext())
        {
            seeder.RefTestTitles.Add(title);
            await seeder.SaveChangesAsync(ct);
        }

        return RefTest.Create(
            title.Id,
            "Ada",
            "Lovelace",
            "ada@example.org",
            numberOfQuestions: 10,
            maxTimeInMinutes: 30,
            questionIds: ["q1", "q2"],
            sendInvitationAutomatically: true,
            sendResultsAutomatically: true);
    }

    [Fact]
    public async Task StagedJobIsNotVisibleToOtherConnectionsUntilTheCallerSaves()
    {
        var ct = TestContext.Current.CancellationToken;
        using var db = SqliteTestDatabase.Create();
        await using var context = db.CreateContext();
        var refTest = await NewRefTestAsync(db, ct);
        context.RefTests.Add(refTest);

        await Service(context).EnqueueInvitationEmailAsync(
            refTest, saveChanges: false, cancellationToken: ct);

        // A second context reads the database, not the first context's change tracker.
        await using (var observer = db.CreateContext())
        {
            Assert.Empty(await observer.Jobs.ToListAsync(ct));
            Assert.Empty(await observer.RefTests.ToListAsync(ct));
        }

        await context.SaveChangesAsync(ct);

        await using (var observer = db.CreateContext())
        {
            Assert.Single(await observer.Jobs.ToListAsync(ct));
            Assert.Single(await observer.RefTests.ToListAsync(ct));
        }
    }

    [Fact]
    public async Task EntityAndItsJobAreCommittedTogether()
    {
        var ct = TestContext.Current.CancellationToken;
        using var db = SqliteTestDatabase.Create();
        await using var context = db.CreateContext();

        var refTest = await NewRefTestAsync(db, ct);
        context.RefTests.Add(refTest);
        var tokenProtection = TokenProtection();
        await Service(context, tokenProtection).EnqueueInvitationEmailAsync(
            refTest, saveChanges: false, cancellationToken: ct);

        await context.SaveChangesAsync(ct);

        await using var observer = db.CreateContext();
        Assert.Single(await observer.RefTests.ToListAsync(ct));
        var storedJob = await observer.Jobs.SingleAsync(ct);
        using var payload = System.Text.Json.JsonDocument.Parse(storedJob.Payload);
        Assert.Equal(refTest.Token, payload.RootElement.GetProperty("tokenHash").GetString());
        Assert.DoesNotContain(refTest.GetIssuedToken(), storedJob.Payload, StringComparison.Ordinal);
        var storedRefTest = await observer.RefTests.SingleAsync(ct);
        var protectedToken = Assert.IsType<string>(storedRefTest.ProtectedInvitationToken);
        Assert.NotEqual(refTest.GetIssuedToken(), protectedToken);
        Assert.Equal(refTest.GetIssuedToken(), tokenProtection.Unprotect(protectedToken));
    }

    [Fact]
    public async Task CallerCanStageAJobOnItsOwnUnitOfWorkContext()
    {
        var ct = TestContext.Current.CancellationToken;
        using var db = SqliteTestDatabase.Create();
        await using var mutationContext = db.CreateContext();
        await using var serviceContext = db.CreateContext();
        var refTest = await NewRefTestAsync(db, ct);
        mutationContext.RefTests.Add(refTest);

        await Service(serviceContext).EnqueueInvitationEmailAsync(
            refTest,
            saveChanges: false,
            unitOfWorkContext: mutationContext,
            cancellationToken: ct);

        await mutationContext.SaveChangesAsync(ct);

        await using var observer = db.CreateContext();
        Assert.Single(await observer.Jobs.ToListAsync(ct));
    }

    [Fact]
    public async Task AFailedCommitLeavesNeitherTheEntityNorItsJob()
    {
        var ct = TestContext.Current.CancellationToken;
        using var db = SqliteTestDatabase.Create();
        await using var context = db.CreateContext();

        var refTest = await NewRefTestAsync(db, ct);
        context.RefTests.Add(refTest);
        await Service(context).EnqueueInvitationEmailAsync(
            refTest, saveChanges: false, cancellationToken: ct);

        // Force the single SaveChanges to fail: a duplicate primary key is the cheapest way to
        // make the database reject the batch that carries both rows.
        await using (var seeder = db.CreateContext())
        {
            seeder.RefTests.Add(refTest);
            await seeder.SaveChangesAsync(ct);
        }

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => context.SaveChangesAsync(ct));

        await using var observer = db.CreateContext();
        // Only the row the seeder wrote survives; crucially, no job was left behind.
        Assert.Empty(await observer.Jobs.ToListAsync(ct));
    }

    [Fact]
    public async Task ImmediateModeCommitsTheRefTestAndItsInvitationJobTogether()
    {
        var ct = TestContext.Current.CancellationToken;
        using var db = SqliteTestDatabase.Create();
        await using var context = db.CreateContext();
        var refTest = await NewRefTestAsync(db, ct);
        context.RefTests.Add(refTest);

        await Service(context).EnqueueInvitationEmailAsync(refTest, cancellationToken: ct);

        await using var observer = db.CreateContext();
        Assert.Single(await observer.Jobs.ToListAsync(ct));
        Assert.NotNull((await observer.RefTests.SingleAsync(ct)).ProtectedInvitationToken);
    }

    [Fact]
    public async Task StagedCancellationIsAlsoDeferred()
    {
        var ct = TestContext.Current.CancellationToken;
        using var db = SqliteTestDatabase.Create();
        var refTest = await NewRefTestAsync(db, ct);
        var refTestId = refTest.Id;

        await using (var seeder = db.CreateContext())
        {
            seeder.RefTests.Add(refTest);
            await Service(seeder).EnqueueInvitationEmailAsync(refTest, cancellationToken: ct);
        }

        await using var context = db.CreateContext();
        await Service(context).CancelPendingJobsForRefTestAsync(refTestId, saveChanges: false,
            cancellationToken: ct);

        await using (var observer = db.CreateContext())
            Assert.Equal(JobStatus.Pending, (await observer.Jobs.SingleAsync(ct)).Status);

        await context.SaveChangesAsync(ct);

        await using (var observer = db.CreateContext())
            Assert.Equal(JobStatus.Cancelled, (await observer.Jobs.SingleAsync(ct)).Status);
    }
}
