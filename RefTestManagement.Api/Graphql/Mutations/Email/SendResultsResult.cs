using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Creation;
using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Shared;
using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Email;

public class SendResultsResult
{
    public int TotalRequested { get; set; }
    public int SuccessfullySent { get; set; }
    public int Failed { get; set; }
    public List<RefTestDto> SentRefTests { get; set; } = [];
    public List<SendResultError> Errors { get; set; } = [];
}

public class SendResultError
{
    public Guid RefTestId { get; set; }
    public User? User { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

