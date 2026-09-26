namespace Handball.Belgium.RefTestManagement.Domain.Participants;

public sealed class ParticipantNotFoundException : Exception
{
    public ParticipantNotFoundException(Guid id)
        : base($"Participant with ID '{id}' not found")
    {
    }
}
