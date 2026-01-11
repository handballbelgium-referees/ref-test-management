namespace Handball.Belgium.RefTestManagement.Application.Models;

public record Question(string Id, IDictionary<string, string> Phrase, List<Answer> Answers)
{
    public string Number { get; set; } = string.Empty;
}