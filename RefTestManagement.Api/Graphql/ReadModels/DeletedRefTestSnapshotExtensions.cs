using Handball.Belgium.RefTestManagement.Application.RefTests.Deletion;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;

internal static class DeletedRefTestSnapshotExtensions
{
    public static RefTestDto ToDto(this DeletedRefTestSnapshot snapshot)
    {
        var dto = snapshot.RefTest.ToDto();
        return new RefTestDto
        {
            Id = dto.Id,
            Title = dto.Title,
            FirstName = snapshot.FirstName,
            LastName = snapshot.LastName,
            FullName = $"{snapshot.FirstName} {snapshot.LastName}",
            Email = snapshot.Email,
            Token = snapshot.Token,
            SendInvitationsAutomatically = dto.SendInvitationsAutomatically,
            InvitationSent = dto.InvitationSent,
            NumberOfQuestions = dto.NumberOfQuestions,
            MaxTimeInMinutes = dto.MaxTimeInMinutes,
            QuestionIds = dto.QuestionIds,
            QuestionTotal = dto.QuestionTotal,
            CreatedAt = dto.CreatedAt,
            StartedAt = dto.StartedAt,
            CompletedAt = dto.CompletedAt,
            Status = dto.Status,
            CurrentQuestionIndex = dto.CurrentQuestionIndex,
            QuestionScore = dto.QuestionScore,
            AnswerScore = dto.AnswerScore,
            AnswerTotal = dto.AnswerTotal,
            Percentage = dto.Percentage,
            SelectedAnswerIds = dto.SelectedAnswerIds,
            WrongQuestionIds = dto.WrongQuestionIds,
            WrongAnswerIds = dto.WrongAnswerIds,
            SendResultsAutomatically = dto.SendResultsAutomatically,
            ResultsSent = dto.ResultsSent,
            Language = dto.Language,
            Duration = dto.Duration,
            RejectionReason = snapshot.RejectionReason,
            ScheduledAt = dto.ScheduledAt,
            IsAnonymized = snapshot.IsAnonymized,
            AnonymizedAt = snapshot.AnonymizedAt
        };
    }
}
