using CodeAgentDemo.Models;

namespace CodeAgentDemo.Core;

/// <summary>
/// Handles message creation and session management operations.
/// </summary>
public interface IMessageHandler
{
    /// <summary>
    /// Creates a user message and appends it to the session.
    /// </summary>
    Task<ChatMessage> CreateUserMessageAsync(string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an assistant message and appends it to the session.
    /// </summary>
    Task<ChatMessage> CreateAssistantMessageAsync(IEnumerable<ContentBlock> content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a tool result message and appends it to the session.
    /// </summary>
    Task<ChatMessage> CreateToolResultMessageAsync(string toolCallId, string output, bool isError, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a system message and appends it to the session.
    /// </summary>
    Task<ChatMessage> CreateSystemMessageAsync(string content, CancellationToken cancellationToken = default);
}