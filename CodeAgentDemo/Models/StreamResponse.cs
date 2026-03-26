using System.Text.Json;

namespace CodeAgentDemo.Models;

/// <summary>
/// 流结束后的完整响应。
/// </summary>
public record StreamResponse(
    IEnumerable<ContentBlock> Content,
    IReadOnlyList<ToolCall> ToolCalls,
    string StopReason,
    UsageInfo? Usage = null
);

/// <summary>
/// 表示完整的工具调用。
/// </summary>
public record ToolCall(
    string Id,
    string Name,
    JsonElement Arguments
);
