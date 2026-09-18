using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.UnitTests;

/// <summary>
/// Proves that a write built from a stale read is rejected rather than silently winning.
/// </summary>
/// <remarks>
/// These tests use two contexts over one database. That is the only honest way to express "two
/// users hit save at the same time": a single context would serve the second read from its
/// identity map and never produce the stale state the token exists to catch.
/// </remarks>
public class ConcurrencyTokenTests
{
    [Fact]
    public async Task Saving_a_ref_test_from_a_stale_read_is_rejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var database = SqliteTestDatabase.Create();

        var id = await SeedRefTestAsync(database, cancellationToken);

        await using var first = database.CreateContext();
        await using var second = database.CreateContext();

        var byFirst = await first.RefTests.SingleAsync(r => r.Id == id, cancellationToken);
        var bySecond = await second.RefTests.SingleAsync(r => r.Id == id, cancellationToken);

        byFirst.UpdateNotificationSettings(sendInvitationsAutomatically: false, sendResultsAutomatically: false);
        await first.SaveChangesAsync(cancellationToken);

        // bySecond was loaded before the save above, so it still carries the old version. The new
        // values have to differ from the ones it loaded, or EF would see nothing modified and
        // issue no UPDATE for the token to guard.
        bySecond.UpdateNotificationSettings(sendInvitationsAutomatically: true, sendResultsAutomatically: false);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => second.SaveChangesAsync(cancellationToken));
    }

    [Fact]
    public async Task The_winning_write_is_the_one_that_survives()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var database = SqliteTestDatabase.Create();

        var id = await SeedRefTestAsync(database, cancellationToken);

        await using var first = database.CreateContext();
        await using var second = database.CreateContext();

        var byFirst = await first.RefTests.SingleAsync(r => r.Id == id, cancellationToken);
        var bySecond = await second.RefTests.SingleAsync(r => r.Id == id, cancellationToken);

        byFirst.UpdateNotificationSettings(sendInvitationsAutomatically: false, sendResultsAutomatically: false);
        await first.SaveChangesAsync(cancellationToken);

        bySecond.UpdateNotificationSettings(sendInvitationsAutomatically: true, sendResultsAutomatically: false);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => second.SaveChangesAsync(cancellationToken));

        await using var verification = database.CreateContext();
        var stored = await verification.RefTests.SingleAsync(r => r.Id == id, cancellationToken);

        Assert.False(stored.SendInvitationsAutomatically);
        Assert.False(stored.SendResultsAutomatically);
    }

    [Fact]
    public async Task The_version_advances_by_one_per_save()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var database = SqliteTestDatabase.Create();

        var id = await SeedRefTestAsync(database, cancellationToken);

        await using var context = database.CreateContext();
        var refTest = await context.RefTests.SingleAsync(r => r.Id == id, cancellationToken);
        var initial = refTest.Version;

        refTest.UpdateNotificationSettings(sendInvitationsAutomatically: false, sendResultsAutomatically: false);
        await context.SaveChangesAsync(cancellationToken);
        Assert.Equal(initial + 1, refTest.Version);

        refTest.UpdateNotificationSettings(sendInvitationsAutomatically: true, sendResultsAutomatically: true);
        await context.SaveChangesAsync(cancellationToken);
        Assert.Equal(initial + 2, refTest.Version);
    }

    [Fact]
    public async Task An_unchanged_entity_does_not_advance_the_version()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var database = SqliteTestDatabase.Create();

        var id = await SeedRefTestAsync(database, cancellationToken);

        await using var context = database.CreateContext();
        var refTest = await context.RefTests.SingleAsync(r => r.Id == id, cancellationToken);
        var initial = refTest.Version;

        await context.SaveChangesAsync(cancellationToken);

        Assert.Equal(initial, refTest.Version);
    }

    [Fact]
    public async Task Claiming_a_job_advances_its_version()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var database = SqliteTestDatabase.Create();

        var job = Job.Create(JobType.InvitationEmail, "{}", DateTime.UtcNow.AddMinutes(-1));

        await using (var seed = database.CreateContext())
        {
            seed.Jobs.Add(job);
            await seed.SaveChangesAsync(cancellationToken);
        }

        await using var context = database.CreateContext();

        // The same shape as BackgroundJobService.ClaimJobAsync: a bulk update, which bypasses the
        // change tracker and therefore the interceptor.
        var claimed = await context.Jobs
            .Where(j => j.Id == job.Id)
            .ExecuteUpdateAsync(setters => setters
                    .SetProperty(j => j.Status, JobStatus.Processing)
                    .SetProperty(j => j.Version, j => j.Version + 1),
                cancellationToken);

        Assert.Equal(1, claimed);

        await using var verification = database.CreateContext();
        var stored = await verification.Jobs.SingleAsync(j => j.Id == job.Id, cancellationToken);

        Assert.Equal(job.Version + 1, stored.Version);
    }

    private static async Task<Guid> SeedRefTestAsync(
        SqliteTestDatabase database,
        CancellationToken cancellationToken)
    {
        await using var context = database.CreateContext();

        // The RefTest -> RefTestTitle foreign key is enforced, so the title has to exist first.
        var title = RefTestTitle.Create("Concurrency");
        context.RefTestTitles.Add(title);

        var refTest = RefTest.Create(
            title.Id,
            "Ada",
            "Lovelace",
            "ada@example.com",
            numberOfQuestions: 10,
            maxTimeInMinutes: 30,
            questionIds: ["q1"],
            sendInvitationAutomatically: true,
            sendResultsAutomatically: true);

        context.RefTests.Add(refTest);
        await context.SaveChangesAsync(cancellationToken);

        return refTest.Id;
    }
}
