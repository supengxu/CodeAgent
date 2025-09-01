using System.Text.Json;

namespace CodeAgentDemo.Models;

/// <summary>
/// Abstract base class for content blocks in messages.
/// </summary>
public abstract record ContentBlock;

/// <summary>
/// Represents a text content block.
/// </summary>
public record TextBlock(string Text) : ContentBlock;

/// <summary>
/// Represents a thinking/reasoning content block.
/// </summary>
public record ThinkingBlock(string Thinking) : ContentBlock;

/// <summary>
/// Represents a tool use content block.
/// </summary>
public record ToolUseBlock(string Id, string Name, JsonElement Input) : ContentBlock;

/// <summary>
/// Represents a tool result content block.
/// </summary>
public record ToolResultBlock(string ToolUseId, string Content, bool IsError = false) : ContentBlock;
