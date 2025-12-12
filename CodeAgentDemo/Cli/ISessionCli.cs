using System;
using System.Threading.Tasks;
using CodeAgentDemo.Core;
using CodeAgentDemo.Models;

namespace CodeAgentDemo.Cli;

/// <summary>
/// 会话 CLI 接口
/// </summary>
public interface ISessionCli
{
    /// <summary>
    /// 当前会话
    /// </summary>
    SessionStore CurrentSession { get; }

    /// <summary>
    /// 当前会话 ID
    /// </summary>
    string CurrentSessionId { get; }

    /// <summary>
    /// 尝试执行命令
    /// </summary>
    Task<CliResult> TryExecuteCommandAsync(string input);

    /// <summary>
    /// 初始化会话
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// 保存当前会话
    /// </summary>
    Task SaveCurrentSessionAsync();

    /// <summary>
    /// 追加消息
    /// </summary>
    Task AppendMessageAsync(ChatMessage message);
}