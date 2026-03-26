using System.Collections.ObjectModel;
using CodeAgentDemo.Models;

namespace CodeAgentDemo.Core;

/// <summary>
/// 管理会话的聊天消息历史，支持可选的 JSONL 持久化。
/// </summary>
public partial class SessionStore
{
    private readonly List<ChatMessage> _messages = new();

    /// <summary>
    /// 获取所有消息的只读视图。
    /// </summary>
    public IReadOnlyList<ChatMessage> Messages => new ReadOnlyCollection<ChatMessage>(_messages);

    /// <summary>
    /// 向历史记录添加消息。
    /// </summary>
    public void AddMessage(ChatMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        _messages.Add(message);
    }

    /// <summary>
    /// 创建并添加具有指定角色的文本消息。
    /// </summary>
    public void AddMessage(ChatRole role, string text)
    {
        _messages.Add(ChatMessage.CreateText(role, text));
    }

    /// <summary>
    /// 创建并添加具有指定角色和内容的消息。
    /// </summary>
    public void AddMessage(ChatRole role, IEnumerable<ContentBlock> content)
    {
        _messages.Add(new ChatMessage(role, content));
    }

    /// <summary>
    /// 清除会话中的所有消息。
    /// </summary>
    public void Clear()
    {
        _messages.Clear();
    }

    /// <summary>
    /// 获取会话中的消息数量。
    /// </summary>
    public int Count => _messages.Count;

    public SessionStore() { }

    public SessionStore(List<ChatMessage> initialMessages)
    {
        _messages = initialMessages ?? new List<ChatMessage>();
    }

    /// <summary>
    /// 用新集合替换所有消息。
    /// </summary>
    public void ReplaceMessages(IEnumerable<ChatMessage> newMessages)
    {
        ArgumentNullException.ThrowIfNull(newMessages);
        _messages.Clear();
        _messages.AddRange(newMessages);
    }
}
