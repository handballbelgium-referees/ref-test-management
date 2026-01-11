namespace Handball.Belgium.RefTestManagement.Application.Models;

public record ScoreCalculation(
    int QuestionScore,
    int AnswerScore,
    int QuestionTotal,
    int AnswerTotal,
    double Percentage,
    List<string> WrongQuestionIds,
    List<string> WrongAnswerIds);
