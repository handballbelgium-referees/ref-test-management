using System.Security.Cryptography;
using System.Text;
using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Application.Services;
using Handball.Belgium.RefTestManagement.Application.Models;
using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Types;

public sealed class ParticipantRefTestType : ObjectType<ParticipantRefTestDto>
{
    protected override void Configure(IObjectTypeDescriptor<ParticipantRefTestDto> descriptor)
    {
        descriptor.Name("ParticipantRefTest");
        descriptor.Description("A RefTest view for its participant.");
        descriptor.BindFieldsExplicitly();

        descriptor.Field(x => x.Id).Description("The RefTest id");
        descriptor.Field(x => x.Name).Description("The participant name");
        descriptor.Field(x => x.Email).Description("The participant email");
        descriptor.Field(x => x.NumberOfQuestions).Description("Number of questions");
        descriptor.Field(x => x.MaxTimeInMinutes).Description("Maximum time in minutes");
        descriptor.Field(x => x.StartedAt).Description("Start date and time");
        descriptor.Field(x => x.Status).Description("The RefTest status");
        descriptor.Field(x => x.CurrentQuestionIndex).Description("Current question index");
        descriptor.Field(x => x.SelectedAnswerIds).Description("Selected answer ids");
        descriptor.Field(x => x.QuestionTotal).Description("Total possible question score");
        descriptor.Field(x => x.QuestionScore).Description("Score based on fully correct questions");
        descriptor.Field(x => x.AnswerScore).Description("Score based on individual answers");
        descriptor.Field(x => x.AnswerTotal).Description("Total possible answer score");
        descriptor.Field(x => x.Percentage).Description("Percentage of correct answers");
        descriptor.Field(x => x.SendResultsAutomatically)
            .Description("Whether results are sent automatically");
        descriptor.Field(x => x.ResultsSent).Description("Whether results were sent");
        descriptor.Field("questions")
            .Description("Questions for this participant RefTest")
            .Argument("includeNumber", x => x.Type<BooleanType>().DefaultValue(false))
            .Resolve(
                (ctx, ct) =>
                {
                    var refTest = ctx.Parent<ParticipantRefTestDto>();
                    return GetQuestions(
                        refTest.Id,
                        refTest.QuestionIds,
                        ctx.Service<IIhfRulesQuestionsService>(),
                        ctx.ArgumentValue<bool>("includeNumber"),
                        refTest.Status == RefTestStatus.Completed,
                        ct);
                });
    }

    private static async Task<List<ParticipantQuestionDto>> GetQuestions(
        Guid refTestId,
        IReadOnlyList<string> questionIds,
        IIhfRulesQuestionsService service,
        bool includeNumber,
        bool includeIsCorrect,
        CancellationToken cancellationToken)
    {
        var questions = await service.GetQuestionsByIdAsync(
            questionIds,
            includeNumber,
            includeIsCorrect,
            randomAnswerOrder: false,
            cancellationToken: cancellationToken);

        return questions.Select(question =>
        {
            var participantQuestion = question.ToParticipantDto();
            return new ParticipantQuestionDto
            {
                Id = participantQuestion.Id,
                Number = includeNumber ? participantQuestion.Number : null,
                Phrase = participantQuestion.Phrase,
                Answers = participantQuestion.Answers
                    .OrderBy(answer => GetStableAnswerOrderKey(refTestId, participantQuestion.Id, answer.Id))
                    .Select(answer => new ParticipantAnswerDto
                    {
                        Id = answer.Id,
                        Number = includeNumber ? answer.Number : null,
                        Phrase = answer.Phrase,
                        IsCorrect = includeIsCorrect && answer.IsCorrect
                    }).ToList()
            };
        }).ToList();
    }

    private static string GetStableAnswerOrderKey(Guid refTestId, string questionId, string answerId)
    {
        var input = Encoding.UTF8.GetBytes($"{refTestId:N}:{questionId}:{answerId}");
        return Convert.ToHexString(SHA256.HashData(input));
    }
}

public sealed class ParticipantQuestionType : ObjectType<ParticipantQuestionDto>
{
    protected override void Configure(IObjectTypeDescriptor<ParticipantQuestionDto> descriptor)
    {
        descriptor.Name("ParticipantQuestion");
        descriptor.BindFieldsExplicitly();
        descriptor.Field(x => x.Id);
        descriptor.Field(x => x.Number);
        descriptor.Field(x => x.Phrase).Type<AnyType>();
        descriptor.Field(x => x.Answers);
    }
}

public sealed class ParticipantAnswerType : ObjectType<ParticipantAnswerDto>
{
    protected override void Configure(IObjectTypeDescriptor<ParticipantAnswerDto> descriptor)
    {
        descriptor.Name("ParticipantAnswer");
        descriptor.BindFieldsExplicitly();
        descriptor.Field(x => x.Id);
        descriptor.Field(x => x.Number);
        descriptor.Field(x => x.Phrase).Type<AnyType>();
        descriptor.Field(x => x.IsCorrect);
    }
}
