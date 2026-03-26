using System.Text;
using System.Text.Json;
using CodeAgentDemo.Models;
using CodeAgentDemo.Providers;
using CodeAgentDemo.Tools;

namespace CodeAgentDemo.Core;

/// <summary>
/// 反思引擎的实现，用于分析失败并生成纠正建议。
/// 反思仅在失败时触发，成功时不会触发。
/// </summary>
public class ReflectionEngine : IReflectionEngine
{
    private readonly IChatProvider _chatProvider;
    private readonly ReflectionConfig _config;
    private int _totalTokensUsed;
    private int _tokenBudget;

    /// <summary>
    /// 反思分析的系统提示。
    /// </summary>
    private const string ReflectionSystemPrompt = @"You are a reflection engine that analyzes tool execution failures and provides correction suggestions.

Analyze the failure context and provide:
1. A brief analysis of what went wrong
2. Specific suggestions for correction
3. Whether a retry should be attempted

Format your response as JSON:
{
  ""analysis"": ""Brief analysis of the failure"",
  ""suggestions"": [""Suggestion 1"", ""Suggestion 2""],
  ""shouldRetry"": true/false
}

Be concise and focus on actionable corrections.";

    /// <summary>
    /// 初始化 ReflectionEngine 类的新实例。
    /// </summary>
    /// <param name="chatProvider">用于 LLM 调用的聊天提供商。</param>
    /// <param name="config">反思配置。</param>
    /// <param name="totalTokenBudget">会话的总 token 预算（用于计算反思预算）。</param>
    public ReflectionEngine(IChatProvider chatProvider, ReflectionConfig? config = null, int totalTokenBudget = 100000)
    {
        _chatProvider = chatProvider ?? throw new ArgumentNullException(nameof(chatProvider));
        _config = config ?? new ReflectionConfig();
        _tokenBudget = (int)(totalTokenBudget * _config.TokenBudgetRatio);
        _totalTokensUsed = 0;
    }

    /// <inheritdoc/>
    public Task<bool> ShouldReflectAsync(ToolResult toolResult, int attemptCount, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(toolResult);

        // 不对成功结果进行反思
        if (toolResult.Success)
        {
            return Task.FromResult(false);
        }

        // 如果已达到最大重试次数，则不进行反思
        if (attemptCount >= _config.MaxRetryAttempts)
        {
            return Task.FromResult(false);
        }

        // 如果 token 预算已超出，则不进行反思
        if (_totalTokensUsed >= _tokenBudget)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    /// <summary>
    /// 有意义的反思所需的最少 token 数量。
    /// </summary>
    private const int MinReflectionTokens = 100;

    /// <inheritdoc/>
    public async Task<ReflectionResult> ReflectAsync(ReflectionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // 检查是否已请求取消
        if (cancellationToken.IsCancellationRequested)
        {
            return new ReflectionResult
            {
                Analysis = "Reflection was cancelled.",
                Suggestions = [],
                ShouldRetry = false
            };
        }

        // 在进行 LLM 调用前检查 token 预算
        var remainingBudget = _tokenBudget - _totalTokensUsed;
        if (remainingBudget <= 0 || remainingBudget < MinReflectionTokens)
        {
            return new ReflectionResult
            {
                Analysis = "Token budget exceeded, cannot perform reflection.",
                Suggestions = [],
                ShouldRetry = false
            };
        }

        var userPrompt = BuildReflectionPrompt(context);
        var messages = new List<ChatMessage>
        {
            ChatMessage.CreateText(ChatRole.User, userPrompt)
        };

        var options = new ChatOptions
        {
            MaxTokens = Math.Min(1024, remainingBudget),
            SystemPrompt = ReflectionSystemPrompt
        };

        try
        {
            var responseBuilder = new StringBuilder();
            int? inputTokens = null;
            int? outputTokens = null;

            await foreach (var chunk in _chatProvider.CompleteStreamingAsync(messages, options, cancellationToken))
            {
                if (!string.IsNullOrEmpty(chunk.TextDelta))
                {
                    responseBuilder.Append(chunk.TextDelta);
                }

                if (chunk.Usage != null)
                {
                    inputTokens = chunk.Usage.InputTokens;
                    outputTokens = chunk.Usage.OutputTokens;
                }
            }

            // 更新 token 使用量
            if (inputTokens.HasValue && outputTokens.HasValue)
            {
                _totalTokensUsed += inputTokens.Value + outputTokens.Value;
            }
            else
            {
                // 如果未提供使用量，则估算 token（粗略估算：每 token 4 个字符）
                _totalTokensUsed += (userPrompt.Length + responseBuilder.Length) / 4;
            }

            var response = responseBuilder.ToString();
            return ParseReflectionResult(response, context.AttemptNumber);
        }
        catch (OperationCanceledException)
        {
            return new ReflectionResult
            {
                Analysis = "Reflection was cancelled.",
                Suggestions = [],
                ShouldRetry = false
            };
        }
        catch (Exception ex)
        {
            return new ReflectionResult
            {
                Analysis = $"Reflection failed: {ex.Message}",
                Suggestions = [],
                ShouldRetry = false
            };
        }
    }

    /// <summary>
    /// 构建反思提示。
    /// </summary>
    private static string BuildReflectionPrompt(ReflectionContext context)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Analyze the following tool execution failure:");
        sb.AppendLine();
        sb.AppendLine($"Tool Name: {context.ToolName}");
        sb.AppendLine($"Attempt Number: {context.AttemptNumber}");
        sb.AppendLine($"Error Message: {context.ErrorMessage}");
        sb.AppendLine($"Arguments: {context.Arguments}");
        sb.AppendLine();
        sb.AppendLine("Provide your analysis and suggestions in JSON format.");
        return sb.ToString();
    }

    /// <summary>
    /// 将 LLM 响应解析为 ReflectionResult。
    /// </summary>
    private ReflectionResult ParseReflectionResult(string response, int attemptNumber)
    {
        try
        {
            // Try to extract JSON from the response
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonStr = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var json = JsonDocument.Parse(jsonStr);
                var root = json.RootElement;

                var analysis = root.TryGetProperty("analysis", out var analysisElement)
                    ? analysisElement.GetString() ?? "No analysis provided"
                    : "No analysis provided";

                var suggestions = new List<string>();
                if (root.TryGetProperty("suggestions", out var suggestionsElement) &&
                    suggestionsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var suggestion in suggestionsElement.EnumerateArray())
                    {
                        var s = suggestion.GetString();
                        if (!string.IsNullOrEmpty(s))
                        {
                            suggestions.Add(s);
                        }
                    }
                }

                var shouldRetry = root.TryGetProperty("shouldRetry", out var shouldRetryElement) &&
                                  shouldRetryElement.GetBoolean();

                // 如果已达到最大重试次数，则覆盖 shouldRetry
                if (attemptNumber >= _config.MaxRetryAttempts)
                {
                    shouldRetry = false;
                }

                return new ReflectionResult
                {
                    Analysis = analysis,
                    Suggestions = suggestions,
                    ShouldRetry = shouldRetry
                };
            }
        }
        catch (JsonException)
        {
            // 继续执行默认解析
        }

        // 回退：将响应作为分析结果
        return new ReflectionResult
        {
            Analysis = response,
            Suggestions = [],
            ShouldRetry = attemptNumber < _config.MaxRetryAttempts
        };
    }

    /// <summary>
    /// 重置 token 使用量计数器。
    /// </summary>
    public void ResetTokenUsage()
    {
        _totalTokensUsed = 0;
    }

    /// <summary>
    /// 获取当前反思的 token 使用量。
    /// </summary>
    public int TokenUsage => _totalTokensUsed;

    /// <summary>
    /// 获取反思的 token 预算。
    /// </summary>
    public int TokenBudget => _tokenBudget;
}