using CodeAgentDemo.Models;

namespace CodeAgentDemo.Core;

/// <summary>
/// 控制 Agent 循环迭代和循环检测的接口。
/// </summary>
public interface ILoopController
{
    /// <summary>
    /// 获取当前循环状态。
    /// </summary>
    LoopState State { get; }

    /// <summary>
    /// 检查是否允许继续迭代并更新状态。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>如果允许迭代返回 true，否则返回 false 表示已达限制。</returns>
    Task<bool> CheckIterationAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 检测当前状态是否与最近状态形成循环。
    /// </summary>
    /// <param name="stateHash">当前状态的哈希值。</param>
    /// <returns>如果检测到循环返回 true。</returns>
    bool DetectCycle(string stateHash);

    /// <summary>
    /// 生成当前状态的哈希值用于循环检测。
    /// </summary>
    /// <param name="stateData">要哈希的状态数据。</param>
    /// <returns>表示状态的哈希字符串。</returns>
    string GetStateHash(string stateData);

    /// <summary>
    /// 更新当前状态的 token 使用量。
    /// </summary>
    /// <param name="tokenUsage">当前 token 使用量。</param>
    void UpdateTokenUsage(int tokenUsage);

    /// <summary>
    /// 重置循环控制器状态以开始新会话。
    /// </summary>
    void Reset();
}