namespace QuizManagement.Application.Models;

public record ScoreCalculation(int Score, int Total, double Percentage, List<string> WrongQuestionIds, List<string> WrongAnswerIds);
