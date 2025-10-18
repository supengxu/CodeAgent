using System.Collections.ObjectModel;
using CodeAgentDemo.Models;

namespace CodeAgentDemo.Core;

/// <summary>
/// Manages chat message history for a session, with optional JSONL persistence.
/// </summary>
public partial class SessionStore
{
    private readonly List<ChatMessage> _messages = new();

    /// <summary>
    /// Gets a read-only view of all messages.
    /// </summary>
    public IReadOnlyList<ChatMessage> Messages => new ReadOnlyCollection<ChatMessage>(_messages);

    /// <summary>
    /// Adds a message to the history.
    /// </summary>
    public void AddMessage(ChatMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        _messages.Add(message);
    }

    /// <summary>
    /// Creates and adds a text message with the specified role.
    /// </summary>
    public void AddMessage(ChatRole role, string text)
    {
        _messages.Add(ChatMessage.CreateText(role, text));
    }

    /// <summary>
    /// Creates and adds a message with the specified role and content.
    /// </summary>
    public void AddMessage(ChatRole role, IEnumerable<ContentBlock> content)
    {
        _messages.Add(new ChatMessage(role, content));
    }

    /// <summary>
    /// Clears all messages from the session.
    /// </summary>
    public void Clear()
    {
        _messages.Clear();
    }

    /// <summary>
    /// Gets the number of messages in the session.
    /// </summary>
    public int Count => _messages.Count;

    public SessionStore() { }

    public SessionStore(List<ChatMessage> initialMessages)
    {
        _messages = initialMessages ?? new List<ChatMessage>();
    }
}
