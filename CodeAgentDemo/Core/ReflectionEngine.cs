using System.Text;
using System.Text.Json;
using CodeAgentDemo.Models;
using CodeAgentDemo.Providers;
using CodeAgentDemo.Tools;

namespace CodeAgentDemo.Core;

/// <summary>
/// Implementation of reflection engine that analyzes failures and generates correction suggestions.
/// Reflection is triggered only on failure, not on success.
/// </summary>
public class ReflectionEngine : IReflectionEngine
{
    private readonly IChatProvider _chatProvider;
    private readonly ReflectionConfig _config;
    private int _totalTokensUsed;
    private int _tokenBudget;

    /// <summary>
    /// System prompt for reflection analysis.
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
    /// Initializes a new instance of the ReflectionEngine class.
    /// </summary>
    /// <param name="chatProvider">The chat provider for LLM calls.</param>
    /// <param name="config">Reflection configuration.</param>
    /// <param name="totalTokenBudget">Total token budget for the session (used to calculate reflection budget).</param>
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

        // Do not reflect on success
        if (toolResult.Success)
        {
            return Task.FromResult(false);
        }

        // Do not reflect if max attempts reached
        if (attemptCount >= _config.MaxRetryAttempts)
        {
            return Task.FromResult(false);
        }

        // Do not reflect if token budget exceeded
        if (_totalTokensUsed >= _tokenBudget)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }

    /// <summary>
    /// Minimum tokens required for a meaningful reflection.
    /// </summary>
    private const int MinReflectionTokens = 100;

    /// <inheritdoc/>
    public async Task<ReflectionResult> ReflectAsync(ReflectionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Check if cancellation is already requested
        if (cancellationToken.IsCancellationRequested)
        {
            return new ReflectionResult
            {
                Analysis = "Reflection was cancelled.",
                Suggestions = [],
                ShouldRetry = false
            };
        }

        // Check token budget before making LLM call
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

            // Update token usage
            if (inputTokens.HasValue && outputTokens.HasValue)
            {
                _totalTokensUsed += inputTokens.Value + outputTokens.Value;
            }
            else
            {
                // Estimate tokens if usage not provided (rough estimate: 4 chars per token)
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
    /// Builds the reflection prompt from the context.
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
    /// Parses the LLM response into a ReflectionResult.
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

                // Override shouldRetry if max attempts reached
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
            // Fall through to default parsing
        }

        // Fallback: use the response as analysis
        return new ReflectionResult
        {
            Analysis = response,
            Suggestions = [],
            ShouldRetry = attemptNumber < _config.MaxRetryAttempts
        };
    }

    /// <summary>
    /// Resets the token usage counter.
    /// </summary>
    public void ResetTokenUsage()
    {
        _totalTokensUsed = 0;
    }

    /// <summary>
    /// Gets the current token usage for reflection.
    /// </summary>
    public int TokenUsage => _totalTokensUsed;

    /// <summary>
    /// Gets the token budget for reflection.
    /// </summary>
    public int TokenBudget => _tokenBudget;
}