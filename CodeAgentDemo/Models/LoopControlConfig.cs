namespace CodeAgentDemo.Models;

/// <summary>
/// 循环控制机制的配置。
/// </summary>
public class LoopControlConfig
{
    /// <summary>
    /// 代理循环中允许的最大迭代次数。
    /// 默认值为 15。
    /// </summary>
    public int MaxIterations { get; set; } = 15;

    /// <summary>
    /// 令牌使用比例阈值（0.0 到 1.0），超过该值会触发警告。
    /// 默认值为 0.8（上下文窗口的 80%）。
    /// </summary>
    public double TokenLimitRatio { get; set; } = 0.8;

    /// <summary>
    /// 用于循环检测的最近状态数量。
    /// 默认值为 5。
    /// </summary>
    public int CycleDetectionWindow { get; set; } = 5;
}