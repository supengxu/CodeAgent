namespace CodeAgentDemo.Models;

/// <summary>
/// Configuration for loop control mechanisms.
/// </summary>
public class LoopControlConfig
{
    /// <summary>
    /// Maximum number of iterations allowed in the agent loop.
    /// Default is 15.
    /// </summary>
    public int MaxIterations { get; set; } = 15;

    /// <summary>
    /// Token usage ratio threshold (0.0 to 1.0) that triggers a warning.
    /// Default is 0.8 (80% of context window).
    /// </summary>
    public double TokenLimitRatio { get; set; } = 0.8;

    /// <summary>
    /// Number of recent states to keep for cycle detection.
    /// Default is 5.
    /// </summary>
    public int CycleDetectionWindow { get; set; } = 5;
}