namespace CodeAgentDemo.Models;

/// <summary>
/// 表示消息发送者在对话中的角色。
/// </summary>
public enum ChatRole
{
    /// <summary>
    /// 系统角色，用于提供指令和上下文。
    /// </summary>
    System,

    /// <summary>
    /// 用户角色，用于人类输入。
    /// </summary>
    User,

    /// <summary>
    /// 助手角色，用于 AI 响应。
    /// </summary>
    Assistant,

    /// <summary>
    /// 工具角色，用于工具执行结果。
    /// </summary>
    Tool
}
