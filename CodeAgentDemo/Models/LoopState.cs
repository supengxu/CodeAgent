namespace CodeAgentDemo.Models;

/// <summary>
/// Represents the current state of the agent loop.
/// </summary>
public class LoopState
{
    /// <summary>
    /// Current iteration count.
    /// </summary>
    public int IterationCount { get; set; }

    /// <summary>
    /// Current token usage.
    /// </summary>
    public int TokenUsage { get; set; }

    /// <summary>
    /// Maximum token limit.
    /// </summary>
    public int MaxTokens { get; set; }

    /// <summary>
    /// Whether a cycle has been detected.
    /// </summary>
    public bool DetectedCycle { get; set; }

    /// <summary>
    /// Whether the iteration limit has been reached.
    /// </summary>
    public bool IterationLimitReached { get; set; }

    /// <summary>
    /// Whether the token limit has been reached.
    /// </summary>
    public bool TokenLimitReached { get; set; }

    /// <summary>
    /// Creates a new instance of LoopState with default values.
    /// </summary>
    public LoopState()
    {
    }

    /// <summary>
    /// Creates a new instance of LoopState with specified values.
    /// </summary>
    public LoopState(int iterationCount, int tokenUsage, int maxTokens)
    {
        IterationCount = iterationCount;
        TokenUsage = tokenUsage;
        MaxTokens = maxTokens;
    }
}