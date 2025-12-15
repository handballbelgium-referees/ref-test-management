namespace Handball.Belgium.Rules.Quiz.Domain;

public class QuizTitle
{
    private QuizTitle(string value)
    {
        Value = value;
    }
    
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Value { get; private set; }
    
    public static QuizTitle Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Title is required", nameof(value));

        return new QuizTitle(value);
    }
}