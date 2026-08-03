using System.Linq.Expressions;
using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;

public static class RefTestMappings
{
    public static readonly Expression<Func<RefTest, RefTestDto>> ToDto =
        refTest => new RefTestDto
        {
            Id = refTest.Id,
            Title = refTest.Title == null
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
            Duration = refTest.CompletedAt != null && refTest.StartedAt != null
                ? refTest.CompletedAt.Value - refTest.StartedAt.Value
                : null,
            RejectionReason = refTest.RejectionReason,
            ScheduledAt = refTest.ScheduledAt,
            IsAnonymized = refTest.IsAnonymized,
            AnonymizedAt = refTest.AnonymizedAt
        };
}