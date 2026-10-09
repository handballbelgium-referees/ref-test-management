using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Application.RefTests.Lifecycle;

public sealed record CompleteRefTestCommand(
    string Token,
    List<string> SelectedAnswerIds,
    string? Language,
    RefTestCompletionSource Source = RefTestCompletionSource.Participant);
