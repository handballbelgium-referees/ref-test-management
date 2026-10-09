using Handball.Belgium.RefTestManagement.Domain.RefTests;
using Handball.Belgium.RefTestManagement.Domain.RefTestTitles;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.UnitTests;

/// <summary>
/// The RefTest ID lists may only change through RefTest's own methods, which validate state and
/// raise domain events. These tests pin that callers can neither mutate the exposed lists nor keep
/// a reference to the list they passed in, and that persistence still round-trips the values.
/// </summary>
public sealed class RefTestCollectionEncapsulationTests
{
    private static RefTest NewRefTest(List<string> questionIds) =>
        RefTest.Create(
            Guid.NewGuid(),
            "Ada",
            "Lovelace",
            "ada@example.org",
            numberOfQuestions: 2,
            maxTimeInMinutes: 30,
            questionIds: questionIds,
            sendInvitationAutomatically: false,
            sendResultsAutomatically: false);

    [Fact]
    public void ExposedListsCannotBeMutated()
    {
        var refTest = NewRefTest(["q1", "q2"]);

        Assert.IsNotType<List<string>>(refTest.QuestionIds);
        var asList = Assert.IsAssignableFrom<IList<string>>(refTest.QuestionIds);
        Assert.Throws<NotSupportedException>(() => asList.Add("q3"));
    }

    [Fact]
    public void ChangingTheCallersListAfterwardsDoesNotChangeTheAggregate()
    {
        List<string> questionIds = ["q1", "q2"];
        List<string> selected = ["a1"];
        List<string> wrongQuestions = ["q2"];
        List<string> wrongAnswers = ["a2"];
        var refTest = NewRefTest(questionIds);
        refTest.AcceptPrivacyNotice("v1");
        refTest.Start("v1");
        refTest.Complete(1, 1, 2, 50, selected, wrongQuestions, wrongAnswers);

        questionIds.Add("q3");
        selected.Clear();
        wrongQuestions.Clear();
        wrongAnswers.Clear();

        Assert.Equal(["q1", "q2"], refTest.QuestionIds);
        Assert.Equal(["a1"], refTest.SelectedAnswerIds);
        Assert.Equal(["q2"], refTest.WrongQuestionIds);
        Assert.Equal(["a2"], refTest.WrongAnswerIds);
    }

    [Fact]
    public async Task ListsRoundTripThroughPersistenceAndReplacementsAreSaved()
    {
        var ct = TestContext.Current.CancellationToken;
        using var database = SqliteTestDatabase.Create();
        var title = RefTestTitle.Create("Season 2026");
        var refTest = RefTest.Create(title.Id, "Ada", "Lovelace", "ada@example.org", 2, 30, ["q1", "q2"],
            sendInvitationAutomatically: false, sendResultsAutomatically: false);
        await using (var context = database.CreateContext())
        {
            context.RefTestTitles.Add(title);
            context.RefTests.Add(refTest);
            await context.SaveChangesAsync(ct);
        }

        await using (var context = database.CreateContext())
        {
            var stored = await context.RefTests.SingleAsync(x => x.Id == refTest.Id, ct);
            Assert.Equal(["q1", "q2"], stored.QuestionIds);

            stored.AcceptPrivacyNotice("v1");
            stored.Start("v1");
            stored.SaveProgress(1, ["a1"]);
            await context.SaveChangesAsync(ct);
        }

        await using (var context = database.CreateContext())
        {
            var stored = await context.RefTests.SingleAsync(x => x.Id == refTest.Id, ct);
            Assert.Equal(["a1"], stored.SelectedAnswerIds);
        }
    }
}
