using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;
using Handball.Belgium.RefTestManagement.Domain.RefTests;
using HotChocolate.Data.Sorting;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Types;

public class RefTestSortType : SortInputType<RefTestDto>
{
    protected override void Configure(ISortInputTypeDescriptor<RefTestDto> descriptor)
    {
        descriptor.BindFieldsExplicitly();
        descriptor.Name($"{nameof(RefTest)}SortInput");
        descriptor.Description(
            "Sort RefTests by Id, Email, Status, Creation Date, Start Date, Completion Date, Percentage, QuestionScore, Number of Questions and Maximum Time");
        descriptor.Field(x => x.Id).Description("Sort on RefTest id");
        descriptor.Field(x => x.FirstName).Description("Sort on first name of the user who started the RefTest");
        descriptor.Field(x => x.LastName).Description("Sort on last name of the user who started the RefTest");
        descriptor.Field(x => x.Email).Description("Sort on email of the user who started the RefTest");
        descriptor.Field(x => x.InvitationSent).Description("Sort on invitation was sent for the RefTest");
        descriptor.Field(x => x.Status)
            .Description("Sort on status of the RefTest (e.g., InProgress, Completed, Expired)");
        descriptor.Field(x => x.CreatedAt).Description("Sort on creation date of the RefTest");
        descriptor.Field(x => x.StartedAt).Description("Sort on start date of the RefTest");
        descriptor.Field(x => x.CompletedAt).Description("Sort on completion date of the RefTest");
        descriptor.Field(x => x.Percentage).Description("Sort on percentage of correct answers");
        descriptor.Field(x => x.QuestionScore).Description("Sort on question score of the RefTest");
        descriptor.Field(x => x.AnswerScore).Description("Sort on answer score of the RefTest");
        descriptor.Field(x => x.QuestionTotal).Description("Sort on total possible question score");
        descriptor.Field(x => x.AnswerTotal).Description("Sort on total possible answer score");
        descriptor.Field(x => x.NumberOfQuestions).Description("Sort on number of questions in the RefTest");
        descriptor.Field(x => x.MaxTimeInMinutes).Description("Sort on maximum time in minutes for the RefTest");
        descriptor.Field(x => x.ResultsSent).Description("Sort on results were sent for the RefTest");
    }
}
