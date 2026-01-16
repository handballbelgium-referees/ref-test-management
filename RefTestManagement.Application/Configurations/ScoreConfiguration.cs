namespace Handball.Belgium.RefTestManagement.Application.Configurations;

public class ScoreConfiguration
{
    /// <summary>
    /// The percentage score required to pass the test.
    /// </summary>
    /// <example>80</example>
    public int PassingPercentage { get; init; } = 80;
    
    /// <summary>
    /// Points awarded for each correct answer that is selected by the user.
    /// </summary>
    /// <example>1</example>
    public int Correct { get; init; }
    
    /// <summary>
    /// Points awarded (typically negative) for each incorrect answer that is selected by the user.
    /// </summary>
    /// <example>-1</example>
    public int InCorrect { get; init; }
    
    /// <summary>
    /// Points awarded (typically 0 or negative) for each correct answer that is NOT selected by the user.
    /// This represents a missed correct answer.
    /// </summary>
    /// <example>0</example>
    public int NotAnswered { get; init; }
    
    /// <summary>
    /// Whether to allow negative scores for individual questions.
    /// </summary>
    /// <remarks>
    /// When false (default):
    /// - Questions scores are capped at 0 minimum (partial credit allowed, but no negative scores)
    /// - Example: If a question calculates to -1 point, it will be scored as 0
    /// 
    /// When true:
    /// - Questions can have negative scores if the penalties exceed the rewards
    /// - Useful for penalizing guessing strategies
    /// </remarks>
    /// <example>false</example>
    public bool NegativeScore { get; init; }
    
    /// <summary>
    /// Whether to apply a penalty when all answers for a question are selected (guessing strategy).
    /// </summary>
    /// <remarks>
    /// When true (default):
    /// - If ALL answers for a question are selected, the score for that question is 0
    /// - This prevents users from gaming the system by selecting all answers
    /// 
    /// When false:
    /// - Normal scoring logic applies even when all answers are selected
    /// - The score will be calculated based on Correct/InCorrect/NotAnswered points
    /// </remarks>
    /// <example>true</example>
    public bool PenalizeGuessingStrategy { get; init; } = true;
}