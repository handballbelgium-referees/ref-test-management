using Handball.Belgium.RefTestManagement.Domain.Participants;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;

public static class ParticipantExtensions
{
    public static ParticipantDto ToDto(this Participant participant)
    {
        return new ParticipantDto
        {
            Id = participant.Id,
            FirstName = participant.FirstName,
            LastName = participant.LastName,
            FullName = participant.FullName,
            Email = participant.Email,
            Type = participant.Type,
            Level = participant.Level,
            CreatedAt = participant.CreatedAt,
            UpdatedAt = participant.UpdatedAt
        };
    }
}
