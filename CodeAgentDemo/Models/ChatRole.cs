namespace CodeAgentDemo.Models;

/// <summary>
/// Represents the role of a message sender in the conversation.
/// </summary>
public enum ChatRole
{
    /// <summary>
    /// System role for providing instructions and context.
    /// </summary>
    System,

    /// <summary>
    /// User role for human input.
    /// </summary>
    User,

    /// <summary>
    /// Assistant role for AI responses.
    /// </summary>
    Assistant,

    /// <summary>
    /// Tool role for tool execution results.
    /// </summary>
    Tool
}
