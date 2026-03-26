using System.Text.Json;
using CodeAgentDemo.Models;
using CodeAgentDemo.Tools;

namespace CodeAgentDemo.Core;

/// <summary>
/// 反思引擎的接口，用于分析失败并生成纠正建议。
/// 反思仅在失败时触发，成功时不会触发。
/// </summary>
public interface IReflectionEngine
{
    /// <summary>
    /// 根据执行结果确定是否应该触发反思。
    /// </summary>
    /// <param name="toolResult">工具执行结果。</param>
    /// <param name="attemptCount">当前尝试次数（从 1 开始）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>如果应该触发反思则返回 true，否则返回 false。</returns>
    Task<bool> ShouldReflectAsync(ToolResult toolResult, int attemptCount, CancellationToken cancellationToken = default);

    /// <summary>
    /// 对失败的执行进行反思并生成建议。
    /// </summary>
    /// <param name="context">关于失败上下文的信息（工具名称、参数、错误消息）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含分析和建议的反思结果。</returns>
    Task<ReflectionResult> ReflectAsync(ReflectionContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// 包含失败执行相关信息的反思上下文。
/// </summary>
public record ReflectionContext
{
    /// <summary>
    /// 失败工具的名称。
    /// </summary>
    public string ToolName { get; init; } = string.Empty;

    /// <summary>
    /// 传递给工具的参数。
    /// </summary>
    public JsonElement Arguments { get; init; }

    /// <summary>
    /// 失败执行的错误消息。
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// 当前尝试次数。
    /// </summary>
    public int AttemptNumber { get; init; }
}