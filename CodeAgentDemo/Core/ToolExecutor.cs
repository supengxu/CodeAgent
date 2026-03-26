using CodeAgentDemo.Models;
using CodeAgentDemo.Tools;

namespace CodeAgentDemo.Core;

/// <summary>
/// Implementation of tool executor with reflection-based retry support.
/// </summary>
public class ToolExecutor : IToolExecutor
{
    private readonly IToolRegistry _toolRegistry;
    private readonly IConsoleUI _ui;
    private readonly IConsoleIO _console;
    private readonly IReflectionEngine? _reflectionEngine;
    private int _toolCallCount;

    private const int MaxReflectionAttempts = 3;
    private const int DefaultTimeoutMinutes = 5;

    /// <inheritdoc />
    public int ToolCallCount => _toolCallCount;

    /// <summary>
    /// Initializes a new instance of the ToolExecutor class.
    /// </summary>
    /// <param name="toolRegistry">The tool registry.</param>
    /// <param name="ui">The console UI.</param>
    /// <param name="console">The console IO.</param>
    /// <param name="reflectionEngine">Optional reflection engine for retry logic.</param>
    public ToolExecutor(
        IToolRegistry toolRegistry,
        IConsoleUI ui,
        IConsoleIO console,
        IReflectionEngine? reflectionEngine = null)
    {
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _ui = ui ?? throw new ArgumentNullException(nameof(ui));
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _reflectionEngine = reflectionEngine;
        _toolCallCount = 0;
    }

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(ToolCall toolCall, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(toolCall);

        var attemptCount = 1;

        while (true)
        {
            var result = await ExecuteSingleAsync(toolCall, cancellationToken);

            if (result.Success)
            {
                _toolCallCount++;
                return result;
            }

            if (_reflectionEngine == null) return result;

            var shouldReflect = await _reflectionEngine.ShouldReflectAsync(result, attemptCount, cancellationToken);
            if (!shouldReflect) return result;

            _ui.PrintInfo($"工具执行失败 (尝试 {attemptCount})，正在反思...");

            var reflectionContext = new ReflectionContext
            {
                ToolName = toolCall.Name,
                Arguments = toolCall.Arguments,
                ErrorMessage = result.Output,
                AttemptNumber = attemptCount
            };

            var reflectionResult = await _reflectionEngine.ReflectAsync(reflectionContext, cancellationToken);

            _ui.PrintInfo($"反思分析: {reflectionResult.Analysis}");
            if (reflectionResult.Suggestions.Count > 0)
            {
                _ui.PrintInfo("建议:");
                foreach (var suggestion in reflectionResult.Suggestions)
                {
                    _ui.PrintInfo($"  - {suggestion}");
                }
            }

            if (!reflectionResult.ShouldRetry) return result;

            attemptCount++;
            if (attemptCount > MaxReflectionAttempts)
            {
                _ui.PrintWarning("已达到最大重试次数");
                return result;
            }

            _ui.PrintInfo("正在重试...");
        }
    }

    private async Task<ToolResult> ExecuteSingleAsync(ToolCall toolCall, CancellationToken cancellationToken)
    {
        var tool = _toolRegistry.GetTool(toolCall.Name);
        if (tool == null)
        {
            _ui.PrintError($"工具 '{toolCall.Name}' 未找到");
            return new ToolResult(false, $"Tool '{toolCall.Name}' not found.");
        }

        _ui.PrintToolCallStart(toolCall.Name, toolCall.Arguments.ToString());

        var confirmed = await ConfirmExecutionAsync(tool, toolCall.Arguments);
        if (!confirmed)
        {
            _ui.PrintWarning("工具执行已取消");
            return new ToolResult(false, "Tool execution cancelled by user");
        }

        try
        {
            _ui.PrintToolExecuting(toolCall.Name);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMinutes(DefaultTimeoutMinutes));

            var result = await tool.ExecuteAsync(toolCall.Arguments, cts.Token);
            _ui.PrintToolResult(toolCall.Name, result.Output, result.Success);

            return result;
        }
        catch (OperationCanceledException)
        {
            _ui.PrintToolResult(toolCall.Name, "执行超时", false);
            return new ToolResult(false, "Tool execution timed out.");
        }
        catch (Exception ex)
        {
            _ui.PrintToolResult(toolCall.Name, ex.Message, false);
            return new ToolResult(false, $"Error: {ex.Message}");
        }
    }

    private Task<bool> ConfirmExecutionAsync(ITool tool, System.Text.Json.JsonElement arguments)
    {
        if (!tool.RequiresConfirmation(arguments))
        {
            return Task.FromResult(true);
        }

        _ui.PrintToolConfirmation(tool.Name);
        _console.Write("    执行? [y/N]: ");

        var confirmation = _console.ReadLine();
        return Task.FromResult(confirmation?.ToLower() == "y");
    }
}