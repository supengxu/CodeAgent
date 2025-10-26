namespace CodeAgentDemo.Models;

/// <summary>
/// Result of a reflection analysis.
/// Contains analysis of failure and suggestions for correction.
/// </summary>
public record ReflectionResult
{
    /// <summary>
    /// Analysis of what went wrong.
    /// </summary>
    public string Analysis { get; init; } = string.Empty;

    /// <summary>
    /// List of suggestions for correcting the failure.
    /// </summary>
    public List<string> Suggestions { get; init; } = [];

    /// <summary>
    /// Whether a retry should be attempted based on the reflection.
    /// </summary>
    public bool ShouldRetry { get; init; }
}