using System.Reflection;
using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.UnitTests;

/// <summary>
/// Covers the server-side time limit. Status alone never proved a test was still open: overdue
/// tests stay <see cref="RefTestStatus.InProgress"/> until the expiration service next sweeps,
/// which left a window in which a participant could keep answering past their deadline.
/// </summary>
public class RefTestDeadlineTests
{
    private const int MaxTimeInMinutes = 30;

    private static RefTest StartedRefTest(int maxTimeInMinutes = MaxTimeInMinutes)
    {
        var refTest = RefTest.Create(
            titleId: Guid.NewGuid(),
            firstName: "John",
            lastName: "Doe",
            email: "john.doe@example.com",
            numberOfQuestions: 20,
            maxTimeInMinutes: maxTimeInMinutes,
            questionIds: ["q1", "q2"],
            sendInvitationAutomatically: false,
            sendResultsAutomatically: false);

        refTest.AcceptPrivacyNotice("v1");
        refTest.Start("v1");
        return refTest;
    }

    /// <summary>
    /// <see cref="RefTest.StartedAt"/> is written by the domain, so a test that needs a RefTest
    /// which started in the past has to reach past the private setter EF itself uses.
    /// </summary>
    private static void BackdateStart(RefTest refTest, TimeSpan by) =>
        typeof(RefTest)
            .GetProperty(nameof(RefTest.StartedAt), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(refTest, refTest.StartedAt!.Value.Subtract(by));

    private static void Complete(RefTest refTest, RefTestCompletionSource source) =>
        refTest.Complete(
            questionScore: 10,
            answerScore: 20,
            answerTotal: 40,
            percentage: 50,
            selectedAnswerIds: ["a1"],
            wrongQuestionIds: [],
            wrongAnswerIds: [],
            language: "en",
            source: source);

    [Fact]
    public void SaveProgress_IsAllowedBeforeTheDeadline()
    {
        var refTest = StartedRefTest();
        BackdateStart(refTest, TimeSpan.FromMinutes(MaxTimeInMinutes - 1));

        refTest.SaveProgress(3, ["a1"]);

        Assert.Equal(3, refTest.CurrentQuestionIndex);
    }

    [Fact]
    public void SaveProgress_IsAllowedInsideTheLatencyGrace()
    {
        // A submission sent just before the deadline must not be rejected for arriving just after.
        var refTest = StartedRefTest();
        BackdateStart(refTest, TimeSpan.FromMinutes(MaxTimeInMinutes).Add(TimeSpan.FromSeconds(5)));

        refTest.SaveProgress(3, ["a1"]);

        Assert.Equal(3, refTest.CurrentQuestionIndex);
    }

    [Fact]
    public void SaveProgress_IsRejectedAfterTheDeadline()
    {
        var refTest = StartedRefTest();
        BackdateStart(refTest, TimeSpan.FromMinutes(MaxTimeInMinutes + 5));

        Assert.Throws<InvalidRefTestStatusException>(() => refTest.SaveProgress(3, ["a1"]));
        Assert.NotEqual(3, refTest.CurrentQuestionIndex);
    }

    [Fact]
    public void Complete_IsRejectedAfterTheDeadlineForTheParticipant()
    {
        var refTest = StartedRefTest();
        BackdateStart(refTest, TimeSpan.FromMinutes(MaxTimeInMinutes + 5));

        Assert.Throws<InvalidRefTestStatusException>(
            () => Complete(refTest, RefTestCompletionSource.Participant));
        Assert.Equal(RefTestStatus.InProgress, refTest.Status);
    }

    [Fact]
    public void Complete_IsAllowedAfterTheDeadlineForTheExpirationService()
    {
        // The expiration service exists to close overdue tests. Enforcing the deadline against it
        // would make every overdue test permanently unfinishable.
        var refTest = StartedRefTest();
        BackdateStart(refTest, TimeSpan.FromMinutes(MaxTimeInMinutes + 5));

        Complete(refTest, RefTestCompletionSource.ExpirationService);

        Assert.Equal(RefTestStatus.Completed, refTest.Status);
    }

    [Fact]
    public void HasPassedDeadline_FollowsAnAdministratorsTimeExtension()
    {
        var refTest = StartedRefTest();
        BackdateStart(refTest, TimeSpan.FromMinutes(MaxTimeInMinutes + 5));
        Assert.True(refTest.HasPassedDeadline());

        refTest.ExtendTime(15);

        Assert.False(refTest.HasPassedDeadline());
        refTest.SaveProgress(4, ["a1"]);
        Assert.Equal(4, refTest.CurrentQuestionIndex);
    }

    [Fact]
    public void HasPassedDeadline_IsFalseForATestThatNeverStarted()
    {
        var refTest = RefTest.Create(
            titleId: Guid.NewGuid(),
            firstName: "John",
            lastName: "Doe",
            email: "john.doe@example.com",
            numberOfQuestions: 20,
            maxTimeInMinutes: MaxTimeInMinutes,
            questionIds: ["q1"],
            sendInvitationAutomatically: false,
            sendResultsAutomatically: false);

        Assert.False(refTest.HasPassedDeadline());
    }
}
