using CodeAgentDemo.Models;

namespace CodeAgentDemo.Core;

/// <summary>
/// 输入处理器接口
/// </summary>
public interface IInputHandler
{
    /// <summary>
    /// 异步读取用户输入
    /// </summary>
    /// <param name="ct">取消令牌</param>
    /// <returns>输入结果</returns>
    Task<InputResult> ReadInputAsync(CancellationToken ct);

    /// <summary>
    /// 取消当前输入
    /// </summary>
    void CancelInput();
}