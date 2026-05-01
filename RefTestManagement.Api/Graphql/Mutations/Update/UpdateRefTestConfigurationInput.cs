using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Shared;
using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Update;

public record UpdateRefTestConfigurationInput(
    [property: ID<RefTestDto>] Guid Id,
    Title Title,
    int NumberOfQuestions,
    int MaxTimeInMinutes,
    List<string>? SpecificQuestionNumbers = null,
    bool RandomQuestions = false
);


