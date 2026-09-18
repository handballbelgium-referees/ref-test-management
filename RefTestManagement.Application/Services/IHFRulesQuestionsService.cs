using System.Text.Json;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Application.Models;

namespace Handball.Belgium.RefTestManagement.Application.Services;

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

    Task<ScoreCalculation> CalculateScoreAsync(List<string> questionIds, List<string> selectedAnswerIds,
        CancellationToken cancellationToken = default);
}

public class IhfRulesQuestionsService(
    IIHFRulesQuestionsClient client,
    LanguageConfiguration languageConfiguration,
    ScoreConfiguration scoreConfiguration)
    : IIhfRulesQuestionsService
{
    public async Task<List<string>> GetRandomQuestionIdsAsync(int count,
        CancellationToken cancellationToken = default)
    {
        var result = await client.GetRandomQuestionIds.ExecuteAsync(count, cancellationToken);
        if (result.Errors.Any())
            throw new Exception(result.Errors[0].Message);
        var nodes = result.Data?.Questions.Nodes?.OfType<IGetRandomQuestionIds_Questions_Nodes>();
        return nodes is null ? [] : [.. nodes.Select(x => x.Id)];
    }

    public async Task<List<string>> GetQuestionIdsByNumberAsync(List<string> numbers,
        CancellationToken cancellationToken = default)
    {
        var result = await client.GetQuestionsByNumber.ExecuteAsync([.. numbers], cancellationToken);
        if (result.Errors.Any())
            throw new Exception(result.Errors[0].Message);
        var nodes = result.Data?.QuestionsByNumber.OfType<IGetQuestionsByNumber_QuestionsByNumber_Question>();

        if (nodes is null)
            return [];

        // Convert nodes to dictionary for a fast lookup by number (filter out null numbers)
        var questionDict = nodes
            .ToDictionary(x => x.Number, x => x.Id);

        // Return question IDs in the same order as the input numbers
        return
        [
            .. numbers.Where(questionDict.ContainsKey)
                .Select(number => questionDict[number])
        ];
    }

    public async Task<List<Question>> GetQuestionsByIdAsync(IReadOnlyList<string> ids, bool includeNumber = false,
        bool includeIsCorrect = false, bool randomAnswerOrder = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // NOTE: The IHF Rules Questions API requires an explicit OrderBy key for cursor pagination
            // (see api-ihf-rules-questions QuestionNode.GetAnswers). Passing a null order (as previously
            // done when randomAnswerOrder was true) causes an "Unexpected Execution Error" from the
            // remote service. We always request a deterministic order and shuffle client-side instead.
            var result = await client.GetQuestionsById.ExecuteAsync([.. ids], includeNumber, includeIsCorrect,
                [
                    new AnswerSortInput
                    {
                        Number = SortEnumType.Asc
                    }
                ], cancellationToken);
            if (result.Errors.Any())
                throw new Exception(result.Errors[0].Message);
            var nodes = result.Data?.QuestionsById.OfType<GetQuestionsById_QuestionsById_Question>();

            // Convert nodes to dictionary for fast lookup
            var questionDict = nodes?.ToDictionary(x => x.Id, x =>
            {
                var questionPhrases = x.Translations?.Deserialize<Dictionary<string, string>>() ??
                                      new Dictionary<string, string>();
                if (!string.IsNullOrEmpty(x.Phrase))
                    questionPhrases[languageConfiguration.DefaultPhraseLanguage] = x.Phrase;

                var answers = x.Answers.Nodes?.OfType<IGetQuestionsById_QuestionsById_Answers_Nodes>().Select(a =>
                {
                    var answerTranslations = a.Translations?.Deserialize<Dictionary<string, string>>() ??
                                             new Dictionary<string, string>();
                    if (!string.IsNullOrEmpty(a.Phrase))
                        answerTranslations[languageConfiguration.DefaultPhraseLanguage] = a.Phrase;
                    return new Answer(a.Id, answerTranslations)
                    {
                        Number = a.Number,
                        IsCorrect = a.IsCorrect ?? false
                    };
                }).ToList() ?? [];

                // The remote service always returns answers ordered by number (see the deterministic
                // order requested above). Shuffle client-side when random order was requested instead
                // of relying on the remote API to randomize, since that path triggers a paging bug.
                if (randomAnswerOrder)
                    answers = [.. answers.OrderBy(_ => Random.Shared.Next())];

                return new Question(x.Id, questionPhrases, answers)
                {
                    Number = x.Number ?? string.Empty,
                };
            }) ?? new Dictionary<string, Question>();

            // Return questions in the same order as the input IDs
            return
            [
                .. ids.Where(questionDict.ContainsKey)
                    .Select(id => questionDict[id])
            ];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new RulesQuestionsUnavailableException(
                "Failed to load questions from the IHF Rules service.", ex);
        }
    }

    public async Task<List<Question>> SearchQuestionsByNumberAsync(string? number,
        CancellationToken cancellationToken = default)
    {
        var result = await client.SearchQuestionsByNumber.ExecuteAsync(number, cancellationToken);
        if (result.Errors.Any())
            throw new Exception(result.Errors[0].Message);
        var nodes = result.Data?.Questions.Nodes?.OfType<SearchQuestionsByNumber_Questions_Nodes_Question>();
        return nodes?.Select(x =>
        {
            var questionPhrases = x.Translations?.Deserialize<Dictionary<string, string>>() ??
                                  new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(x.Phrase))
                questionPhrases[languageConfiguration.DefaultPhraseLanguage] = x.Phrase;

            return new Question(x.Id, questionPhrases, [])
            {
                Number = x.Number ?? string.Empty
            };
        }).ToList() ?? [];
    }

    public async Task<List<Question>> GetQuestionsByNumberAsync(List<string> numbers,
        CancellationToken cancellationToken = default)
    {
        var result = await client.GetQuestionsByNumbers.ExecuteAsync(numbers, cancellationToken);
        if (result.Errors.Any())
            throw new Exception(result.Errors[0].Message);
        var nodes = result.Data?.QuestionsByNumber.OfType<GetQuestionsByNumbers_QuestionsByNumber_Question>();
        return nodes?.Select(x =>
        {
            var questionPhrases = x.Translations?.Deserialize<Dictionary<string, string>>() ??
                                  new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(x.Phrase))
                questionPhrases[languageConfiguration.DefaultPhraseLanguage] = x.Phrase;

            return new Question(x.Id, questionPhrases, [])
            {
                Number = x.Number ?? string.Empty
            };
        }).ToList() ?? [];
    }

    public async Task<ScoreCalculation> CalculateScoreAsync(List<string> questionIds,
        List<string> selectedAnswerIds,
        CancellationToken cancellationToken = default)
    {
        var scoreConfigInput = new ScoreConfigurationInput
        {
            Correct = scoreConfiguration.Correct,
            InCorrect = scoreConfiguration.InCorrect,
            NotAnswered = scoreConfiguration.NotAnswered,
            NegativeScore = scoreConfiguration.NegativeScore,
            PenalizeGuessingStrategy = scoreConfiguration.PenalizeGuessingStrategy
        };

        var result =
            await client.CalculateScore.ExecuteAsync(questionIds, selectedAnswerIds, scoreConfigInput,
                cancellationToken);
        if (result.Errors.Any())
            throw new Exception(result.Errors[0].Message);

        if (result.Data?.CalculateScore is null)
            throw new Exception("Failed to calculate score");

        var scoreData = result.Data.CalculateScore;

        var percentage = ScorePercentage.Parse(scoreData.Percentage);

        // Calculate a question-based score (number of fully correct questions)
        var questionScore = questionIds.Count(id => !scoreData.WrongQuestionsIds.Contains(id));

        // Get an answer-based score from API (based on correct +1, incorrect -1, not answered 0)
        var answerScore = scoreData.Score;

        return new ScoreCalculation(
            questionScore,
            answerScore,
            questionIds.Count,
            scoreData.Total,
            percentage,
            scoreData.WrongQuestionsIds.ToList() ?? [],
            scoreData.WrongAnswerIds.ToList() ?? []
        );
    }
}
