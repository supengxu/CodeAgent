using System.Text.Json;

namespace CodeAgentDemo.Models;

/// <summary>
/// Configuration options for chat completion.
/// </summary>
public class ChatOptions
{
    /// <summary>
    /// System prompt to set the assistant's behavior.
    /// </summary>
    public string? SystemPrompt { get; set; }
    
    /// <summary>
    /// Maximum tokens for the response.
    /// </summary>
    public int MaxTokens { get; set; } = 4096;
    
    /// <summary>
    /// Whether to enable extended thinking mode.
    /// </summary>
    public bool EnableThinking { get; set; }
    
    /// <summary>
    /// Budget tokens for thinking (if enabled).
    /// </summary>
    public int ThinkingBudgetTokens { get; set; }
    
    /// <summary>
    /// Available tools for function calling.
    /// </summary>
    public IEnumerable<ToolDefinition>? Tools { get; set; }
}

/// <summary>
/// Definition of a tool for the LLM.
/// </summary>
public record ToolDefinition(
    string Name,
    string Description,
    JsonElement InputSchema
);
