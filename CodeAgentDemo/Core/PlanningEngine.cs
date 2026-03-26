using System.Text;
using System.Text.Json;
using CodeAgentDemo.Models;
using CodeAgentDemo.Providers;

namespace CodeAgentDemo.Core;

/// <summary>
/// 任务规划引擎的实现，用于评估任务复杂度并生成执行计划。
/// </summary>
public class PlanningEngine : IPlanningEngine
{
    private readonly IChatProvider _provider;
    private readonly PlanningConfig _config;

    // 粗略估算：平均每 token 约 4 个字符
    private const int CharsPerToken = 4;

    // 暗示工具使用的关键词
    private static readonly string[] ToolKeywords = new[]
    {
        "read", "write", "edit", "create", "delete", "search", "find",
        "execute", "run", "build", "test", "commit", "grep", "bash"
    };

    /// <summary>
    /// 初始化 PlanningEngine 类的新实例。
    /// </summary>
    /// <param name="provider">用于 LLM 调用的聊天提供商。</param>
    /// <param name="config">规划配置选项。</param>
    public PlanningEngine(IChatProvider provider, PlanningConfig config)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <inheritdoc />
    public Task<ComplexityLevel> AssessComplexityAsync(string task, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);

        // 从字符数估算 token 数量
        int estimatedTokens = EstimateTokens(task);

        // 检查估算的 token 数量是否超过阈值
        if (estimatedTokens > _config.ComplexityThreshold)
        {
            return Task.FromResult(ComplexityLevel.Complex);
        }

        // 从关键词估算预期的工具调用次数
        int estimatedToolCalls = EstimateToolCalls(task);

        // 检查估算的工具调用次数是否超过复杂任务的最低要求
        if (estimatedToolCalls >= _config.MinToolCallsForComplex)
        {
            return Task.FromResult(ComplexityLevel.Complex);
        }

        return Task.FromResult(ComplexityLevel.Simple);
    }

    /// <inheritdoc />
    public async Task<TaskPlan> GeneratePlanAsync(string task, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);

        using var timeoutSource = new CancellationTokenSource(TimeSpan.FromSeconds(_config.TimeoutSeconds));
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutSource.Token);

        try
        {
            var plan = await GeneratePlanInternalAsync(task, linkedSource.Token);
            return plan;
        }
        catch (OperationCanceledException) when (timeoutSource.Token.IsCancellationRequested)
        {
            // 超时发生 - 返回包含原始任务的简单计划
            return new TaskPlan
            {
                OriginalTask = task,
                Steps = new List<string> { task },
                Complexity = ComplexityLevel.Simple
            };
        }
        catch (Exception ex)
        {
            JsonSerializer.Serialize(ex);
            // 其他异常发生 - 捕获并返回包含原始任务的简单计划
            return new TaskPlan
            {
                OriginalTask = task,
                Steps = new List<string> { task },
                Complexity = ComplexityLevel.Simple
            };
        }
    }

    private async Task<TaskPlan> GeneratePlanInternalAsync(string task, CancellationToken cancellationToken)
    {
        var systemPrompt = BuildSystemPrompt();
        var userMessage = BuildUserMessage(task);

        var messages = new[]
        {
            ChatMessage.CreateText(ChatRole.User, userMessage)
        };

        var options = new ChatOptions
        {
            SystemPrompt = systemPrompt,
            MaxTokens = 1024,
            EnableThinking = false
        };

        var textBuilder = new StringBuilder();

        await foreach (var chunk in _provider.CompleteStreamingAsync(messages, options, cancellationToken))
        {
            if (!string.IsNullOrEmpty(chunk.TextDelta))
            {
                textBuilder.Append(chunk.TextDelta);
            }
        }

        var responseText = textBuilder.ToString();
        var steps = ParseSteps(responseText);

        return new TaskPlan
        {
            OriginalTask = task,
            Steps = steps,
            Complexity = ComplexityLevel.Complex,
            CurrentStep = 0
        };
    }

    private static string BuildSystemPrompt()
    {
        return @"You are a task planning assistant. Your job is to break down complex tasks into clear, executable steps.

Given a task, output ONLY a numbered list of steps. Each step should be:
- Clear and actionable
- A single, atomic operation
- Independent where possible

Format:
1. [First step]
2. [Second step]
3. [Third step]
...

Do not include any explanation, reasoning, or additional text. Only output the numbered list.";
    }

    private static string BuildUserMessage(string task)
    {
        return $"Break down this task into executable steps:\n\n{task}";
    }

    private static List<string> ParseSteps(string responseText)
    {
        var steps = new List<string>();
        var lines = responseText.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmedLine))
            {
                continue;
            }

            // 尝试从编号列表格式中提取步骤内容
            var step = ExtractStepContent(trimmedLine);
            if (!string.IsNullOrWhiteSpace(step))
            {
                steps.Add(step);
            }
        }

        // 如果没有解析出步骤，则将整个响应作为单个步骤
        if (steps.Count == 0 && !string.IsNullOrWhiteSpace(responseText))
        {
            steps.Add(responseText.Trim());
        }

        return steps;
    }

    private static string? ExtractStepContent(string line)
    {
        // 匹配如 "1. Step"、"1) Step"、"1- Step"、"1 Step" 这样的模式
        for (int i = 0; i < line.Length; i++)
        {
            if (char.IsDigit(line[i]))
            {
                continue;
            }

            // 跳过数字后的常见分隔符
            if (line[i] is '.' or ')' or '-' or ' ')
            {
                var remaining = line.Substring(i + 1).Trim();
                if (!string.IsNullOrWhiteSpace(remaining))
                {
                    return remaining;
                }
            }

            break;
        }

        // 如果未检测到编号格式，则按原样返回该行
        return line;
    }

    private static int EstimateTokens(string text)
    {
        // 粗略估算：平均每 token 约 4 个字符
        return text.Length / CharsPerToken;
    }

    private static int EstimateToolCalls(string task)
    {
        var lowerTask = task.ToLowerInvariant();
        int count = 0;

        foreach (var keyword in ToolKeywords)
        {
            if (lowerTask.Contains(keyword))
            {
                count++;
            }
        }

        return count;
    }
}