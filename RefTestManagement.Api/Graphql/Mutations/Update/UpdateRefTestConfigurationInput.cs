using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Shared;
using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Update;

public record UpdateRefTestConfigurationInput(
    [property: ID<RefTest>] Guid Id,
    Title Title,
    int NumberOfQuestions,
    int MaxTimeInMinutes,
    List<string>? SpecificQuestionNumbers = null,
    bool RandomQuestions = false
);


