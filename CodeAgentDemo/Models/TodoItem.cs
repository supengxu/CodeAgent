namespace CodeAgentDemo.Models;

/// <summary>
/// 表示待办事项的状态。
/// </summary>
public enum TodoStatus
{
    /// <summary>
    /// 任务待处理，尚未开始。
    /// </summary>
    Pending,

    /// <summary>
    /// 任务正在处理中。
    /// </summary>
    InProgress,

    /// <summary>
    /// 任务已完成。
    /// </summary>
    Completed
}

/// <summary>
/// 表示带有状态跟踪的待办事项。
/// </summary>
public class TodoItem
{
    /// <summary>
    /// 获取或设置待办事项的唯一标识符。
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// 获取或设置待办事项的描述。
    /// </summary>
    public required string Text { get; set; }

    /// <summary>
    /// 获取或设置待办事项的当前状态。
    /// </summary>
    public TodoStatus Status { get; set; } = TodoStatus.Pending;

    /// <summary>
    /// 获取或设置待办事项的优先级。
    /// </summary>
    public string Priority { get; set; } = "medium";

    /// <summary>
    /// 获取创建时间戳。
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.Now;

    /// <summary>
    /// 获取或设置最后更新时间戳。
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}