using CodeAgentDemo.Cli;
using CodeAgentDemo.Models;

namespace CodeAgentDemo.Core;

/// <summary>
/// 消息处理器的实现类，管理消息创建和会话持久化。
/// </summary>
public class MessageHandler : IMessageHandler
{
    private readonly ISessionCli _sessionCli;

    /// <summary>
    /// 初始化 MessageHandler 类的新实例。
    /// </summary>
    /// <param name="sessionCli">用于管理会话的会话 CLI。</param>
    public MessageHandler(ISessionCli sessionCli)
    {
        _sessionCli = sessionCli ?? throw new ArgumentNullException(nameof(sessionCli));
    }

    /// <inheritdoc />
    public async Task<ChatMessage> CreateUserMessageAsync(string content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var message = ChatMessage.CreateText(ChatRole.User, content);
        await _sessionCli.AppendMessageAsync(message);
        return message;
    }

    /// <inheritdoc />
    public async Task<ChatMessage> CreateAssistantMessageAsync(IEnumerable<ContentBlock> content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var message = new ChatMessage(ChatRole.Assistant, content);
        await _sessionCli.AppendMessageAsync(message);
        return message;
    }

    /// <inheritdoc />
    public async Task<ChatMessage> CreateToolResultMessageAsync(string toolCallId, string output, bool isError, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(toolCallId);
        ArgumentNullException.ThrowIfNull(output);

        var message = new ChatMessage(ChatRole.Tool, new[]
        {
            new ToolResultBlock(toolCallId, output, isError)
        });
        await _sessionCli.AppendMessageAsync(message);
        return message;
    }

    /// <inheritdoc />
    public async Task<ChatMessage> CreateSystemMessageAsync(string content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var message = ChatMessage.CreateText(ChatRole.System, content);
        await _sessionCli.AppendMessageAsync(message);
        return message;
    }
}