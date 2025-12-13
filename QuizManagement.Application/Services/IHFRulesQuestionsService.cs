using QuizManagement.Application.Models;
using System.Globalization;
using System.Text.Json;

namespace QuizManagement.Application.Services;

public interface IIhfRulesQuestionsService
{
    Task<List<string>> GetRandomQuestionIdsAsync(int count, CancellationToken cancellationToken = default);

    Task<List<string>> GetQuestionIdsByNumberAsync(List<string> numbers,
        CancellationToken cancellationToken = default);

    Task<List<Question>> GetQuestionsByIdAsync(List<string> ids, bool includeNumber = false,
        bool includeIsCorrect = false,
        bool randomAnswerOrder = true,
        CancellationToken cancellationToken = default);

    Task<List<Question>> SearchQuestionsByNumberAsync(string? number, CancellationToken cancellationToken = default);

    Task<ScoreCalculation> CalculateScoreAsync(List<string> questionIds, List<string> selectedAnswerIds,
        CancellationToken cancellationToken = default);
}

public class IhfRulesQuestionsService(IIHFRulesQuestionsClient client, LanguageConfiguration languageConfiguration)
    : IIhfRulesQuestionsService
{
    public async Task<List<string>> GetRandomQuestionIdsAsync(int count,
        CancellationToken cancellationToken = default)
    {
        var result = await client.GetRandomQuestionIds.ExecuteAsync(count, cancellationToken);
        if (result.Errors.Any())
            throw new Exception(result.Errors[0].Message);
        var nodes = result.Data?.Questions?.Nodes?.OfType<IGetRandomQuestionIds_Questions_Nodes>();
        return nodes is null ? [] : nodes.Select(x => x.Id).ToList();
    }

    public async Task<List<string>> GetQuestionIdsByNumberAsync(List<string> numbers,
        CancellationToken cancellationToken = default)
    {
        var result = await client.GetQuestionsByNumber.ExecuteAsync(numbers.ToList(), cancellationToken);
        if (result.Errors.Any())
            throw new Exception(result.Errors[0].Message);
        var nodes = result.Data?.Questions?.Nodes?.OfType<IGetQuestionsByNumber_Questions_Nodes>();
        
        if (nodes is null)
            return [];
        
        // Convert nodes to dictionary for fast lookup by number (filter out null numbers)
        var questionDict = nodes.Where(x => x.Number != null)
                                .ToDictionary(x => x.Number!, x => x.Id);
        
        // Return question IDs in the same order as the input numbers
        return numbers.Where(number => questionDict.ContainsKey(number))
                     .Select(number => questionDict[number])
                     .ToList();
    }

    public async Task<List<Question>> GetQuestionsByIdAsync(List<string> ids, bool includeNumber = false,
        bool includeIsCorrect = false, bool randomAnswerOrder = true,
        CancellationToken cancellationToken = default)
    {
        var result = await client.GetQuestionsById.ExecuteAsync(ids.ToList(), includeNumber, includeIsCorrect, randomAnswerOrder, cancellationToken);
        if (result.Errors.Any())
            throw new Exception(result.Errors[0].Message);
        var nodes = result.Data?.QuestionsById?.OfType<GetQuestionsById_QuestionsById_Question>();
        
        // Convert nodes to dictionary for fast lookup
        var questionDict = nodes?.ToDictionary(x => x.Id, x =>
        {
            var questionPhrases = x.Translations?.Deserialize<Dictionary<string, string>>() ??
                                  new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(x.Phrase))
                questionPhrases[languageConfiguration.DefaultPhraseLanguage] = x.Phrase;

            var answers = x.Answers?.Nodes?.OfType<IGetQuestionsById_QuestionsById_Answers_Nodes>().Select(a =>
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

            return new Question(x.Id, questionPhrases, answers)
            {
                Number = x.Number ?? string.Empty,
            };
        }) ?? new Dictionary<string, Question>();
        
        // Return questions in the same order as the input IDs
        return ids.Where(id => questionDict.ContainsKey(id))
                  .Select(id => questionDict[id])
                  .ToList();
    }

    public async Task<List<Question>> SearchQuestionsByNumberAsync(string? number,
        CancellationToken cancellationToken = default)
    {
        var result = await client.SearchQuestionsByNumber.ExecuteAsync(number, cancellationToken);
        if (result.Errors.Any())
            throw new Exception(result.Errors[0].Message);
        var nodes = result.Data?.Questions?.Nodes?.OfType<SearchQuestionsByNumber_Questions_Nodes_Question>();
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
        var result =
            await client.CalculateScore.ExecuteAsync(questionIds, selectedAnswerIds,
                cancellationToken);
        if (result.Errors.Any())
            throw new Exception(result.Errors[0].Message);

        if (result.Data?.CalculateScore is null)
            throw new Exception("Failed to calculate score");

        double percentage;
        var percentageString = result.Data.CalculateScore.Percentage;

        if (percentageString != null && percentageString.EndsWith('%'))
        {
            // Remove the '%' sign and parse using invariant culture to ensure decimal point is correctly interpreted
            if (!double.TryParse(percentageString.TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture,
                    out percentage))
            {
                throw new Exception("Invalid percentage format");
            }
        }
        else if (!double.TryParse(percentageString, NumberStyles.Float, CultureInfo.InvariantCulture, out percentage))
        {
            throw new Exception("Invalid percentage format");
        }

        return new ScoreCalculation(
            questionIds.Count - result.Data.CalculateScore.WrongQuestionsIds.Count,
            questionIds.Count,
            percentage,
            result.Data.CalculateScore.WrongQuestionsIds.ToList() ?? [],
            result.Data.CalculateScore.WrongAnswerIds.ToList() ?? []
        );
    }
}