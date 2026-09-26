using Handball.Belgium.RefTestManagement.Domain.Participants;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;

public sealed class ParticipantDto
{
    public Guid Id { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string FullName { get; init; }
    public required string Email { get; init; }
    public ParticipantType Type { get; init; }
    public ParticipantLevel? Level { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
