using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Queries;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.UnitTests;

/// <summary>
/// Covers <see cref="RefTestExpirationQueries.IsDueForExpiration"/>.
/// </summary>
/// <remarks>
/// Two things need proving, and they fail in different ways.
///
/// The behaviour: the predicate replaced an in-process check that encoded a rule per status. Drop a
/// branch and a whole category of test silently stops expiring.
///
/// The translation: EF throws when it cannot translate a <c>Where</c>, but only at the point the
/// query runs — which for a background sweep on a provider nobody develops against means a
/// production log entry, not a failing build. Generating the SQL for all four providers catches it
/// here instead. That needs no database, only the provider packages.
/// </remarks>
public class RefTestExpirationQueriesTests
{
    private static readonly TimeSpan UnstartedWindow = TimeSpan.FromDays(7);
    private static readonly DateTime Now = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    private static RefTest NewRefTest(Guid titleId, bool requiresApproval = false) =>
        RefTest.Create(
            titleId: titleId,
            firstName: "Ada",
            lastName: "Lovelace",
            email: "ada@example.org",
            numberOfQuestions: 10,
            maxTimeInMinutes: 30,
            questionIds: ["q1", "q2"],
            sendInvitationAutomatically: false,
            sendResultsAutomatically: false,
            requiresApproval: requiresApproval);

    private static RefTest StartedRefTest(Guid titleId)
    {
        var refTest = NewRefTest(titleId);
        refTest.AcceptPrivacyNotice("v1");
        refTest.Start("v1");
        return refTest;
    }

    private static async Task<List<Guid>> DueIdsAsync(
        SqliteTestDatabase database,
        DateTime now)
    {
        await using var context = database.CreateContext();
        return await context.RefTests
            .Where(RefTestExpirationQueries.IsDueForExpiration(now, UnstartedWindow))
            .Select(rt => rt.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<(SqliteTestDatabase Database, Guid TitleId)> SeedTitleAsync()
    {
        var database = SqliteTestDatabase.Create();
        await using var context = database.CreateContext();
        var title = RefTestTitle.Create("Season 2026");
        context.RefTestTitles.Add(title);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (database, title.Id);
    }

    [Fact]
    public async Task AnUnstartedTestInsideItsWindowIsNotDue()
    {
        var (database, titleId) = await SeedTitleAsync();
        using var _ = database;

        await using (var context = database.CreateContext())
        {
            context.RefTests.Add(NewRefTest(titleId));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // The test was created now, so it has the full window ahead of it.
        Assert.Empty(await DueIdsAsync(database, DateTime.UtcNow));
    }

    [Fact]
    public async Task AnUnstartedTestPastItsWindowIsDue()
    {
        var (database, titleId) = await SeedTitleAsync();
        using var _ = database;

        Guid id;
        await using (var context = database.CreateContext())
        {
            var refTest = NewRefTest(titleId);
            context.RefTests.Add(refTest);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            id = refTest.Id;
        }

        var due = await DueIdsAsync(database, DateTime.UtcNow + UnstartedWindow + TimeSpan.FromMinutes(1));

        Assert.Equal([id], due);
    }

    [Fact]
    public async Task AnInProgressTestInsideItsTimeLimitIsNotDue()
    {
        var (database, titleId) = await SeedTitleAsync();
        using var _ = database;

        await using (var context = database.CreateContext())
        {
            var refTest = StartedRefTest(titleId);
            context.RefTests.Add(refTest);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Assert.Empty(await DueIdsAsync(database, DateTime.UtcNow.AddMinutes(5)));
    }

    /// <summary>
    /// The one branch that reads a column on both sides of the arithmetic, and therefore the one
    /// most likely to be mistranslated.
    /// </summary>
    [Fact]
    public async Task AnInProgressTestPastItsTimeLimitIsDue()
    {
        var (database, titleId) = await SeedTitleAsync();
        using var _ = database;

        Guid id;
        await using (var context = database.CreateContext())
        {
            var refTest = StartedRefTest(titleId);
            context.RefTests.Add(refTest);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            id = refTest.Id;
        }

        // maxTimeInMinutes is 30.
        var due = await DueIdsAsync(database, DateTime.UtcNow.AddMinutes(31));

        Assert.Equal([id], due);
    }

    /// <summary>
    /// Extending time moves the deadline, so a test that was due must stop being due.
    /// </summary>
    [Fact]
    public async Task ExtendingTimeMovesTheDeadline()
    {
        var (database, titleId) = await SeedTitleAsync();
        using var _ = database;

        await using (var context = database.CreateContext())
        {
            var refTest = StartedRefTest(titleId);
            refTest.ExtendTime(60);
            context.RefTests.Add(refTest);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Assert.Empty(await DueIdsAsync(database, DateTime.UtcNow.AddMinutes(31)));
    }

    [Fact]
    public async Task AnAnonymizedTestIsNeverDue()
    {
        var (database, titleId) = await SeedTitleAsync();
        using var _ = database;

        await using (var context = database.CreateContext())
        {
            var refTest = NewRefTest(titleId);
            refTest.Anonymize();
            context.RefTests.Add(refTest);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Assert.Empty(await DueIdsAsync(database, DateTime.UtcNow + UnstartedWindow + TimeSpan.FromDays(365)));
    }

    /// <summary>
    /// A completed test has no clock left to run out. Before this predicate the sweep excluded it
    /// by status up front; now the predicate has to do it.
    /// </summary>
    [Fact]
    public async Task ACompletedTestIsNeverDue()
    {
        var (database, titleId) = await SeedTitleAsync();
        using var _ = database;

        await using (var context = database.CreateContext())
        {
            var refTest = StartedRefTest(titleId);
            refTest.Complete(
                questionScore: 10,
                answerScore: 20,
                answerTotal: 20,
                percentage: 100,
                selectedAnswerIds: ["a1"],
                wrongQuestionIds: [],
                wrongAnswerIds: [],
                language: "en",
                source: RefTestCompletionSource.Participant);
            context.RefTests.Add(refTest);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Assert.Empty(await DueIdsAsync(database, DateTime.UtcNow.AddYears(1)));
    }

    /// <summary>
    /// Neither status has been handed to a participant, so no clock is running. Privacy retention
    /// is what eventually clears them.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ATestAwaitingOrRefusedApprovalIsNeverDue(bool reject)
    {
        var (database, titleId) = await SeedTitleAsync();
        using var _ = database;

        await using (var context = database.CreateContext())
        {
            var refTest = NewRefTest(titleId, requiresApproval: true);

            if (reject)
                refTest.Reject("not this season");

            context.RefTests.Add(refTest);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Assert.Empty(await DueIdsAsync(database, DateTime.UtcNow.AddYears(1)));
    }

    /// <summary>
    /// Proves the predicate reaches SQL on every supported provider. A connection string is needed
    /// to build the model, but none of these connect: generating SQL is offline work.
    /// </summary>
    [Theory]
    [InlineData("SqlServer")]
    [InlineData("PostgreSQL")]
    [InlineData("MySQL")]
    [InlineData("SQLite")]
    public void ThePredicateTranslatesToSqlOnEveryProvider(string provider)
    {
        var builder = new DbContextOptionsBuilder<RefTestManagementContext>();

        switch (provider)
        {
            case "SqlServer":
                builder.UseSqlServer("Server=none;Database=none;Trusted_Connection=True;");
                break;
            case "PostgreSQL":
                builder.UseNpgsql("Host=none;Database=none;Username=none;Password=none");
                break;
            case "MySQL":
                builder.UseMySQL("Server=none;Database=none;User=none;Password=none;");
                break;
            case "SQLite":
                builder.UseSqlite("Data Source=:memory:");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(provider), provider, null);
        }

        using var context = new RefTestManagementContext(builder.Options);

        var sql = context.RefTests
            .Where(RefTestExpirationQueries.IsDueForExpiration(Now, UnstartedWindow))
            .Select(rt => rt.Id)
            .ToQueryString();

        // Every branch has to survive translation. If EF had evaluated any of it on the client the
        // corresponding column would be missing from the WHERE clause.
        Assert.Contains("InProgress", sql, StringComparison.Ordinal);
        Assert.Contains("Pending", sql, StringComparison.Ordinal);
        Assert.Contains("StartedAt", sql, StringComparison.Ordinal);
        Assert.Contains("CreatedAt", sql, StringComparison.Ordinal);
        Assert.Contains("MaxTimeInMinutes", sql, StringComparison.Ordinal);
        Assert.Contains("IsAnonymized", sql, StringComparison.Ordinal);
    }
}


