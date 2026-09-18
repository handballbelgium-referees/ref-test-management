using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Lifecycle;

/// <summary>
/// Bounds checks for the mutations a participant can call with nothing but an invitation token.
/// These are the only unauthenticated write paths in the API, so their input is entirely
/// attacker-controlled and has to be constrained before it reaches the database.
/// </summary>
/// <remarks>
/// Every violation is raised as <see cref="RefTestValidationException"/> so it surfaces as a typed
/// GraphQL error rather than an unhandled exception, which would otherwise return a bare
/// "Unexpected Execution Error" and log a stack trace for input a caller can trivially replay.
/// </remarks>
internal static class ParticipantInput
{
    /// <summary>Matches the <c>Token</c> column width.</summary>
    private const int MaxTokenLength = 50;

    /// <summary>
    /// Answer and question id lists are persisted as a single comma-joined string in a 4000
    /// character column, so the joined value — not just the element count — is what has to fit.
    /// </summary>
    private const int MaxJoinedIdsLength = 4000;

    private const int MaxAnswerIdLength = 64;

    private const int MaxLanguageLength = 16;

    /// <summary>
    /// Validates a raw invitation token before it is used to look up a RefTest. Rejecting
    /// over-long tokens here keeps them from reaching a provider that would otherwise error on a
    /// value wider than the column.
    /// </summary>
    internal static string Token(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new RefTestValidationException("A token is required.");

        if (token.Length > MaxTokenLength)
            throw new RefTestValidationException("The token is not valid.");

        return token;
    }

    /// <summary>
    /// Validates the selected answers. Ids may not contain a comma: the list round-trips through
    /// a comma-joined column, so an embedded comma would silently split one answer into two.
    /// </summary>
    internal static List<string> AnswerIds(List<string>? selectedAnswerIds)
    {
        if (selectedAnswerIds is null || selectedAnswerIds.Count == 0)
            return [];

        var joinedLength = selectedAnswerIds.Count - 1;

        foreach (var answerId in selectedAnswerIds)
        {
            if (string.IsNullOrWhiteSpace(answerId))
                throw new RefTestValidationException("An answer id may not be empty.");

            if (answerId.Length > MaxAnswerIdLength)
                throw new RefTestValidationException("An answer id is not valid.");

            if (answerId.Contains(',', StringComparison.Ordinal))
                throw new RefTestValidationException("An answer id may not contain a comma.");

            joinedLength += answerId.Length;
        }

        if (joinedLength > MaxJoinedIdsLength)
            throw new RefTestValidationException("Too many answers were submitted.");

        return selectedAnswerIds;
    }

    /// <summary>
    /// Validates the position within the test. The index is echoed back to the participant when
    /// they resume, and an out-of-range value would leave them stuck on a question that does not
    /// exist. One past the last question is allowed: that is the review step before submitting.
    /// </summary>
    internal static int QuestionIndex(int currentQuestionIndex, RefTest refTest)
    {
        if (currentQuestionIndex < 0 || currentQuestionIndex > refTest.QuestionIds.Count)
            throw new RefTestValidationException("The question index is outside this RefTest.");

        return currentQuestionIndex;
    }

    /// <summary>
    /// Validates the language tag. The column has no explicit width, so without this an arbitrarily
    /// long value would be accepted by some providers and rejected by others.
    /// </summary>
    internal static string? Language(string? language)
    {
        if (language is null)
            return null;

        if (language.Length > MaxLanguageLength)
            throw new RefTestValidationException("The language is not valid.");

        return language;
    }
}
