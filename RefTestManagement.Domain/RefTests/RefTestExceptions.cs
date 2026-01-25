namespace Handball.Belgium.RefTestManagement.Domain.RefTests;

/// <summary>
/// Exception thrown when a RefTest is not found
/// </summary>
public class RefTestNotFoundException : Exception
{
    public RefTestNotFoundException() : base("RefTest not found")
    {
    }

    public RefTestNotFoundException(string token) : base($"RefTest with token '{token}' not found")
    {
    }

    public RefTestNotFoundException(Guid id) : base($"RefTest with ID '{id}' not found")
    {
    }
}

/// <summary>
/// Exception thrown when a RefTest has expired
/// </summary>
public class RefTestExpiredException : Exception
{
    public RefTestExpiredException() : base("RefTest has expired")
    {
    }

    public RefTestExpiredException(string token) : base($"RefTest with token '{token}' has expired")
    {
    }
}

/// <summary>
/// Exception thrown when attempting to perform an action on a RefTest with an invalid status
/// </summary>
public class InvalidRefTestStatusException : Exception
{
    public InvalidRefTestStatusException(string message) : base(message)
    {
    }

    public InvalidRefTestStatusException(RefTestStatus currentStatus, RefTestStatus expectedStatus)
        : base($"RefTest is in '{currentStatus}' status. Expected status: '{expectedStatus}'.")  
    {
    }

    public InvalidRefTestStatusException(RefTestStatus currentStatus, RefTestStatus[] expectedStatuses) 
        : base($"RefTest is in '{currentStatus}' status. Expected statuses: '{string.Join(", '", expectedStatuses)}'.")  
    {
    }
}

