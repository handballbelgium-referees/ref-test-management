namespace QuizManagement.Application.Models;

public record ScoreCalculation(int Score, int Total, double Percentage, List<string> WrongQuestionsIds, List<string> WrongAnswerIds);
