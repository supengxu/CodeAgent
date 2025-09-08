using System.Text.Json;

namespace CodeAgentDemo.Models;

public record StreamChunk(
    string? TextDelta,
    string? ThinkingDelta,
    ToolCallDelta? ToolCallDelta,
    string? StopReason = null
);

public record ToolCallDelta(
    string? Id,
    string? Name,
    string? ArgumentsDelta
);
