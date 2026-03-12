using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;

public sealed class RefTestDto
{
    public Guid Id { get; init; }

    public RefTestTitleDto? Title { get; init; }

    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string FullName { get; init; }

    public required string Email { get; init; }
    public required string Token { get; init; }

    public bool SendInvitationsAutomatically { get; init; }
    public bool InvitationSent { get; init; }

    public int NumberOfQuestions { get; init; }
    public int MaxTimeInMinutes { get; init; }

    public IReadOnlyList<string> QuestionIds { get; init; } = [];
    public int QuestionTotal { get; init; }

    public DateTime CreatedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }

    public RefTestStatus Status { get; init; }

    public int? CurrentQuestionIndex { get; init; }

    public int? QuestionScore { get; init; }
    public int? AnswerScore { get; init; }
    public int? AnswerTotal { get; init; }
    public double? Percentage { get; init; }

    public IReadOnlyList<string> SelectedAnswerIds { get; init; } = [];
    public IReadOnlyList<string> WrongQuestionIds { get; init; } = [];
    public IReadOnlyList<string> WrongAnswerIds { get; init; } = [];

    public bool SendResultsAutomatically { get; init; }
    public bool ResultsSent { get; init; }

    public string? Language { get; init; }

    public TimeSpan? Duration { get; init; }

    public ApprovalStatus ApprovalStatus { get; init; }
    public DateTime? ApprovedAt { get; init; }
    public string? ApprovedByUserEmail { get; init; }
    public string? RejectionReason { get; init; }
}