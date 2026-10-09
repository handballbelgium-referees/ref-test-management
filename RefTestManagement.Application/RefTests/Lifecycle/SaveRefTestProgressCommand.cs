namespace Handball.Belgium.RefTestManagement.Application.RefTests.Lifecycle;

public sealed record SaveRefTestProgressCommand(
    string Token,
    int CurrentQuestionIndex,
    List<string> SelectedAnswerIds,
    string Language);
