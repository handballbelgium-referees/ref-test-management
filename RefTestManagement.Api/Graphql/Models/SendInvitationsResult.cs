using Handball.Belgium.RefTestManagement.Api.Graphql.ReadModels;

namespace Handball.Belgium.RefTestManagement.Api.Graphql.Models;

public class SendInvitationsResult
{
    public int TotalRequested { get; set; }
    public int SuccessfullySent { get; set; }
    public int Failed { get; set; }
    public List<RefTestDto> SentRefTests { get; set; } = [];
    public List<SendInvitationError> Errors { get; set; } = [];
}

public class SendInvitationError
{
    public Guid RefTestId { get; set; }
    public User? User { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}