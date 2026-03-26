using CodeAgentDemo.Models;
using CodeAgentDemo.Tools;

namespace CodeAgentDemo.Core;

/// <summary>
/// 处理工具执行的接口，支持可选的反思和重试逻辑。
/// </summary>
public interface IToolExecutor
{
    /// <summary>
    /// 执行工具调用，支持可选的基于反思的重试。
    /// </summary>
    /// <param name="toolCall">要执行的工具调用。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>工具执行的结果。</returns>
    Task<ToolResult> ExecuteAsync(ToolCall toolCall, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取成功的工具调用总数。
    /// </summary>
    int ToolCallCount { get; }
}