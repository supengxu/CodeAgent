using CodeAgentDemo.Models;

namespace CodeAgentDemo.Core;

/// <summary>
/// 管理 Agent 循环迭代控制的接口，包括限制和循环检测。
/// </summary>
public interface ILoopManager
{
    /// <summary>
    /// 检查循环是否可以继续到下一次迭代。
    /// </summary>
    Task<bool> CanContinueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 检测给定状态哈希是否存在循环。
    /// </summary>
    bool DetectCycle(string stateHash);

    /// <summary>
    /// 获取给定状态数据的哈希值。
    /// </summary>
    string GetStateHash(string stateData);

    /// <summary>
    /// 更新 token 使用计数器。
    /// </summary>
    void UpdateTokenUsage(int totalTokens);

    /// <summary>
    /// 重置循环状态以开始新的迭代周期。
    /// </summary>
    void Reset();

    /// <summary>
    /// 获取当前循环状态。
    /// </summary>
    LoopState State { get; }

    /// <summary>
    /// 根据当前状态确定停止原因。
    /// </summary>
    string DetermineStopReason();
}