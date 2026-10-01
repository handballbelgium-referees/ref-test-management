using System.Text.Json;
using Handball.Belgium.RefTestManagement.Api.BackgroundServices.JobHandlers;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.Jobs;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;
using Microsoft.Extensions.Logging.Abstractions;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public sealed class InvitationEmailJobHandlerTests
{
    [Fact]
    public async Task SkipsInvitationWhenTheTokenHasBeenRotated()
    {
        var ct = TestContext.Current.CancellationToken;
        using var database = SqliteTestDatabase.Create();
        await using var context = database.CreateContext();

        var title = RefTestTitle.Create("Season opener");
        context.RefTestTitles.Add(title);
        await context.SaveChangesAsync(ct);

        var refTest = RefTest.Create(
            title.Id,
            "Ada",
            "Lovelace",
            "ada@example.org",
            numberOfQuestions: 10,
            maxTimeInMinutes: 30,
            questionIds: ["q1"],
            sendInvitationAutomatically: true,
            sendResultsAutomatically: true);
        var staleToken = refTest.GetIssuedToken();
        refTest.RegenerateToken();

        var payload = new InvitationEmailPayload(
            refTest.Id,
            refTest.FullName,
            refTest.Email,
            staleToken,
            refTest.NumberOfQuestions,
            refTest.MaxTimeInMinutes);
        var job = Job.Create(JobType.InvitationEmail, JsonSerializer.Serialize(payload));
        context.RefTests.Add(refTest);
        context.Jobs.Add(job);
        await context.SaveChangesAsync(ct);

        var handler = new InvitationEmailJobHandler(
            null!,
            context,
            null!,
            NullLogger<InvitationEmailJobHandler>.Instance);

        await handler.HandleAsync(job, ct);

        Assert.Null(refTest.InvitationSentAt);
    }
}
