using CodeAgentDemo.Models;

namespace CodeAgentDemo.Core;

/// <summary>
/// 处理消息创建和会话管理的接口。
/// </summary>
public interface IMessageHandler
{
    /// <summary>
    /// 创建用户消息并将其追加到会话中。
    /// </summary>
    Task<ChatMessage> CreateUserMessageAsync(string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建助手消息并将其追加到会话中。
    /// </summary>
    Task<ChatMessage> CreateAssistantMessageAsync(IEnumerable<ContentBlock> content, CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建工具结果消息并将其追加到会话中。
    /// </summary>
    Task<ChatMessage> CreateToolResultMessageAsync(string toolCallId, string output, bool isError, CancellationToken cancellationToken = default);

    /// <summary>
    /// 创建系统消息并将其追加到会话中。
    /// </summary>
    Task<ChatMessage> CreateSystemMessageAsync(string content, CancellationToken cancellationToken = default);
}