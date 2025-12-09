using Handball.Belgium.Rules.Quiz.Domain;

namespace QuizManagement.Api.Graphql;

/// <summary>
/// Exception thrown when a quiz session is not found
/// </summary>
public class QuizSessionNotFoundException : Exception
{
    public QuizSessionNotFoundException() : base("Quiz session not found")
    {
    }

    public QuizSessionNotFoundException(string token) : base($"Quiz session with token '{token}' not found")
    {
    }
}

/// <summary>
/// Exception thrown when a quiz session has expired
/// </summary>
public class QuizSessionExpiredException : Exception
{
    public QuizSessionExpiredException() : base("Quiz session has expired")
    {
    }

    public QuizSessionExpiredException(string token) : base($"Quiz session with token '{token}' has expired")
    {
    }
}

/// <summary>
/// Exception thrown when attempting to perform an action on a quiz session with an invalid status
/// </summary>
public class InvalidQuizSessionStatusException : Exception
{
    public InvalidQuizSessionStatusException(string message) : base(message)
    {
    }

    public InvalidQuizSessionStatusException(QuizSessionStatus currentStatus, QuizSessionStatus expectedStatus) 
        : base($"Quiz session is in '{currentStatus}' status. Expected '{expectedStatus}' status.")
    {
    }
}

