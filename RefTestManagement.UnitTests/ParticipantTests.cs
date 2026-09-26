using Handball.Belgium.RefTestManagement.Domain.Participants;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public class ParticipantTests
{
    [Fact]
    public void Create_RequiresARefereeLevelForReferees() =>
        Assert.Throws<ArgumentException>(
            () => Participant.Create("Jane", "Doe", "jane@example.com", ParticipantType.Referee, null));

    [Fact]
    public void Create_RejectsLevelsForNonReferees() =>
        Assert.Throws<ArgumentException>(
            () => Participant.Create("Jane", "Doe", "jane@example.com", ParticipantType.Delegate, ParticipantLevel.Elite));

    [Fact]
    public void Create_NormalizesEmailAndKeepsARefereeLevel()
    {
        var participant = Participant.Create(
            "Jane",
            "Doe",
            " Jane.Doe@Example.com ",
            ParticipantType.Referee,
            ParticipantLevel.NationalPlus);

        Assert.Equal("jane.doe@example.com", participant.Email);
        Assert.Equal(ParticipantLevel.NationalPlus, participant.Level);
    }

    [Fact]
    public void Update_ClearsTheLevelWhenSwitchingAwayFromReferee()
    {
        var participant = Participant.Create(
            "Jane",
            "Doe",
            "jane@example.com",
            ParticipantType.Referee,
            ParticipantLevel.Elite);

        participant.Update("Jane", "Doe", "jane@example.com", ParticipantType.Other, null);

        Assert.Equal(ParticipantType.Other, participant.Type);
        Assert.Null(participant.Level);
    }
}
