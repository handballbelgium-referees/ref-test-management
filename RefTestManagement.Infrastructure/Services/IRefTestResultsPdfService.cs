using Handball.Belgium.RefTestManagement.Application.Models;

namespace Handball.Belgium.RefTestManagement.Infrastructure.Services;

public interface IRefTestResultsPdfService
{
    byte[] GenerateRefTestResultsPdf(
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
        List<Question> questionsWithCorrectAnswers);
}