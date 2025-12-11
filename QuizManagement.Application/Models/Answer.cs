namespace QuizManagement.Application.Models;

public record Answer(string Id, IDictionary<string, string> Phrase);