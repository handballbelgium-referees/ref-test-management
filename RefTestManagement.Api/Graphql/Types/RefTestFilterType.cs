using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using HotChocolate.Data.Filters;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Types;

public class RefTestFilterType : FilterInputType<RefTestDto>
{
    protected override void Configure(IFilterInputTypeDescriptor<RefTestDto> descriptor)
    {
        descriptor.BindFieldsExplicitly();
        descriptor.Name($"{nameof(RefTest)}FilterInput");
        descriptor.Description("Filter RefTests based on Id, Email or Status");
        descriptor.Field(x => x.Id).Description("Filter on RefTest id");
        descriptor.Field(x => x.Title).Description("Filter on RefTest title");
        descriptor.Field(x => x.FirstName).Description("Filter on first name of the user who started the RefTest");
        descriptor.Field(x => x.LastName).Description("Filter on last name of the user who started the RefTest");
        descriptor.Field(x => x.Email).Description("Filter on email of the user who started the RefTest");
        descriptor.Field(x => x.InvitationSent).Description("Filter on invitation was sent for the RefTest");
        descriptor.Field(x => x.SendInvitationsAutomatically).Description("Filter on whether invitations are sent automatically");
        descriptor.Field(x => x.Status).Description("Filter on status of the RefTest (e.g., InProgress, Completed, Expired)");
        descriptor.Field(x => x.CreatedAt).Description("Filter on creation date of the RefTest");
        descriptor.Field(x => x.StartedAt).Description("Filter on start date of the RefTest");
        descriptor.Field(x => x.CompletedAt).Description("Filter on completion date of the RefTest");
        descriptor.Field(x => x.Percentage).Description("Filter on percentage of correct answers");
        descriptor.Field(x => x.QuestionScore).Description("Filter on question score of the RefTest");
        descriptor.Field(x => x.AnswerScore).Description("Filter on answer score of the RefTest");
        descriptor.Field(x => x.QuestionTotal).Description("Filter on total possible question score");
        descriptor.Field(x => x.AnswerTotal).Description("Filter on total possible answer score");
        descriptor.Field(x => x.NumberOfQuestions).Description("Filter on number of questions in the RefTest");
        descriptor.Field(x => x.MaxTimeInMinutes).Description("Filter on maximum time in minutes for the RefTest");
        descriptor.Field(x => x.ResultsSent).Description("Filter on results were sent for the RefTest");
        descriptor.Field(x => x.SendResultsAutomatically).Description("Filter on whether results are sent automatically");
    }
}
