using Handball.Belgium.RefTestManagement.Application.Models;

namespace Handball.Belgium.RefTestManagement.Application.Abstractions;

/// <summary>
/// Thrown when a call to the external IHF Rules questions service fails (network, deserialization, or remote GraphQL errors)
/// </summary>
public class RulesQuestionsUnavailableException(string message, Exception innerException)
    : Exception(message, innerException);

public interface IIhfRulesQuestionsService
{
    Task<List<string>> GetRandomQuestionIdsAsync(int count, CancellationToken cancellationToken = default);

    Task<List<string>> GetQuestionIdsByNumberAsync(List<string> numbers,
        CancellationToken cancellationToken = default);

    Task<List<Question>> GetQuestionsByIdAsync(IReadOnlyList<string> ids, bool includeNumber = false,
        bool includeIsCorrect = false,
        bool randomAnswerOrder = true,
        CancellationToken cancellationToken = default);

    Task<List<Question>> SearchQuestionsByNumberAsync(string? number, CancellationToken cancellationToken = default);

    Task<List<Question>> GetQuestionsByNumberAsync(List<string> numbers, CancellationToken cancellationToken = default);

    Task<ScoreCalculation> CalculateScoreAsync(IReadOnlyList<string> questionIds, IReadOnlyList<string> selectedAnswerIds,
        CancellationToken cancellationToken = default);
}
