using System.Text.Json;

namespace CodeAgentDemo.Tools;

/// <summary>
/// Interface for tools that can be called by the LLM.
/// </summary>
public interface ITool
{
    /// <summary>
    /// Tool name used for identification.
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Description of what the tool does.
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// JSON Schema for input parameters.
    /// </summary>
    JsonElement InputSchema { get; }
    
    /// <summary>
    /// Executes the tool with given arguments.
    /// </summary>
    Task<ToolResult> ExecuteAsync(JsonElement arguments);
}

/// <summary>
/// Result of tool execution.
/// </summary>
public record ToolResult(bool Success, string Output);
