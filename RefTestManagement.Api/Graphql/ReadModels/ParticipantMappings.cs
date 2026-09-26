using System.Linq.Expressions;
using Handball.Belgium.RefTestManagement.Domain.Participants;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;

public static class ParticipantMappings
{
    public static readonly Expression<Func<Participant, ParticipantDto>> ToDto = participant => new ParticipantDto
    {
        Id = participant.Id,
        FirstName = participant.FirstName,
        LastName = participant.LastName,
        FullName = participant.FirstName + " " + participant.LastName,
        Email = participant.Email,
        Type = participant.Type,
        Level = participant.Level,
        CreatedAt = participant.CreatedAt,
        UpdatedAt = participant.UpdatedAt
    };
}
