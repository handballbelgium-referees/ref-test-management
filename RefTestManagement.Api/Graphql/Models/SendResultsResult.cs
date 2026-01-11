namespace Handball.Belgium.RefTestManagement.Api.Graphql.Models;

public class SendResultsResult
{
    public int TotalRequested { get; set; }
    public int SuccessfullySent { get; set; }
    public int Failed { get; set; }
    public List<Domain.RefTest> SentRefTests { get; set; } = [];
    public List<SendResultError> Errors { get; set; } = [];
}

public class SendResultError
{
    public Guid RefTestId { get; set; }
    public User? User { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}