using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Shared;
using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Creation;

public record CreateRefTestsInput(
    Title Title,
    [property: ID<ParticipantDto>] List<Guid> ParticipantIds,
    List<ParticipantDraft> NewParticipants,
    int NumberOfQuestions,
    bool RandomQuestionsForEachUser,
    int MaxTimeInMinutes,
    List<string>? SpecificQuestionNumbers = null,
    bool SendAutomatedInvitations = false,
    bool SendAutomatedResults = false,
    [GraphQLDescription("Optional date/time from which the ref tests can be started. When SendAutomatedInvitations is true, the invitation email is scheduled for this time.")]
    DateTime? ScheduledAt = null
);