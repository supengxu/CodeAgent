using CodeAgentDemo.Models;

namespace CodeAgentDemo.Core;

/// <summary>
/// 任务规划引擎的接口，用于评估任务复杂度并生成执行计划。
/// </summary>
public interface IPlanningEngine
{
    /// <summary>
    /// 评估给定任务的复杂度。
    /// </summary>
    /// <param name="task">要评估的任务描述。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>评估后的复杂度级别。</returns>
    Task<ComplexityLevel> AssessComplexityAsync(string task, CancellationToken cancellationToken = default);

    /// <summary>
    /// 为复杂任务生成执行计划。
    /// </summary>
    /// <param name="task">要规划的任务描述。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含有序执行步骤的任务计划。</returns>
    Task<TaskPlan> GeneratePlanAsync(string task, CancellationToken cancellationToken = default);
}