using Handball.Belgium.RefTestManagement.Application.Models;

namespace Handball.Belgium.RefTestManagement.Application.Abstractions;

public interface IRefTestResultsPdfService
{
    Task<byte[]> GenerateRefTestResultsPdfAsync(
        string name,
        string language,
        int questionScore,
        int answerScore,
        int totalQuestions,
        int answerTotal,
        double percentage,
        List<string> selectedAnswerIds,
        List<string> wrongQuestionIds,
        List<string> wrongAnswerIds,
        List<Question> questionsWithCorrectAnswers,
        CancellationToken cancellationToken);
}
