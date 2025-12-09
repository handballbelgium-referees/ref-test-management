namespace QuizManagement.Application.Models;

public record Answer(string Id, string Phrase, IDictionary<string, string> Translations);