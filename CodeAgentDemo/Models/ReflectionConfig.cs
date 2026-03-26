namespace CodeAgentDemo.Models;

/// <summary>
/// 反思引擎行为的配置。
/// 反思仅在失败时触发，用于自我纠正。
/// </summary>
public class ReflectionConfig
{
    /// <summary>
    /// 反思后的最大重试次数。
    /// 默认值为 3。
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// 反思的令牌预算比例（0.0 到 1.0）。
    /// 分配给反思的最大令牌预算百分比。
    /// 默认值为 0.2（20%）。
    /// </summary>
    public double TokenBudgetRatio { get; set; } = 0.2;
}