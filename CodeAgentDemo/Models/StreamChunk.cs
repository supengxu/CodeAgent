using System.Text.Json;

namespace CodeAgentDemo.Models;

/// <summary>
/// Represents a delta chunk from streaming response.
/// </summary>
public record StreamChunk(
    string? TextDelta,
    string? ThinkingDelta,
    ToolCallDelta? ToolCallDelta
);

/// <summary>
/// Represents a partial tool call update in streaming.
/// </summary>
public record ToolCallDelta(
    string? Id,
    string? Name,
    string? ArgumentsDelta
);
