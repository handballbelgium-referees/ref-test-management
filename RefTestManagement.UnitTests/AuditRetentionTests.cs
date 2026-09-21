using Handball.Belgium.RefTestManagement.Api.BackgroundServices;
using Handball.Belgium.RefTestManagement.AuditLog;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public class AuditRetentionTests
{
    [Fact]
    public async Task InterceptorAuditsAddedEntitiesWithoutAuditingAuditEvents()
    {
        using var database = SqliteTestDatabase.Create();
        var options = new AuditLogOptions();
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        };

        var job = Job.Create(JobType.InvitationEmail, """{"email":"john.doe@example.com"}""");
        await using (var context = database.CreateContext(
                         new AuditSaveChangesInterceptor(httpContextAccessor, options)))
        {
            context.Jobs.Add(job);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var verification = database.CreateContext();
        var auditEvents = await verification.AuditEvents.ToListAsync(TestContext.Current.CancellationToken);

        var audit = Assert.Single(auditEvents);
        Assert.Equal(job.Id.ToString(), audit.StreamId);
        Assert.Equal("EntityCreated", audit.Type);
        Assert.Contains("john.doe@example.com", audit.Data, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RetentionRedactsHistoricalRowsAndSetsRedactedAt()
    {
        using var database = SqliteTestDatabase.Create();
        var now = new DateTime(2026, 1, 31, 12, 0, 0, DateTimeKind.Utc);

        await using (var context = database.CreateContext())
        {
            context.AuditEvents.AddRange(
                AuditEvent("old", now.AddDays(-31), """{"email":"old@example.com","score":12}"""),
                AuditEvent("recent", now.AddDays(-29), """{"email":"recent@example.com"}"""),
                AuditEvent(
                    "historical-archive",
                    now.AddDays(-40),
                    """{"email":"already-redacted@example.com"}""",
                    isArchived: true));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.CreateContext())
        {
            var count = await AuditLogCleanupService.RedactOldAuditLogsAsync(
                context,
                new AuditLogOptions { RetentionDays = 30 },
                now,
                TestContext.Current.CancellationToken);

            Assert.Equal(2, count);
        }

        await using (var context = database.CreateContext())
        {
            var count = await AuditLogCleanupService.RedactOldAuditLogsAsync(
                context,
                new AuditLogOptions { RetentionDays = 30 },
                now,
                TestContext.Current.CancellationToken);

            Assert.Equal(0, count);
        }

        await using var verification = database.CreateContext();
        var rows = await verification.AuditEvents
            .OrderBy(x => x.StreamId)
            .ToListAsync(TestContext.Current.CancellationToken);

        var old = Assert.Single(rows, x => x.StreamId == "old");
        Assert.True(old.IsArchived);
        Assert.Equal(now, old.RedactedAt);
        Assert.DoesNotContain("old@example.com", old.Data, StringComparison.Ordinal);
        Assert.Equal(AuditPiiRedactor.RedactedValue, old.ActorName);
        Assert.Equal(AuditPiiRedactor.RedactedValue, old.ActorEmail);

        var historical = Assert.Single(rows, x => x.StreamId == "historical-archive");
        Assert.Equal(now, historical.RedactedAt);
        Assert.DoesNotContain("already-redacted@example.com", historical.Data, StringComparison.Ordinal);
        Assert.True(historical.IsArchived);

        var recent = Assert.Single(rows, x => x.StreamId == "recent");
        Assert.False(recent.IsArchived);
        Assert.Null(recent.RedactedAt);
        Assert.Contains("recent@example.com", recent.Data, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RetentionProcessesArchivedRowsThatWereNeverRedacted()
    {
        using var database = SqliteTestDatabase.Create();
        var now = new DateTime(2026, 1, 31, 12, 0, 0, DateTimeKind.Utc);

        await using (var context = database.CreateContext())
        {
            context.AuditEvents.Add(AuditEvent(
                "legacy",
                now.AddDays(-31),
                """{"firstName":"Jane","email":"jane@example.com"}""",
                isArchived: true));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.CreateContext())
        {
            await AuditLogCleanupService.RedactOldAuditLogsAsync(
                context,
                new AuditLogOptions { RetentionDays = 30 },
                now,
                TestContext.Current.CancellationToken);
        }

        await using var verification = database.CreateContext();
        var legacy = await verification.AuditEvents.SingleAsync(
            x => x.StreamId == "legacy",
            TestContext.Current.CancellationToken);

        Assert.Equal(now, legacy.RedactedAt);
        Assert.DoesNotContain("jane@example.com", legacy.Data, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RetentionLeavesRowsUntouchedWhenCancelledBeforeTheSweep()
    {
        using var database = SqliteTestDatabase.Create();
        var now = new DateTime(2026, 1, 31, 12, 0, 0, DateTimeKind.Utc);

        await using (var context = database.CreateContext())
        {
            context.AuditEvents.Add(AuditEvent(
                "cancelled",
                now.AddDays(-31),
                """{"email":"cancelled@example.com"}"""));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await using (var context = database.CreateContext())
        {
            var count = await AuditLogCleanupService.RedactOldAuditLogsAsync(
                context,
                new AuditLogOptions { RetentionDays = 30 },
                now,
                cancellation.Token);

            Assert.Equal(0, count);
        }

        await using var verification = database.CreateContext();
        var row = await verification.AuditEvents.SingleAsync(
            x => x.StreamId == "cancelled",
            TestContext.Current.CancellationToken);
        Assert.Null(row.RedactedAt);
        Assert.Contains("cancelled@example.com", row.Data, StringComparison.Ordinal);
    }

    private static AuditEvent AuditEvent(
        string streamId,
        DateTime timestamp,
        string data,
        bool isArchived = false) =>
        new()
        {
            StreamId = streamId,
            Version = 1,
            Type = "RefTestCreated",
            Timestamp = timestamp,
            Data = data,
            ActorName = "Jane Doe",
            ActorEmail = "jane@example.com",
            IsArchived = isArchived
        };
}
