using System.Text.Json;

namespace CodeAgentDemo.Models;

/// <summary>
/// 聊天补全的配置选项。
/// </summary>
public class ChatOptions
{
    /// <summary>
    /// 设置助手行为的系统提示。
    /// </summary>
    public string? SystemPrompt { get; set; }

    /// <summary>
    /// 响应的最大令牌数。
    /// </summary>
    public int MaxTokens { get; set; } = 4096;

    /// <summary>
    /// 是否启用扩展思考模式。
    /// </summary>
    public bool EnableThinking { get; set; }

    /// <summary>
    /// 思考预算令牌数（如果启用）。
    /// </summary>
    public int ThinkingBudgetTokens { get; set; }

    /// <summary>
    /// 函数调用的可用工具。
    /// </summary>
    public IEnumerable<ToolDefinition>? Tools { get; set; }
}

/// <summary>
/// LLM 工具的定义。
/// </summary>
public record ToolDefinition(
    string Name,
    string Description,
    JsonElement InputSchema
);
