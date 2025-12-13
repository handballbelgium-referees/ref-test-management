using Handball.Belgium.Rules.Quiz.Domain;
using HotChocolate.Data.Filters;

namespace QuizManagement.Api.Graphql;

public class QuizSessionFilterType : FilterInputType<QuizSession>
{
    protected override void Configure(IFilterInputTypeDescriptor<QuizSession> descriptor)
    {
        descriptor.BindFieldsExplicitly();
        descriptor.Description("Filter quiz sessions based on Id, Email or Status");
        descriptor.Field(x => x.Id).Description("Filter on quiz session id");
        descriptor.Field(x => x.TitleId).Description("Filter on title id");
        descriptor.Field(x => x.FirstName).Description("Filter on first name of the user who started the quiz");
        descriptor.Field(x => x.LastName).Description("Filter on last name of the user who started the quiz");
        descriptor.Field(x => x.Email).Description("Filter on email of the user who started the quiz");
        descriptor.Field(x => x.InvitationSent).Description("Filter on invitation was sent");
        descriptor.Field(x => x.Status).Description("Filter on status of the quiz session");
        descriptor.Field(x => x.CreatedAt).Description("Filter on creation date of the quiz session");
        descriptor.Field(x => x.StartedAt).Description("Filter on start date of the quiz session");
        descriptor.Field(x => x.CompletedAt).Description("Filter on completion date of the quiz session");
        descriptor.Field(x => x.Percentage).Description("Filter on percentage of correct answers");
        descriptor.Field(x => x.Score).Description("Filter on score of the quiz session");
        descriptor.Field(x => x.NumberOfQuestions).Description("Filter on number of questions in the quiz");
        descriptor.Field(x => x.MaxTimeInMinutes).Description("Filter on maximum time in minutes for the quiz");
        descriptor.Field(x => x.ResultsSent).Description("Filter on results were sent");
    }
}