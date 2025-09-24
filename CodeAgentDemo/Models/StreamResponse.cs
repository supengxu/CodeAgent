using System.Text.Json;

namespace CodeAgentDemo.Models;

/// <summary>
/// Represents the complete response after streaming ends.
/// </summary>
public record StreamResponse(
    IEnumerable<ContentBlock> Content,
    IReadOnlyList<ToolCall> ToolCalls,
    string StopReason,
    UsageInfo? Usage = null
);

/// <summary>
/// Represents a complete tool call.
/// </summary>
public record ToolCall(
    string Id,
    string Name,
    JsonElement Arguments
);
