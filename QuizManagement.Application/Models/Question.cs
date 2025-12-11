namespace QuizManagement.Application.Models;

public record Question(string Id, IDictionary<string, string> Phrase, List<Answer> Answers)
{
    public string Number { get; set; } = string.Empty;
}