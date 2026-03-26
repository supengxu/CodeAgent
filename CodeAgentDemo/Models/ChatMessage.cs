namespace CodeAgentDemo.Models;

/// <summary>
/// 表示聊天消息，包含角色和内容块。
/// </summary>
public record ChatMessage(
    ChatRole Role,
    IEnumerable<ContentBlock> Content
)
{
    /// <summary>
    /// 创建简单的文本消息。
    /// </summary>
    public static ChatMessage CreateText(ChatRole role, string text)
    {
        return new ChatMessage(role, new[] { new TextBlock(text) });
    }
}
