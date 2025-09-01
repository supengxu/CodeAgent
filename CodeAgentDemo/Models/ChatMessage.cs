namespace CodeAgentDemo.Models;

/// <summary>
/// Represents a chat message with role and content blocks.
/// </summary>
public record ChatMessage(
    ChatRole Role,
    IEnumerable<ContentBlock> Content
)
{
    /// <summary>
    /// Creates a simple text message.
    /// </summary>
    public static ChatMessage CreateText(ChatRole role, string text)
    {
        return new ChatMessage(role, new[] { new TextBlock(text) });
    }
}
