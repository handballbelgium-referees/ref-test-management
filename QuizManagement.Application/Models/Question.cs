namespace QuizManagement.Application.Models;

public record Question(string Id, string Phrase, IDictionary<string, string> Translations, List<Answer> Answers);