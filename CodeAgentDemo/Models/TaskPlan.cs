namespace CodeAgentDemo.Models;

/// <summary>
/// 表示包含有序步骤的任务执行计划。
/// </summary>
public class TaskPlan
{
    /// <summary>
    /// 获取或设置要执行的有序步骤列表。
    /// </summary>
    public List<string> Steps { get; set; } = new();

    /// <summary>
    /// 获取或设置当前正在执行的步骤索引。
    /// </summary>
    public int CurrentStep { get; set; }

    /// <summary>
    /// 获取一个值，指示所有步骤是否已完成。
    /// </summary>
    public bool IsComplete => CurrentStep >= Steps.Count;

    /// <summary>
    /// 获取或设置原始任务描述。
    /// </summary>
    public string? OriginalTask { get; set; }

    /// <summary>
    /// 获取或设置复杂度评估结果。
    /// </summary>
    public ComplexityLevel Complexity { get; set; }

    /// <summary>
    /// 推进到计划中的下一步。
    /// </summary>
    public void AdvanceStep()
    {
        if (!IsComplete)
        {
            CurrentStep++;
        }
    }

    /// <summary>
    /// 获取当前步骤的描述，如果已完成则返回 null。
    /// </summary>
    public string? GetCurrentStepDescription()
    {
        return IsComplete ? null : Steps[CurrentStep];
    }
}

/// <summary>
/// 表示任务的复杂度级别。
/// </summary>
public enum ComplexityLevel
{
    /// <summary>
    /// 简单任务，可以直接执行，无需规划。
    /// </summary>
    Simple,

    /// <summary>
    /// 复杂任务，需要在执行前进行规划。
    /// </summary>
    Complex
}