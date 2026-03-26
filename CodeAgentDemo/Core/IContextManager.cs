using CodeAgentDemo.Models;

namespace CodeAgentDemo.Core;

/// <summary>
/// 管理对话上下文和压缩的接口。
/// </summary>
public interface IContextManager
{
    /// <summary>
    /// 获取给定消息的总 token 数量。
    /// </summary>
    /// <param name="messages">要计数 token 的消息。</param>
    /// <returns>估算的 token 数量。</returns>
    int GetTokenCount(IEnumerable<ChatMessage> messages);

    /// <summary>
    /// 确定是否应该执行上下文压缩。
    /// </summary>
    /// <param name="messages">当前消息历史。</param>
    /// <param name="currentTokenCount">当前 token 数量（可选，如果未提供将自动计算）。</param>
    /// <returns>如果应该执行压缩则返回 true，否则返回 false。</returns>
    bool ShouldCompress(IEnumerable<ChatMessage> messages, int? currentTokenCount = null);

    /// <summary>
    /// 通过删除或总结旧消息来压缩上下文。
    /// 保留系统提示、工具定义和最近的对话轮次。
    /// </summary>
    /// <param name="messages">要压缩的消息。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含压缩消息和元数据的压缩结果。</returns>
    Task<CompressionResult> CompressAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default);
}