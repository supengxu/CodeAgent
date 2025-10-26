using System.Text.Json;
using CodeAgentDemo.Models;
using CodeAgentDemo.Tools;

namespace CodeAgentDemo.Core;

/// <summary>
/// Interface for reflection engine that analyzes failures and generates correction suggestions.
/// Reflection is triggered only on failure, not on success.
/// </summary>
public interface IReflectionEngine
{
    /// <summary>
    /// Determines whether reflection should be triggered based on the execution result.
    /// </summary>
    /// <param name="toolResult">The result of the tool execution.</param>
    /// <param name="attemptCount">Current attempt count (starts at 1).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if reflection should be triggered, false otherwise.</returns>
    Task<bool> ShouldReflectAsync(ToolResult toolResult, int attemptCount, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs reflection on a failed execution and generates suggestions.
    /// </summary>
    /// <param name="context">Context information about the failure (tool name, arguments, error message).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Reflection result containing analysis and suggestions.</returns>
    Task<ReflectionResult> ReflectAsync(ReflectionContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Context for reflection containing information about the failed execution.
/// </summary>
public record ReflectionContext
{
    /// <summary>
    /// Name of the tool that failed.
    /// </summary>
    public string ToolName { get; init; } = string.Empty;

    /// <summary>
    /// Arguments passed to the tool.
    /// </summary>
    public JsonElement Arguments { get; init; }

    /// <summary>
    /// Error message from the failed execution.
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// Current attempt number.
    /// </summary>
    public int AttemptNumber { get; init; }
}