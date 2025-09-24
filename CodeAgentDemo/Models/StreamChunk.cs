using System.Text.Json;

namespace CodeAgentDemo.Models;

public record StreamChunk(
    string? TextDelta,
    string? ThinkingDelta,
    ToolCallDelta? ToolCallDelta,
    string? StopReason = null,
    UsageInfo? Usage = null
);

public record ToolCallDelta(
    string? Id,
    string? Name,
    string? ArgumentsDelta
);

public record UsageInfo(
    int InputTokens,
    int OutputTokens
);
