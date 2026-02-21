using CodeAgentDemo.Models;
using CodeAgentDemo.Tools;

namespace CodeAgentDemo.Core;

/// <summary>
/// Handles tool execution with optional reflection and retry logic.
/// </summary>
public interface IToolExecutor
{
    /// <summary>
    /// Executes a tool call with optional reflection-based retry.
    /// </summary>
    /// <param name="toolCall">The tool call to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of tool execution.</returns>
    Task<ToolResult> ExecuteAsync(ToolCall toolCall, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total number of successful tool calls.
    /// </summary>
    int ToolCallCount { get; }
}