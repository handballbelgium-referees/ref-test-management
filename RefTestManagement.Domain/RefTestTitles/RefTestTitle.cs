namespace Handball.Belgium.RefTestManagement.Domain.RefTestTitles;

public class RefTestTitle
{
    private RefTestTitle(string value)
    {
        Value = value;
    }
    
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Value { get; private set; }
    
    public static RefTestTitle Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Title is required", nameof(value));

        return new RefTestTitle(value);
    }
}