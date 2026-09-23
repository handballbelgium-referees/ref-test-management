using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;

public sealed class ParticipantRefTestDto
{
    public Guid Id { get; init; }

    public required string Name { get; init; }
    public required string Email { get; init; }

    public int NumberOfQuestions { get; init; }
    public int MaxTimeInMinutes { get; init; }
    public IReadOnlyList<string> QuestionIds { get; init; } = [];

    public DateTime? StartedAt { get; init; }
    public RefTestStatus Status { get; init; }
    public int? CurrentQuestionIndex { get; init; }
    public IReadOnlyList<string> SelectedAnswerIds { get; init; } = [];

    public int QuestionTotal { get; init; }
    public int? QuestionScore { get; init; }
    public int? AnswerScore { get; init; }
    public int? AnswerTotal { get; init; }
    public double? Percentage { get; init; }

    public bool SendResultsAutomatically { get; init; }
    public bool ResultsSent { get; init; }
}

public sealed class ParticipantQuestionDto
{
    public required string Id { get; init; }
    public string? Number { get; init; }
    public required IDictionary<string, string> Phrase { get; init; }
    public IReadOnlyList<ParticipantAnswerDto> Answers { get; init; } = [];
}

public sealed class ParticipantAnswerDto
{
    public required string Id { get; init; }
    public string? Number { get; init; }
    public required IDictionary<string, string> Phrase { get; init; }
    public bool IsCorrect { get; init; }
}
