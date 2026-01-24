namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Update;

public record UpdateRefTestConfigurationInput(
    Guid RefTestId,
    Guid TitleId,
    int NumberOfQuestions,
    int MaxTimeInMinutes,
    List<string>? SpecificQuestionNumbers = null,
    bool RandomQuestions = false
);


