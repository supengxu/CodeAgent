using System.Text;
using System.Text.Json;
using CodeAgentDemo.Models;
using CodeAgentDemo.Providers;

namespace CodeAgentDemo.Core;

/// <summary>
/// Implementation of task planning engine that assesses task complexity and generates execution plans.
/// </summary>
public class PlanningEngine : IPlanningEngine
{
    private readonly IChatProvider _provider;
    private readonly PlanningConfig _config;

    // Rough estimate: ~4 characters per token on average
    private const int CharsPerToken = 4;

    // Keywords that suggest tool usage
    private static readonly string[] ToolKeywords = new[]
    {
        "read", "write", "edit", "create", "delete", "search", "find",
        "execute", "run", "build", "test", "commit", "grep", "bash"
    };

    /// <summary>
    /// Initializes a new instance of the PlanningEngine class.
    /// </summary>
    /// <param name="provider">The chat provider for LLM calls.</param>
    /// <param name="config">Planning configuration options.</param>
    public PlanningEngine(IChatProvider provider, PlanningConfig config)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <inheritdoc />
    public Task<ComplexityLevel> AssessComplexityAsync(string task, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);

        // Estimate token count from character count
        int estimatedTokens = EstimateTokens(task);

        // Check if estimated tokens exceed threshold
        if (estimatedTokens > _config.ComplexityThreshold)
        {
            return Task.FromResult(ComplexityLevel.Complex);
        }

        // Estimate expected tool calls from keywords
        int estimatedToolCalls = EstimateToolCalls(task);

        // Check if estimated tool calls exceed minimum for complexity
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
            // Timeout occurred - return a simple plan with the original task
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

            // Try to extract step content from numbered list format
            var step = ExtractStepContent(trimmedLine);
            if (!string.IsNullOrWhiteSpace(step))
            {
                steps.Add(step);
            }
        }

        // If no steps were parsed, use the entire response as a single step
        if (steps.Count == 0 && !string.IsNullOrWhiteSpace(responseText))
        {
            steps.Add(responseText.Trim());
        }

        return steps;
    }

    private static string? ExtractStepContent(string line)
    {
        // Match patterns like "1. Step", "1) Step", "1- Step", "1 Step"
        for (int i = 0; i < line.Length; i++)
        {
            if (char.IsDigit(line[i]))
            {
                continue;
            }

            // Skip common separators after number
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

        // If no numbered format detected, return the line as-is
        return line;
    }

    private static int EstimateTokens(string text)
    {
        // Rough estimation: ~4 characters per token on average
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