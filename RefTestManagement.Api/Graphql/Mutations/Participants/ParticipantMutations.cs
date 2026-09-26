using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Domain.Participants;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Handball.Belgium.RefTestManagement.Security;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Participants;

[MutationType]
public static class ParticipantMutations
{
    [Authorize(Policy = Permissions.Participants.Create)]
    public static async Task<ParticipantDto> CreateParticipantAsync(
        CreateParticipantInput input,
        RefTestManagementContext context,
        CancellationToken cancellationToken)
    {
        var participant = Participant.Create(
            input.Participant.FirstName,
            input.Participant.LastName,
            input.Participant.Email,
            input.Participant.Type,
            input.Participant.Level);

        await EnsureUniqueEmailAsync(participant.Email, null, context, cancellationToken);

        context.Participants.Add(participant);
        await context.SaveChangesWithRetryAsync(cancellationToken);

        return participant.ToDto();
    }

    [Authorize(Policy = Permissions.Participants.Update)]
    [Error<ParticipantNotFoundException>]
    public static async Task<ParticipantDto> UpdateParticipantAsync(
        UpdateParticipantInput input,
        RefTestManagementContext context,
        CancellationToken cancellationToken)
    {
        var participant = await context.Participants
            .FirstOrDefaultAsync(x => x.Id == input.Id, cancellationToken)
            ?? throw new ParticipantNotFoundException(input.Id);

        participant.Update(
            input.Participant.FirstName,
            input.Participant.LastName,
            input.Participant.Email,
            input.Participant.Type,
            input.Participant.Level);

        await EnsureUniqueEmailAsync(participant.Email, participant.Id, context, cancellationToken);
        await context.SaveChangesWithRetryAsync(cancellationToken);

        return participant.ToDto();
    }

    private static async Task EnsureUniqueEmailAsync(
        string email,
        Guid? currentParticipantId,
        RefTestManagementContext context,
        CancellationToken cancellationToken)
    {
        var emailTaken = await context.Participants
            .AnyAsync(
                participant => participant.Email == email && participant.Id != currentParticipantId,
                cancellationToken);

        if (emailTaken)
            throw new ArgumentException("A participant with this email already exists.", nameof(email));
    }
}
