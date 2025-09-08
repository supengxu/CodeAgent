using System.Text.Json;

namespace CodeAgentDemo.Tools;

public interface ITool
{
    string Name { get; }
    string Description { get; }
    JsonElement InputSchema { get; }
    Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 判断工具执行是否需要用户确认。
    /// 返回 true 表示需要确认，false 表示可以跳过确认直接执行。
    /// </summary>
    bool RequiresConfirmation(JsonElement arguments) => true;
}

public record ToolResult(bool Success, string Output);
