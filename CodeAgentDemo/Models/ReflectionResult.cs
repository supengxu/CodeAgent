namespace CodeAgentDemo.Models;

/// <summary>
/// 反思分析的结果。
/// 包含失败分析和纠正建议。
/// </summary>
public record ReflectionResult
{
    /// <summary>
    /// 分析出了什么问题。
    /// </summary>
    public string Analysis { get; init; } = string.Empty;

    /// <summary>
    /// 纠正失败的建议列表。
    /// </summary>
    public List<string> Suggestions { get; init; } = [];

    /// <summary>
    /// 根据反思结果，是否应该重试。
    /// </summary>
    public bool ShouldRetry { get; init; }
}