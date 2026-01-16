namespace Handball.Belgium.RefTestManagement.Application.Models;

public record Answer(string Id, IDictionary<string, string> Phrase)
{
    public string? Number { get; init; }
    public bool IsCorrect { get; init; }
}