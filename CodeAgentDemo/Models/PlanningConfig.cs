namespace CodeAgentDemo.Models;

/// <summary>
/// Configuration options for task planning.
/// </summary>
public class PlanningConfig
{
    /// <summary>
    /// Token threshold for determining task complexity.
    /// Tasks with estimated tokens above this threshold are considered complex.
    /// Default: 500 tokens.
    /// </summary>
    public int ComplexityThreshold { get; set; } = 500;

    /// <summary>
    /// Maximum time in seconds allowed for plan generation.
    /// Default: 2 seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 100;

    /// <summary>
    /// Minimum number of expected tool calls to consider a task complex.
    /// Default: 3 tool calls.
    /// </summary>
    public int MinToolCallsForComplex { get; set; } = 3;
}