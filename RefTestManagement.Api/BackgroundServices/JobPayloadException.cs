namespace Handball.Belgium.RefTestManagement.Api.BackgroundServices;

/// <summary>
/// Thrown when a job's payload cannot be turned into the type the handler expects. Distinct from
/// an ordinary processing failure because retrying cannot help: the payload is stored on the row
/// and will be exactly as unparseable on the next attempt. A cancelled job's payload is cleared,
/// so an empty string reaching a handler is the common cause.
/// </summary>
public class JobPayloadException : Exception
{
    public JobPayloadException(string message) : base(message)
    {
    }

    public JobPayloadException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
