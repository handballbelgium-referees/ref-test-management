using System.ComponentModel.DataAnnotations.Schema;

namespace Handball.Belgium.RefTestManagement.Domain.Participants;

public class Participant
{
    private Participant(
        string firstName,
        string lastName,
        string email,
        ParticipantType type,
        ParticipantLevel? level)
    {
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        Type = type;
        Level = level;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public long Version { get; private set; } = 1;
    public string FirstName { get; private set; }
    public string LastName { get; private set; }

    [NotMapped]
    public string FullName => $"{FirstName} {LastName}";

    public string Email { get; private set; }
    public ParticipantType Type { get; private set; }
    public ParticipantLevel? Level { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public static Participant Create(
        string firstName,
        string lastName,
        string email,
        ParticipantType type,
        ParticipantLevel? level)
    {
        var normalizedFirstName = NormalizeRequiredValue(firstName, nameof(firstName), "First name is required");
        var normalizedLastName = NormalizeRequiredValue(lastName, nameof(lastName), "Last name is required");
        var normalizedEmail = NormalizeEmail(email);
        ValidateGrouping(type, level);

        return new Participant(normalizedFirstName, normalizedLastName, normalizedEmail, type, level);
    }

    public void Update(
        string firstName,
        string lastName,
        string email,
        ParticipantType type,
        ParticipantLevel? level)
    {
        FirstName = NormalizeRequiredValue(firstName, nameof(firstName), "First name is required");
        LastName = NormalizeRequiredValue(lastName, nameof(lastName), "Last name is required");
        Email = NormalizeEmail(email);
        ValidateGrouping(type, level);
        Type = type;
        Level = level;
        UpdatedAt = DateTime.UtcNow;
    }

    private static string NormalizeRequiredValue(string value, string paramName, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException(message, paramName);

        return value.Trim();
    }

    private static string NormalizeEmail(string email) =>
        NormalizeRequiredValue(email, nameof(email), "Email is required").ToLowerInvariant();

    private static void ValidateGrouping(ParticipantType type, ParticipantLevel? level)
    {
        if (type == ParticipantType.Referee && level is null)
            throw new ArgumentException("Referees must have a level", nameof(level));

        if (type != ParticipantType.Referee && level is not null)
            throw new ArgumentException("Only referees can have a level", nameof(level));
    }
}
