using Handball.Belgium.RefTestManagement.Api.Services;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Handball.Belgium.RefTestManagement.UnitTests;

/// <summary>
/// The domain takes the current time from its caller, so time-dependent rules can be checked at
/// an exact instant instead of racing the system clock.
/// </summary>
public sealed class RefTestClockTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private static RefTest NewRefTest(DateTime? scheduledAt = null) =>
        RefTest.Create(
            Guid.NewGuid(), "Ada", "Lovelace", "ada@example.org",
            numberOfQuestions: 2, maxTimeInMinutes: 30, questionIds: ["q1", "q2"],
            sendInvitationAutomatically: false, sendResultsAutomatically: false,
            scheduledAt: scheduledAt, now: T0);

    [Fact]
    public void Create_StampsTheSuppliedTime()
    {
        Assert.Equal(T0, NewRefTest().CreatedAt);
    }

    [Fact]
    public void Start_HonoursTheScheduleAtTheSuppliedTime()
    {
        var refTest = NewRefTest(scheduledAt: T0.AddHours(1));
        refTest.AcceptPrivacyNotice("v1", T0);

        Assert.Throws<RefTestValidationException>(() => refTest.Start("v1", T0.AddMinutes(59)));

        refTest.Start("v1", T0.AddHours(1));
        Assert.Equal(T0.AddHours(1), refTest.StartedAt);
    }

    [Fact]
    public void Deadline_IsEvaluatedAtTheSuppliedTime()
    {
        var refTest = NewRefTest();
        refTest.AcceptPrivacyNotice("v1", T0);
        refTest.Start("v1", T0);
        var deadline = T0.AddMinutes(30).Add(RefTestExpirationRules.DeadlineGrace);

        Assert.False(refTest.HasPassedDeadline(deadline.AddTicks(-1)));
        Assert.True(refTest.HasPassedDeadline(deadline));
        Assert.Throws<InvalidRefTestStatusException>(() => refTest.SaveProgress(1, ["a1"], now: deadline));

        refTest.SaveProgress(1, ["a1"], now: deadline.AddTicks(-1));
        refTest.Complete(1, 1, 2, 50, ["a1"], [], [], now: deadline.AddTicks(-1));
        Assert.Equal(deadline.AddTicks(-1), refTest.CompletedAt);
    }

    [Fact]
    public void IsExpired_UsesTheSuppliedTime()
    {
        var refTest = NewRefTest();
        var expiry = TimeSpan.FromDays(7);

        Assert.False(refTest.IsExpired(expiry, T0.AddDays(7).AddTicks(-1)));
        Assert.True(refTest.IsExpired(expiry, T0.AddDays(7)));

        refTest.Expire(T0.AddDays(7));
        Assert.Equal(T0.AddDays(7), refTest.ExpiredAt);
    }

    [Fact]
    public async Task JobEnqueueService_StampsJobsWithItsTimeProvider()
    {
        var ct = TestContext.Current.CancellationToken;
        using var database = SqliteTestDatabase.Create();
        await using var context = database.CreateContext();
        var service = new JobEnqueueService(
            context,
            new RefTestInvitationTokenProtection(new EphemeralDataProtectionProvider()),
            NullLogger<JobEnqueueService>.Instance,
            new FixedTimeProvider(new DateTimeOffset(T0)));

        await service.EnqueueReportEmailAsync(
            new ReportEmailPayload(["staff@example.org"], [], "20260101"),
            cancellationToken: ct);

        var job = await context.Jobs.AsNoTracking().SingleAsync(ct);
        Assert.Equal(JobType.ReportEmail, job.JobType);
        Assert.Equal(T0, job.CreatedAt);
        Assert.Equal(T0, job.ExecuteAfter);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
