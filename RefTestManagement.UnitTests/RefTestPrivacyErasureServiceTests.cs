using Handball.Belgium.RefTestManagement.AuditLog;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using DomainEvents = Handball.Belgium.RefTestManagement.Domain.RefTests.Events;

namespace Handball.Belgium.RefTestManagement.UnitTests;

/// <summary>
/// Covers <see cref="RefTestPrivacyErasureService"/>.
/// </summary>
/// <remarks>
/// This is the code that answers a GDPR erasure request, and it is the one place in the system
/// where being wrong is unrecoverable in both directions. Under-erasing leaves a name or an email
/// behind in a place nobody thinks to look — a queued job payload, an audit row — and the
/// organisation has told the participant it deleted their data when it did not. Over-erasing
/// destroys the record of who performed the erasure, which is the accountability evidence that
/// makes the deletion defensible in the first place.
///
/// Neither failure shows up in the UI. The RefTest looks erased either way. So the assertions here
/// deliberately reach past the aggregate into the two side-tables the flow touches.
///
/// The actor rule is the subtle one and has its own tests below: whether the anonymization event's
/// own actor gets redacted depends entirely on who asked, because for a participant withdrawal the
/// recorded actor *is* the participant.
/// </remarks>
public class RefTestPrivacyErasureServiceTests
{
    private const string ParticipantEmail = "ada@example.org";
    private const string ParticipantFirstName = "Ada";
    private const string ParticipantLastName = "Lovelace";

    private static RefTest NewRefTest(Guid titleId) =>
        RefTest.Create(
            titleId: titleId,
            firstName: ParticipantFirstName,
            lastName: ParticipantLastName,
            email: ParticipantEmail,
            numberOfQuestions: 10,
            maxTimeInMinutes: 30,
            questionIds: ["q1", "q2"],
            sendInvitationAutomatically: false,
            sendResultsAutomatically: false,
            requiresApproval: false);

    private static async Task<(SqliteTestDatabase Database, Guid TitleId)> SeedTitleAsync()
    {
        var database = SqliteTestDatabase.Create();
        await using var context = database.CreateContext();
        context.RefTestTitles.Add(RefTestTitle.Create("Season 2026"));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (database, await context.RefTestTitles.Select(t => t.Id).SingleAsync(TestContext.Current.CancellationToken));
    }

    private static AuditEvent AuditEventFor(Guid refTestId, string type, string? data = null) =>
        new()
        {
            StreamId = refTestId.ToString(),
            Type = type,
            Version = 1,
            Data = data ?? $$"""{"firstName":"{{ParticipantFirstName}}","email":"{{ParticipantEmail}}"}""",
            ActorName = $"{ParticipantFirstName} {ParticipantLastName}",
            ActorEmail = ParticipantEmail
        };

    /// <summary>
    /// A job payload that mentions the RefTest, shaped like the real invitation job: the id it
    /// keys off plus the personal data the email needs.
    /// </summary>
    private static Job JobFor(Guid refTestId, JobType type = JobType.InvitationEmail) =>
        Job.Create(
            type,
            $$"""{"refTestId":"{{refTestId}}","email":"{{ParticipantEmail}}","name":"{{ParticipantFirstName}}"}""");

    private static async Task<Guid> SeedRefTestAsync(SqliteTestDatabase database, Guid titleId)
    {
        await using var context = database.CreateContext();
        var refTest = NewRefTest(titleId);
        context.RefTests.Add(refTest);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return refTest.Id;
    }

    private static async Task EraseAsync(
        SqliteTestDatabase database,
        Guid refTestId,
        ErasureInitiator initiator)
    {
        await using var context = database.CreateContext();
        var refTest = await context.RefTests.SingleAsync(
            rt => rt.Id == refTestId,
            TestContext.Current.CancellationToken);

        await new RefTestPrivacyErasureService(context)
            .EraseAsync(refTest, initiator, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ErasureRemovesPersonalDataFromTheRecord()
    {
        var (database, titleId) = await SeedTitleAsync();
        using var _ = database;
        var id = await SeedRefTestAsync(database, titleId);

        await EraseAsync(database, id, ErasureInitiator.Operator);

        await using var context = database.CreateContext();
        var erased = await context.RefTests.SingleAsync(rt => rt.Id == id, TestContext.Current.CancellationToken);

        Assert.True(erased.IsAnonymized);
        Assert.DoesNotContain(ParticipantFirstName, erased.FirstName, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ParticipantLastName, erased.LastName, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ParticipantEmail, erased.Email, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The row survives on purpose. Erasure is not deletion here — the anonymized record is what
    /// proves the assessment happened and that the data was removed.
    /// </summary>
    [Fact]
    public async Task ErasureKeepsTheRecordItself()
    {
        var (database, titleId) = await SeedTitleAsync();
        using var _ = database;
        var id = await SeedRefTestAsync(database, titleId);

        await EraseAsync(database, id, ErasureInitiator.Operator);

        await using var context = database.CreateContext();
        Assert.True(await context.RefTests.AnyAsync(rt => rt.Id == id, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// The retention sweep runs daily and will re-select nothing, but the withdraw-consent endpoint
    /// is public and can be called twice. A second erasure must not overwrite the first — in
    /// particular it must not re-anonymize and stamp a fresh anonymization event.
    /// </summary>
    [Fact]
    public async Task ErasingTwiceIsANoOp()
    {
        var (database, titleId) = await SeedTitleAsync();
        using var _ = database;
        var id = await SeedRefTestAsync(database, titleId);

        await EraseAsync(database, id, ErasureInitiator.Operator);

        string firstEmail;
        await using (var context = database.CreateContext())
        {
            firstEmail = (await context.RefTests.SingleAsync(rt => rt.Id == id, TestContext.Current.CancellationToken))
                .Email;
        }

        await EraseAsync(database, id, ErasureInitiator.Operator);

        await using var after = database.CreateContext();
        var second = await after.RefTests.SingleAsync(rt => rt.Id == id, TestContext.Current.CancellationToken);

        // Anonymize() writes a per-erasure placeholder, so an accidental second pass would change
        // the value rather than leave it alone.
        Assert.Equal(firstEmail, second.Email);
    }

    /// <summary>
    /// The job payload carries the participant's name, email and token. Erasing the RefTest while
    /// leaving a queued invitation behind both retains the data and sends the email afterwards.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ErasureCancelsJobsStillReferencingTheRefTest(bool inFlight)
    {
        var (database, titleId) = await SeedTitleAsync();
        using var _ = database;
        var id = await SeedRefTestAsync(database, titleId);

        Guid jobId;
        await using (var context = database.CreateContext())
        {
            var job = JobFor(id);
            if (inFlight)
                job.MarkAsProcessing(TimeSpan.FromMinutes(5));

            context.Jobs.Add(job);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            jobId = job.Id;
        }

        await EraseAsync(database, id, ErasureInitiator.Operator);

        await using var after = database.CreateContext();
        var cancelled = await after.Jobs.SingleAsync(j => j.Id == jobId, TestContext.Current.CancellationToken);

        Assert.Equal(JobStatus.Cancelled, cancelled.Status);
        Assert.DoesNotContain(ParticipantEmail, cancelled.Payload, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A job belonging to somebody else must not be collateral damage. The match is a substring
    /// search on the payload, so this pins that it is actually scoped to the right id.
    /// </summary>
    [Fact]
    public async Task ErasureLeavesJobsForOtherRefTestsAlone()
    {
        var (database, titleId) = await SeedTitleAsync();
        using var _ = database;
        var id = await SeedRefTestAsync(database, titleId);
        var otherId = await SeedRefTestAsync(database, titleId);

        Guid otherJobId;
        await using (var context = database.CreateContext())
        {
            var otherJob = JobFor(otherId);
            context.Jobs.Add(otherJob);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            otherJobId = otherJob.Id;
        }

        await EraseAsync(database, id, ErasureInitiator.Operator);

        await using var after = database.CreateContext();
        var untouched = await after.Jobs.SingleAsync(j => j.Id == otherJobId, TestContext.Current.CancellationToken);

        Assert.Equal(JobStatus.Pending, untouched.Status);
    }

    /// <summary>
    /// A completed job is history. Cancelling it would rewrite the record of an email that really
    /// was sent; its payload is cleared later by audit retention instead.
    /// </summary>
    [Fact]
    public async Task ErasureLeavesAlreadyCompletedJobsAlone()
    {
        var (database, titleId) = await SeedTitleAsync();
        using var _ = database;
        var id = await SeedRefTestAsync(database, titleId);

        Guid jobId;
        await using (var context = database.CreateContext())
        {
            var job = JobFor(id);
            job.MarkAsProcessing(TimeSpan.FromMinutes(5));
            job.MarkAsCompleted();
            context.Jobs.Add(job);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            jobId = job.Id;
        }

        await EraseAsync(database, id, ErasureInitiator.Operator);

        await using var after = database.CreateContext();
        var completed = await after.Jobs.SingleAsync(j => j.Id == jobId, TestContext.Current.CancellationToken);

        Assert.Equal(JobStatus.Completed, completed.Status);
    }

    /// <summary>
    /// Audit rows carry no foreign key to the RefTest, so nothing cascades. If this redaction were
    /// skipped the participant's name and email would simply stay in the audit table.
    /// </summary>
    [Fact]
    public async Task ErasureRedactsPersonalDataInTheAuditTrail()
    {
        var (database, titleId) = await SeedTitleAsync();
        using var _ = database;
        var id = await SeedRefTestAsync(database, titleId);

        await using (var context = database.CreateContext())
        {
            context.AuditEvents.Add(AuditEventFor(id, "RefTestCreated"));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await EraseAsync(database, id, ErasureInitiator.Operator);

        await using var after = database.CreateContext();
        var audit = await after.AuditEvents.SingleAsync(
            e => e.StreamId == id.ToString(),
            TestContext.Current.CancellationToken);

        Assert.DoesNotContain(ParticipantEmail, audit.Data!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ParticipantFirstName, audit.Data!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Another participant's trail must not be touched. StreamId is the only thing scoping this.
    /// </summary>
    [Fact]
    public async Task ErasureLeavesOtherParticipantsAuditTrailsAlone()
    {
        var (database, titleId) = await SeedTitleAsync();
        using var _ = database;
        var id = await SeedRefTestAsync(database, titleId);
        var otherId = await SeedRefTestAsync(database, titleId);

        await using (var context = database.CreateContext())
        {
            context.AuditEvents.Add(AuditEventFor(otherId, "RefTestCreated"));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await EraseAsync(database, id, ErasureInitiator.Operator);

        await using var after = database.CreateContext();
        var untouched = await after.AuditEvents.SingleAsync(
            e => e.StreamId == otherId.ToString(),
            TestContext.Current.CancellationToken);

        Assert.Contains(ParticipantEmail, untouched.Data!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Participant-triggered events are attributed to the participant's own identity, so the actor
    /// columns are personal data and have to go with everything else.
    /// </summary>
    [Theory]
    [InlineData(DomainEvents.RefTestStartedEvent.EventType)]
    [InlineData(DomainEvents.RefTestPrivacyNoticeAcceptedEvent.EventType)]
    [InlineData(DomainEvents.RefTestCompletedEvent.EventType)]
    public async Task ErasureRedactsTheActorOnParticipantAttributedEvents(string eventType)
    {
        var (database, titleId) = await SeedTitleAsync();
        using var _ = database;
        var id = await SeedRefTestAsync(database, titleId);

        await using (var context = database.CreateContext())
        {
            context.AuditEvents.Add(AuditEventFor(id, eventType));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await EraseAsync(database, id, ErasureInitiator.Operator);

        await using var after = database.CreateContext();
        var audit = await after.AuditEvents.SingleAsync(
            e => e.StreamId == id.ToString(),
            TestContext.Current.CancellationToken);

        Assert.Equal(AuditPiiRedactor.RedactedValue, audit.ActorName);
        Assert.Equal(AuditPiiRedactor.RedactedValue, audit.ActorEmail);
    }

    /// <summary>
    /// A staff-attributed event's actor is accountability evidence, not the participant's data.
    /// Redacting it would erase who did what — the opposite of what an audit trail is for.
    /// </summary>
    [Fact]
    public async Task ErasureKeepsTheActorOnStaffAttributedEvents()
    {
        var (database, titleId) = await SeedTitleAsync();
        using var _ = database;
        var id = await SeedRefTestAsync(database, titleId);

        await using (var context = database.CreateContext())
        {
            context.AuditEvents.Add(new AuditEvent
            {
                StreamId = id.ToString(),
                Type = "RefTestDetailsUpdated",
                Version = 1,
                Data = """{"firstName":{"old":"Ada","new":"Grace"}}""",
                ActorName = "Registrar",
                ActorEmail = "registrar@handball.example"
            });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await EraseAsync(database, id, ErasureInitiator.Operator);

        await using var after = database.CreateContext();
        var audit = await after.AuditEvents.SingleAsync(
            e => e.StreamId == id.ToString(),
            TestContext.Current.CancellationToken);

        Assert.Equal("Registrar", audit.ActorName);
        Assert.Equal("registrar@handball.example", audit.ActorEmail);

        // The payload is still personal data even when the actor is not.
        Assert.DoesNotContain("Ada", audit.Data!, StringComparison.Ordinal);
    }

    /// <summary>
    /// The rule the whole <see cref="ErasureInitiator"/> enum exists for, asserted from both sides.
    ///
    /// When a participant withdraws consent the anonymization event is attributed to them — by the
    /// time it is written their name has already been replaced in memory, so the recorded actor is
    /// the erased placeholder and must be redacted like any other participant-attributed event.
    /// When staff or the retention sweep erases, the same event's actor is the administrator or
    /// "System", and redacting it would destroy the only record of who performed the erasure.
    /// </summary>
    [Theory]
    [InlineData(ErasureInitiator.Participant, true)]
    [InlineData(ErasureInitiator.Operator, false)]
    public async Task TheAnonymizationEventsActorFollowsWhoAskedForTheErasure(
        ErasureInitiator initiator,
        bool expectRedacted)
    {
        var (database, titleId) = await SeedTitleAsync();
        using var _ = database;
        var id = await SeedRefTestAsync(database, titleId);

        await using (var context = database.CreateContext())
        {
            context.AuditEvents.Add(new AuditEvent
            {
                StreamId = id.ToString(),
                Type = DomainEvents.RefTestAnonymizedEvent.EventType,
                Version = 1,
                Data = "{}",
                ActorName = "System",
                ActorEmail = "system@handball.example"
            });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await EraseAsync(database, id, initiator);

        await using var after = database.CreateContext();
        var audit = await after.AuditEvents.SingleAsync(
            e => e.StreamId == id.ToString() && e.Type == DomainEvents.RefTestAnonymizedEvent.EventType,
            TestContext.Current.CancellationToken);

        if (expectRedacted)
        {
            Assert.Equal(AuditPiiRedactor.RedactedValue, audit.ActorName);
            Assert.Equal(AuditPiiRedactor.RedactedValue, audit.ActorEmail);
        }
        else
        {
            Assert.Equal("System", audit.ActorName);
            Assert.Equal("system@handball.example", audit.ActorEmail);
        }
    }

    /// <summary>
    /// The staff delete path. Erasing first is not optional: audit rows have no FK to the RefTest
    /// and are not removed with it, so deleting the row alone would leave the participant's name
    /// and email in the audit trail with the record that explained them now gone.
    /// </summary>
    [Fact]
    public async Task DeletingRemovesTheRowAndStillRedactsTheAuditTrail()
    {
        var (database, titleId) = await SeedTitleAsync();
        using var _ = database;
        var id = await SeedRefTestAsync(database, titleId);

        await using (var context = database.CreateContext())
        {
            context.AuditEvents.Add(AuditEventFor(id, "RefTestCreated"));
            context.Jobs.Add(JobFor(id));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var context = database.CreateContext())
        {
            var refTest = await context.RefTests.SingleAsync(rt => rt.Id == id, TestContext.Current.CancellationToken);
            await new RefTestPrivacyErasureService(context).EraseAndDeleteAsync(
                refTest,
                ErasureInitiator.Operator,
                TestContext.Current.CancellationToken);
        }

        await using var after = database.CreateContext();

        Assert.False(await after.RefTests.AnyAsync(rt => rt.Id == id, TestContext.Current.CancellationToken));

        var audit = await after.AuditEvents.SingleAsync(
            e => e.StreamId == id.ToString(),
            TestContext.Current.CancellationToken);
        Assert.DoesNotContain(ParticipantEmail, audit.Data!, StringComparison.OrdinalIgnoreCase);

        var job = await after.Jobs.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(JobStatus.Cancelled, job.Status);
    }

    /// <summary>
    /// Deleting an already-erased record still has to remove the row. The erase half is skipped,
    /// and an early return there would leave the record undeletable.
    /// </summary>
    [Fact]
    public async Task DeletingAnAlreadyErasedRecordStillRemovesIt()
    {
        var (database, titleId) = await SeedTitleAsync();
        using var _ = database;
        var id = await SeedRefTestAsync(database, titleId);

        await EraseAsync(database, id, ErasureInitiator.Operator);

        await using (var context = database.CreateContext())
        {
            var refTest = await context.RefTests.SingleAsync(rt => rt.Id == id, TestContext.Current.CancellationToken);
            await new RefTestPrivacyErasureService(context).EraseAndDeleteAsync(
                refTest,
                ErasureInitiator.Operator,
                TestContext.Current.CancellationToken);
        }

        await using var after = database.CreateContext();
        Assert.False(await after.RefTests.AnyAsync(rt => rt.Id == id, TestContext.Current.CancellationToken));
    }
}
