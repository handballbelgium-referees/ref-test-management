using Handball.Belgium.RefTestManagement.Application.Abstractions;
using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Application.RefTests.Lifecycle;

/// <summary>
/// Bounds checks for participant operations authenticated by an invitation or session token.
/// These are unauthenticated write paths, so their input is attacker-controlled and must be
/// constrained before it reaches persistence.
/// </summary>
public static class ParticipantInput
{
    private const int MaxJoinedIdsLength = 4000;
    private const int MaxAnswerIdLength = 64;
    private const int MaxLanguageLength = 16;

    /// <summary>
    /// Validates a participant credential before lookup. A stored invitation digest is not itself
    /// a credential and therefore cannot be submitted as a token.
    /// </summary>
    public static string Token(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new RefTestValidationException("A token is required.");
        if (!RefTest.IsValidTokenFormat(token) && !RefTestSessionTokenFormat.HasSessionTokenFormat(token))
            throw new RefTestValidationException("The token is not valid.");

        return token;
    }

    /// <summary>
    /// Validates answer ids for their comma-separated persistence format and maximum column width.
    /// </summary>
    public static List<string> AnswerIds(List<string>? selectedAnswerIds)
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
    /// Validates the current question position. One past the last question is allowed for review.
    /// </summary>
    public static int QuestionIndex(int currentQuestionIndex, RefTest refTest)
    {
        if (currentQuestionIndex < 0 || currentQuestionIndex > refTest.QuestionIds.Count)
            throw new RefTestValidationException("The question index is outside this RefTest.");
        return currentQuestionIndex;
    }

    /// <summary>Validates the language tag against the supported persistence width.</summary>
    public static string? Language(string? language)
    {
        if (language is null)
            return null;
        if (language.Length > MaxLanguageLength)
            throw new RefTestValidationException("The language is not valid.");
        return language;
    }
}