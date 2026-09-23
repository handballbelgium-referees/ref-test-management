using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;

public static class ParticipantRefTestMappings
{
    public static ParticipantRefTestDto ToParticipantDto(this RefTest refTest) =>
        new()
        {
            Id = refTest.Id,
            Name = refTest.FullName,
            Email = refTest.Email,
            NumberOfQuestions = refTest.NumberOfQuestions,
            MaxTimeInMinutes = refTest.MaxTimeInMinutes,
            QuestionIds = refTest.QuestionIds,
            StartedAt = refTest.StartedAt,
            Status = refTest.Status,
            CurrentQuestionIndex = refTest.CurrentQuestionIndex,
            SelectedAnswerIds = refTest.SelectedAnswerIds,
            QuestionTotal = refTest.QuestionTotal,
            QuestionScore = refTest.QuestionScore,
            AnswerScore = refTest.AnswerScore,
            AnswerTotal = refTest.AnswerTotal,
            Percentage = refTest.Percentage,
            SendResultsAutomatically = refTest.SendResultsAutomatically,
            ResultsSent = refTest.ResultsSentAt.HasValue
        };

    public static ParticipantQuestionDto ToParticipantDto(this Question question) =>
        new()
        {
            Id = question.Id,
            Number = question.Number,
            Phrase = question.Phrase,
            Answers = question.Answers.Select(answer => new ParticipantAnswerDto
            {
                Id = answer.Id,
                Number = answer.Number,
                Phrase = answer.Phrase,
                IsCorrect = answer.IsCorrect
            }).ToList()
        };
}
