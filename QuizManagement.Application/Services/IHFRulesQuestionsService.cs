using QuizManagement.Application.Models;
using System.Text.Json;

namespace QuizManagement.Application.Services;

public interface IIhfRulesQuestionsService
{
    Task<List<string>> GetRandomQuestionIdsAsync(int count, CancellationToken cancellationToken = default);

    Task<List<string>> GetQuestionIdsByNumberAsync(IEnumerable<string> numbers,
        CancellationToken cancellationToken = default);

    Task<List<Question>> GetQuestionsByIdAsync(IEnumerable<string> ids,
        CancellationToken cancellationToken = default);

    Task<List<Question>> SearchQuestionsByNumberAsync(string? number, CancellationToken cancellationToken = default);

    Task<ScoreCalculation> CalculateScoreAsync(IEnumerable<string> questionIds, IEnumerable<string> selectedAnswerIds,
        CancellationToken cancellationToken = default);
}

public class IhfRulesQuestionsService(IIHFRulesQuestionsClient client, LanguageConfiguration languageConfiguration) : IIhfRulesQuestionsService
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

    public async Task<List<string>> GetQuestionIdsByNumberAsync(IEnumerable<string> numbers,
        CancellationToken cancellationToken = default)
    {
        var result = await client.GetQuestionsByNumber.ExecuteAsync(numbers.ToList(), cancellationToken);
        if (result.Errors.Any())
            throw new Exception(result.Errors[0].Message);
        var nodes = result.Data?.Questions?.Nodes?.OfType<IGetQuestionsByNumber_Questions_Nodes>();
        return nodes is null ? [] : nodes.Select(x => x.Id).ToList();
    }

    public async Task<List<Question>> GetQuestionsByIdAsync(IEnumerable<string> ids,
        CancellationToken cancellationToken = default)
    {
        var result = await client.GetQuestionsById.ExecuteAsync(ids.ToList(), cancellationToken);
        if (result.Errors.Any())
            throw new Exception(result.Errors[0].Message);
        var nodes = result.Data?.QuestionsById?.OfType<GetQuestionsById_QuestionsById_Question>();
        return nodes?.Select(x =>
        {
            var questionPhrases = x.Translations?.Deserialize<Dictionary<string, string>>() ?? new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(x.Phrase))
                questionPhrases[languageConfiguration.DefaultPhraseLanguage] = x.Phrase;
            
            var answers = x.Answers?.Nodes?.OfType<IGetQuestionsById_QuestionsById_Answers_Nodes>().Select(a =>
            {
                var answerTranslations = a.Translations?.Deserialize<Dictionary<string, string>>() ?? new Dictionary<string, string>();
                if (!string.IsNullOrEmpty(a.Phrase))
                    answerTranslations[languageConfiguration.DefaultPhraseLanguage] = a.Phrase;
                return new Answer(a.Id, answerTranslations);
            }).ToList() ?? [];
            
            return new Question(x.Id, questionPhrases, answers);
        }).ToList() ?? [];
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
            var questionPhrases = x.Translations?.Deserialize<Dictionary<string, string>>() ?? new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(x.Phrase))
                questionPhrases[languageConfiguration.DefaultPhraseLanguage] = x.Phrase;
            
            return new Question(x.Id, questionPhrases, [])
            {
                Number = x.Number ?? string.Empty
            };
        }).ToList() ?? [];
    }

    public async Task<ScoreCalculation> CalculateScoreAsync(IEnumerable<string> questionIds,
        IEnumerable<string> selectedAnswerIds,
        CancellationToken cancellationToken = default)
    {
        var result =
            await client.CalculateScore.ExecuteAsync(questionIds.ToList(), selectedAnswerIds.ToList(),
                cancellationToken);
        if (result.Errors.Any())
            throw new Exception(result.Errors[0].Message);

        if (result.Data?.CalculateScore is null)
            throw new Exception("Failed to calculate score");

        return new ScoreCalculation(
            result.Data.CalculateScore.Score ?? 0,
            result.Data.CalculateScore.Total ?? 0,
            result.Data.CalculateScore.WrongQuestionsIds.ToArray() ?? [],
            result.Data.CalculateScore.WrongAnswerIds.ToArray() ?? []
        );
    }
}