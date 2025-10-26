namespace CodeAgentDemo.Models;

/// <summary>
/// Configuration for reflection engine behavior.
/// Reflection is triggered only on failure for self-correction.
/// </summary>
public class ReflectionConfig
{
    /// <summary>
    /// Maximum number of retry attempts after reflection.
    /// Default is 3.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Token budget ratio for reflection (0.0 to 1.0).
    /// Maximum percentage of total token budget allocated to reflection.
    /// Default is 0.2 (20%).
    /// </summary>
    public double TokenBudgetRatio { get; set; } = 0.2;
}