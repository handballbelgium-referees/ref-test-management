using Handball.Belgium.Rules.Quiz.Domain;
using HotChocolate.Data.Sorting;

namespace QuizManagement.Api.Graphql;

public class QuizSessionSortType : SortInputType<QuizSession>
{
    protected override void Configure(ISortInputTypeDescriptor<QuizSession> descriptor)
    {
        descriptor.BindFieldsExplicitly();
        descriptor.Description("Sort quiz sessions by Id, Email, Status, Creation Date, Start Date, Completion Date, Percentage, Score, Number of Questions and Maximum Time");
        descriptor.Field(x => x.Id).Description("Sort on quiz session id");
        descriptor.Field(x => x.FirstName).Description("Sort on first name of the user who started the quiz");
        descriptor.Field(x => x.LastName).Description("Sort on last name of the user who started the quiz");
        descriptor.Field(x => x.Email).Description("Sort on email of the user who started the quiz");
        descriptor.Field(x => x.InvitationSent).Description("Sort on invitation was sent");
        descriptor.Field(x => x.Status).Description("Sort on status of the quiz session");
        descriptor.Field(x => x.CreatedAt).Description("Sort on creation date of the quiz session");
        descriptor.Field(x => x.StartedAt).Description("Sort on start date of the quiz session");
        descriptor.Field(x => x.CompletedAt).Description("Sort on completion date of the quiz session");
        descriptor.Field(x => x.Percentage).Description("Sort on percentage of correct answers");
        descriptor.Field(x => x.Score).Description("Sort on score of the quiz session");
        descriptor.Field(x => x.NumberOfQuestions).Description("Sort on number of questions in the quiz");
        descriptor.Field(x => x.MaxTimeInMinutes).Description("Sort on maximum time in minutes for the quiz");
        descriptor.Field(x => x.ResultsSent).Description("Sort on results were sent");       
    }
}