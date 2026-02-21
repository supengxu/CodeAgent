using CodeAgentDemo.Cli;
using CodeAgentDemo.Models;

namespace CodeAgentDemo.Core;

/// <summary>
/// Implementation of message handler that manages message creation and session persistence.
/// </summary>
public class MessageHandler : IMessageHandler
{
    private readonly ISessionCli _sessionCli;

    /// <summary>
    /// Initializes a new instance of the MessageHandler class.
    /// </summary>
    /// <param name="sessionCli">The session CLI for managing sessions.</param>
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