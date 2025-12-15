using QuizManagement.Application.Models;

namespace QuizManagement.Infrastructure.Services;

public interface IQuizResultsPdfService
{
    byte[] GenerateQuizResultsPdf(
        string name,
        string language, 
        int totalQuestions, 
        List<string> selectedAnswerIds, 
        List<string> wrongQuestionIds,
        List<string> wrongAnswerIds,
        List<Question> questionsWithCorrectAnswers);
}

