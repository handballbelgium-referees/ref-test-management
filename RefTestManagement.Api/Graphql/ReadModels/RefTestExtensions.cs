using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;

public static class RefTestExtensions
{
    public static RefTestDto ToDto(this RefTest refTest)
    {
        return new RefTestDto
        {
            Id = refTest.Id,
            Title = refTest.Title is null
                ? null
                : new RefTestTitleDto
                {
                    Id = refTest.Title.Id,
                    Value = refTest.Title.Value
                },
            FirstName = refTest.FirstName,
            LastName = refTest.LastName,
            FullName = $"{refTest.FirstName} {refTest.LastName}",
            Email = refTest.Email,
            Token = refTest.Token,
            SendInvitationsAutomatically = refTest.SendInvitationsAutomatically,
            InvitationSent = refTest.InvitationSentAt.HasValue,
            NumberOfQuestions = refTest.NumberOfQuestions,
            MaxTimeInMinutes = refTest.MaxTimeInMinutes,
            QuestionIds = refTest.QuestionIds,
            QuestionTotal = refTest.QuestionIds.Count,
            CreatedAt = refTest.CreatedAt,
            StartedAt = refTest.StartedAt,
            CompletedAt = refTest.CompletedAt,
            Status = refTest.Status,
            CurrentQuestionIndex = refTest.CurrentQuestionIndex,
            QuestionScore = refTest.QuestionScore,
            AnswerScore = refTest.AnswerScore,
            AnswerTotal = refTest.AnswerTotal,
            Percentage = refTest.Percentage,
            SelectedAnswerIds = refTest.SelectedAnswerIds,
            WrongQuestionIds = refTest.WrongQuestionIds,
            WrongAnswerIds = refTest.WrongAnswerIds,
            SendResultsAutomatically = refTest.SendResultsAutomatically,
            ResultsSent = refTest.ResultsSentAt.HasValue,
            Language = refTest.Language,
            Duration = refTest is { CompletedAt: not null, StartedAt: not null }
                ? refTest.CompletedAt.Value - refTest.StartedAt.Value
                : null,
            ApprovalStatus = refTest.ApprovalStatus,
            ApprovedAt = refTest.ApprovedAt,
            ApprovedByUserEmail = refTest.ApprovedByUserEmail,
            RejectionReason = refTest.RejectionReason
        };
    }
}