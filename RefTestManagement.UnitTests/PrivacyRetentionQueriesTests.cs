using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Infrastructure.Queries;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.UnitTests;

/// <summary>
/// Covers <see cref="PrivacyRetentionQueries.IsDueForErasure"/>.
/// </summary>
/// <remarks>
/// This predicate selects people for irreversible erasure, and both ways of getting it wrong are
/// silent. Dropping a branch keeps personal data past its lawful basis and nothing complains;
/// widening one anonymizes a live participant and there is no undo. So every status is pinned
/// explicitly, including the ones that must never match, because "never matches" is the assertion
/// that a future edit is most likely to break without noticing.
///
/// The translation matters as much as the logic. A predicate EF cannot translate still compiles and
/// still returns correct results — by loading the entire RefTests table and filtering in memory.
/// On the daily retention sweep that is a silent full-table read, so the SQL is asserted too.
/// </remarks>
public class PrivacyRetentionQueriesTests
{
    private static readonly DateTime Cutoff = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

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

    private static async Task<(SqliteTestDatabase Database, Guid TitleId)> SeedTitleAsync()
    {
        var database = SqliteTestDatabase.Create();
        await using var context = database.CreateContext();
        var title = RefTestTitle.Create("Season 2026");
        context.RefTestTitles.Add(title);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (database, title.Id);
    }

    private static async Task<List<Guid>> DueIdsAsync(SqliteTestDatabase database, DateTime cutoff)
    {
        await using var context = database.CreateContext();
        return await context.RefTests
            .Where(PrivacyRetentionQueries.IsDueForErasure(cutoff))
            .Select(refTest => refTest.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Backdates a column the domain refuses to set directly. Retention is entirely about
    /// timestamps in the past, and the aggregate stamps them with the current clock, so a test
    /// cannot otherwise produce a record old enough to be due.
    /// </summary>
    private static async Task BackdateAsync(
        SqliteTestDatabase database,
        Guid id,
        string column,
        DateTime value)
    {
        await using var context = database.CreateContext();
        await context.Database.ExecuteSqlRawAsync(
            $"UPDATE RefTests SET {column} = {{0}} WHERE Id = {{1}}",
            [value, id],
            TestContext.Current.CancellationToken);
    }

    private static async Task<Guid> AddAsync(
        SqliteTestDatabase database,
        Guid titleId,
        Action<RefTest> arrange,
        bool requiresApproval = false)
    {
        await using var context = database.CreateContext();
        var refTest = NewRefTest(titleId, requiresApproval);
        arrange(refTest);
        context.RefTests.Add(refTest);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return refTest.Id;
    }

    private static void Complete(RefTest refTest)
    {
        refTest.AcceptPrivacyNotice("v1");
        refTest.Start("v1");
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
    }

    [Fact]
    public async Task ACompletedTestOlderThanTheWindowIsDue()
    {
        var (database, titleId) = await SeedTitleAsync();
        using var _ = database;

        var id = await AddAsync(database, titleId, Complete);
        await BackdateAsync(database, id, "CompletedAt", Cutoff.AddDays(-1));

        Assert.Equal([id], await DueIdsAsync(database, Cutoff));
    }

    [Fact]
    public async Task ACompletedTestInsideTheWindowIsNotDue()
    {
        var (database, titleId) = await SeedTitleAsync();
        using var _ = database;

        var id = await AddAsync(database, titleId, Complete);
        await BackdateAsync(database, id, "CompletedAt", Cutoff.AddDays(1));

        Assert.Empty(await DueIdsAsync(database, Cutoff));
    }

    [Fact]
    public async Task AnExpiredTestOlderThanTheWindowIsDue()
    {
        var (database, titleId) = await SeedTitleAsync();
        using var _ = database;

        var id = await AddAsync(database, titleId, refTest => refTest.Expire());
        await BackdateAsync(database, id, "ExpiredAt", Cutoff.AddDays(-1));

        Assert.Equal([id], await DueIdsAsync(database, Cutoff));
    }

    /// <summary>
    /// Neither status stamps a terminal timestamp, so retention has to run from CreatedAt. If this
    /// branch were dropped these records would keep personal data forever, and nothing anywhere
    /// would report it.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnApprovalTestOlderThanTheWindowIsDue(bool reject)
    {
        var (database, titleId) = await SeedTitleAsync();
        using var _ = database;

        var id = await AddAsync(
            database,
            titleId,
            refTest =>
            {
                if (reject)
                    refTest.Reject("not this season");
            },
            requiresApproval: true);

        await BackdateAsync(database, id, "CreatedAt", Cutoff.AddDays(-1));

        Assert.Equal([id], await DueIdsAsync(database, Cutoff));
    }

    /// <summary>
    /// The predicate must key off the right timestamp per status. A rejected test that is old
    /// enough by CreatedAt is due even though it has no CompletedAt at all — and a NULL comparison
    /// in SQL is false, so getting this wrong excludes the row rather than throwing.
    /// </summary>
    [Fact]
    public async Task AnApprovalTestInsideTheWindowIsNotDue()
    {
        var (database, titleId) = await SeedTitleAsync();
        using var _ = database;

        await AddAsync(database, titleId, _ => { }, requiresApproval: true);

        Assert.Empty(await DueIdsAsync(database, Cutoff.AddYears(-10)));
    }

    /// <summary>
    /// The most important assertion here. A pending or in-progress test belongs to someone who may
    /// still be sitting it; erasing one on a timer would destroy a live assessment, irreversibly,
    /// no matter how old the row is.
    /// </summary>
    [Fact]
    public async Task ALiveTestIsNeverDueHoweverOldItIs()
    {
        var (database, titleId) = await SeedTitleAsync();
        using var _ = database;

        var pending = await AddAsync(database, titleId, _ => { });
        var inProgress = await AddAsync(
            database,
            titleId,
            refTest =>
            {
                refTest.AcceptPrivacyNotice("v1");
                refTest.Start("v1");
            });

        await BackdateAsync(database, pending, "CreatedAt", Cutoff.AddYears(-50));
        await BackdateAsync(database, inProgress, "CreatedAt", Cutoff.AddYears(-50));
        await BackdateAsync(database, inProgress, "StartedAt", Cutoff.AddYears(-50));

        Assert.Empty(await DueIdsAsync(database, Cutoff));
    }

    /// <summary>
    /// Erasure is idempotent, but re-selecting erased rows would mean the sweep does the same
    /// pointless work every day forever.
    /// </summary>
    [Fact]
    public async Task AnAlreadyErasedTestIsNotDue()
    {
        var (database, titleId) = await SeedTitleAsync();
        using var _ = database;

        var id = await AddAsync(
            database,
            titleId,
            refTest =>
            {
                Complete(refTest);
                refTest.Anonymize();
            });

        await BackdateAsync(database, id, "CompletedAt", Cutoff.AddYears(-10));

        Assert.Empty(await DueIdsAsync(database, Cutoff));
    }

    /// <summary>
    /// Proves the rule reaches SQL on every supported provider rather than falling back to the
    /// client. A connection string is needed to build the model, but none of these connect.
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
            .Where(PrivacyRetentionQueries.IsDueForErasure(Cutoff))
            .Select(refTest => refTest.Id)
            .ToQueryString();

        // Every timestamp the rule keys off has to appear in the WHERE clause. A missing one means
        // EF evaluated that branch in memory, which on this sweep is a full-table read.
        Assert.Contains("CompletedAt", sql, StringComparison.Ordinal);
        Assert.Contains("ExpiredAt", sql, StringComparison.Ordinal);
        Assert.Contains("CreatedAt", sql, StringComparison.Ordinal);
        Assert.Contains("IsAnonymized", sql, StringComparison.Ordinal);
    }
}
