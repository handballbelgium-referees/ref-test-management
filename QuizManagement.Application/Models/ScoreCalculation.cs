namespace QuizManagement.Application.Models;

public record ScoreCalculation(int Score, int Total, string[] WrongQuestionsIds, string[] WrongAnswerIds)
{
    public double Percentage => Total > 0 ? (double)Score / Total : 0;
}