using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Lifecycle;
using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.UnitTests;

/// <summary>
/// Covers the bounds checks on the participant mutations. Those are the only unauthenticated
/// write paths in the API, so everything they receive is attacker-controlled: without these
/// checks an over-long or malformed value reaches the database, where the failure mode is either
/// a provider error surfaced as "Unexpected Execution Error" or, for ids containing a comma,
/// silent data corruption.
/// </summary>
public class ParticipantInputTests
{
    private static RefTest RefTestWithQuestions(params string[] questionIds) =>
        RefTest.Create(
            titleId: Guid.NewGuid(),
            firstName: "John",
            lastName: "Doe",
            email: "john.doe@example.com",
            numberOfQuestions: questionIds.Length,
            maxTimeInMinutes: 30,
            questionIds: [.. questionIds],
            sendInvitationAutomatically: false,
            sendResultsAutomatically: false);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Token_RejectsAMissingToken(string? token) =>
        Assert.Throws<RefTestValidationException>(() => ParticipantInput.Token(token));

    /// <summary>The Token column is 50 characters wide, so anything longer cannot be a real token.</summary>
    [Fact]
    public void Token_RejectsATokenWiderThanTheColumn() =>
        Assert.Throws<RefTestValidationException>(() => ParticipantInput.Token(new string('a', 51)));

    [Fact]
    public void Token_AcceptsAGeneratedToken()
    {
        var token = new string('a', 32);

        Assert.Equal(token, ParticipantInput.Token(token));
    }

    [Fact]
    public void AnswerIds_TreatsNoAnswersAsAnEmptySelection()
    {
        Assert.Empty(ParticipantInput.AnswerIds(null));
        Assert.Empty(ParticipantInput.AnswerIds([]));
    }

    /// <summary>
    /// The selection round-trips through a comma-joined column, so an embedded comma would be
    /// read back as two separate answers — corruption rather than a rejected request.
    /// </summary>
    [Fact]
    public void AnswerIds_RejectsAnIdContainingAComma() =>
        Assert.Throws<RefTestValidationException>(() => ParticipantInput.AnswerIds(["a1,a2"]));

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void AnswerIds_RejectsAnEmptyId(string answerId) =>
        Assert.Throws<RefTestValidationException>(() => ParticipantInput.AnswerIds([answerId]));

    [Fact]
    public void AnswerIds_RejectsAnOverlongId() =>
        Assert.Throws<RefTestValidationException>(
            () => ParticipantInput.AnswerIds([new string('a', 65)]));

    /// <summary>
    /// The 4000 character limit is on the joined value, not the element count: a few hundred
    /// individually valid ids still overflow the column once they are joined.
    /// </summary>
    [Fact]
    public void AnswerIds_RejectsASelectionThatDoesNotFitTheColumnOnceJoined()
    {
        List<string> answerIds = [.. Enumerable.Range(0, 200).Select(_ => new string('a', 64))];

        Assert.Throws<RefTestValidationException>(() => ParticipantInput.AnswerIds(answerIds));
    }

    [Fact]
    public void AnswerIds_AcceptsARealisticSelection()
    {
        List<string> answerIds = ["a1", "a2", "a3"];

        Assert.Equal(answerIds, ParticipantInput.AnswerIds(answerIds));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void QuestionIndex_RejectsAnIndexOutsideTheRefTest(int index)
    {
        var refTest = RefTestWithQuestions("q1", "q2");

        Assert.Throws<RefTestValidationException>(
            () => ParticipantInput.QuestionIndex(index, refTest));
    }

    /// <summary>
    /// One past the last question is the review step before submitting, so it has to be allowed.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void QuestionIndex_AcceptsEveryPositionUpToTheReviewStep(int index)
    {
        var refTest = RefTestWithQuestions("q1", "q2");

        Assert.Equal(index, ParticipantInput.QuestionIndex(index, refTest));
    }

    [Fact]
    public void Language_AcceptsNoLanguage() => Assert.Null(ParticipantInput.Language(null));

    [Fact]
    public void Language_AcceptsALanguageTag() => Assert.Equal("nl-BE", ParticipantInput.Language("nl-BE"));

    /// <summary>The Language column has no configured width, so the check is the only bound.</summary>
    [Fact]
    public void Language_RejectsAnOverlongLanguage() =>
        Assert.Throws<RefTestValidationException>(
            () => ParticipantInput.Language(new string('n', 17)));
}
