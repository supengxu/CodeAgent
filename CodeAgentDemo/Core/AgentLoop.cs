using System.Text;
using CodeAgentDemo.Cli;
using CodeAgentDemo.Models;
using CodeAgentDemo.Providers;
using CodeAgentDemo.Tools;


namespace CodeAgentDemo.Core;

public record AgentResponse(IEnumerable<ContentBlock> Content, string StopReason);

public interface IAgentLoop
{
    IReadOnlyList<ChatMessage> History { get; }
    Task<AgentResponse> SendMessageAsync(string message, CancellationToken cancellationToken = default);
    Task RunAsync(CancellationToken cancellationToken = default);
    void RegisterTool(ITool tool);
}

public class AgentLoop : IAgentLoop
{
    private readonly IChatProvider _provider;
    private readonly IToolRegistry _tools;
    private readonly ChatOptions _options;
    private readonly IConsoleIO _console;
    private readonly ISessionCli _sessionCli;
    private readonly IConsoleUI _ui;
    private readonly IPlanningEngine? _planningEngine;
    private readonly ILoopController? _loopController;
    private readonly IReflectionEngine? _reflectionEngine;
    private readonly IContextManager? _contextManager;
    private int _toolCallCount;

    public IReadOnlyList<ChatMessage> History => _sessionCli.CurrentSession.Messages;

    public AgentLoop(IChatProvider provider, IToolRegistry tools, ChatOptions options,
        IConsoleIO console, ISessionCli sessionCli, IConsoleUI ui)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _tools = tools ?? throw new ArgumentNullException(nameof(tools));
        _options = options ?? new ChatOptions();
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _sessionCli = sessionCli ?? throw new ArgumentNullException(nameof(sessionCli));
        _ui = ui ?? throw new ArgumentNullException(nameof(ui));
        _toolCallCount = 0;

        sessionCli.InitializeAsync().GetAwaiter().GetResult();
        if (sessionCli.CurrentSession.Count > 0)
        {
            _ui.DisplaySessionHistory(sessionCli.CurrentSession.Messages);
        }
    }

    internal AgentLoop(IChatProvider provider, IToolRegistry tools, ChatOptions options,
        IConsoleIO console, ISessionCli sessionCli, IConsoleUI ui,
        IPlanningEngine? planningEngine, ILoopController? loopController,
        IReflectionEngine? reflectionEngine, IContextManager? contextManager)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _tools = tools ?? throw new ArgumentNullException(nameof(tools));
        _options = options ?? new ChatOptions();
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _sessionCli = sessionCli ?? throw new ArgumentNullException(nameof(sessionCli));
        _ui = ui ?? throw new ArgumentNullException(nameof(ui));
        _planningEngine = planningEngine;
        _loopController = loopController;
        _reflectionEngine = reflectionEngine;
        _contextManager = contextManager;
        _toolCallCount = 0;

        sessionCli.InitializeAsync().GetAwaiter().GetResult();
        if (sessionCli.CurrentSession.Count > 0)
        {
            _ui.DisplaySessionHistory(sessionCli.CurrentSession.Messages);
        }
    }

    public void RegisterTool(ITool tool)
    {
        _tools.Register(tool);
    }

    public async Task<AgentResponse> SendMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message cannot be empty", nameof(message));

        var userMessage = ChatMessage.CreateText(ChatRole.User, message);
        await _sessionCli.AppendMessageAsync(userMessage);

        var startTime = DateTime.Now;
        var totalInputTokens = 0;
        var totalOutputTokens = 0;

        // Reset loop controller for new message
        _loopController?.Reset();

        // Planning phase: assess complexity and potentially generate plan
        var plan = await TryGeneratePlanAsync(message, cancellationToken);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Loop control: check if iteration is allowed
            if (_loopController != null)
            {
                var canContinue = await _loopController.CheckIterationAsync(cancellationToken);
                if (!canContinue)
                {
                    var stopReason = DetermineLoopStopReason();
                    return new AgentResponse(Array.Empty<ContentBlock>(), stopReason);
                }
            }

            // Context compression: check and compress if needed
            await TryCompressContextAsync(cancellationToken);

            var response = await CollectStreamingResponseAsync(cancellationToken);

            if (response.Usage != null)
            {
                totalInputTokens += response.Usage.InputTokens;
                totalOutputTokens += response.Usage.OutputTokens;

                // Update loop controller with token usage
                _loopController?.UpdateTokenUsage(totalInputTokens + totalOutputTokens);
            }

            if (response.Content.Any())
            {
                var assistantMessage = new ChatMessage(ChatRole.Assistant, response.Content);
                await _sessionCli.AppendMessageAsync(assistantMessage);
            }

            if (response.StopReason == "tool_use" && response.ToolCalls.Count > 0)
            {
                if (_loopController != null)
                {
                    var stateHash = _loopController.GetStateHash(
                        string.Join(",", response.ToolCalls.Select(t => $"{t.Name}:{t.Arguments}")));
                    if (_loopController.DetectCycle(stateHash))
                    {
                        _ui.PrintWarning("检测到循环行为，停止执行");
                        return new AgentResponse(Array.Empty<ContentBlock>(), "cycle_detected");
                    }
                }

                foreach (var toolCall in response.ToolCalls)
                {
                    var result = await ExecuteToolWithReflectionAsync(toolCall, cancellationToken);

                    var toolResultMessage = new ChatMessage(ChatRole.Tool, new[]
                    {
                        new ToolResultBlock(toolCall.Id, result.Output, !result.Success)
                    });

                    await _sessionCli.AppendMessageAsync(toolResultMessage);
                }

                // Advance plan step if all tools succeeded
                if (plan != null && response.ToolCalls.All(tc => true))
                {
                    plan.AdvanceStep();
                }

                continue;
            }

            var endTime = DateTime.Now;
            _ui.PrintResponseUsage(startTime, endTime,
                totalInputTokens > 0 ? totalInputTokens : null,
                totalOutputTokens > 0 ? totalOutputTokens : null);

            return new AgentResponse(response.Content, response.StopReason);
        }
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _ui.PrintBanner();

        while (!cancellationToken.IsCancellationRequested)
        {
            _ui.PrintPrompt();
            var input = _console.ReadLine();

            if (input == null) break;
            if (string.IsNullOrWhiteSpace(input)) continue;

            if (input.Trim().ToLower() is "quit" or "exit")
            {
                await SaveAndExitAsync(cancellationToken);
                break;
            }

            if (input.StartsWith("/"))
            {
                var result = await _sessionCli.TryExecuteCommandAsync(input);
                if (result.IsCommand)
                {
                    if (result.Success)
                    {
                        _console.WriteLine(result.Message);
                    }
                    else
                    {
                        _ui.PrintError(result.Message);
                    }
                    continue;
                }
            }

            try
            {
                await SendMessageAsync(input, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _ui.PrintError(ex.Message);
            }
        }
    }

    private async Task SaveAndExitAsync(CancellationToken cancellationToken)
    {
        await _sessionCli.SaveCurrentSessionAsync();
        _console.WriteLine($"\nSession saved.");
        _ui.PrintStats(_sessionCli.CurrentSession.Count, _toolCallCount);

        _ui.PrintGoodbye();
    }

    /// <summary>
    /// Tries to generate a plan for complex tasks.
    /// </summary>
    private async Task<TaskPlan?> TryGeneratePlanAsync(string message, CancellationToken cancellationToken)
    {
        if (_planningEngine == null)
        {
            return null;
        }

        try
        {
            var complexity = await _planningEngine.AssessComplexityAsync(message, cancellationToken);

            if (complexity == ComplexityLevel.Complex)
            {
                _ui.PrintInfo("检测到复杂任务，正在生成执行计划...");
                var plan = await _planningEngine.GeneratePlanAsync(message, cancellationToken);

                if (plan.Steps.Count > 0)
                {
                    _ui.PrintInfo($"计划生成完成，共 {plan.Steps.Count} 个步骤");
                    for (int i = 0; i < plan.Steps.Count; i++)
                    {
                        _ui.PrintInfo($"  {i + 1}. {plan.Steps[i]}");
                    }
                }

                return plan;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _ui.PrintWarning($"规划失败: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Tries to compress context if needed.
    /// </summary>
    private async Task TryCompressContextAsync(CancellationToken cancellationToken)
    {
        if (_contextManager == null)
        {
            return;
        }

        try
        {
            if (_contextManager.ShouldCompress(History))
            {
                _ui.PrintInfo("上下文过长，正在压缩...");
                var result = await _contextManager.CompressAsync(History, cancellationToken);

                if (result.Success && result.RemovedTokens > 0)
                {
                    _ui.PrintInfo($"上下文压缩完成，移除 {result.RemovedTokens} tokens");

                    // Note: In a real implementation, we would need to update the session
                    // with the compressed messages. For now, we just log the compression.
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _ui.PrintWarning($"上下文压缩失败: {ex.Message}");
        }
    }

    /// <summary>
    /// Executes a tool with reflection on failure.
    /// </summary>
    private async Task<ToolResult> ExecuteToolWithReflectionAsync(ToolCall toolCall, CancellationToken cancellationToken)
    {
        var attemptCount = 1;
        const int maxReflectionAttempts = 3;

        while (true)
        {
            var result = await ExecuteToolAsync(toolCall, cancellationToken);

            if (result.Success)
            {
                return result;
            }

            // Check if reflection should be triggered
            if (_reflectionEngine == null)
            {
                return result;
            }

            var shouldReflect = await _reflectionEngine.ShouldReflectAsync(result, attemptCount, cancellationToken);
            if (!shouldReflect)
            {
                return result;
            }

            // Perform reflection
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

            if (!reflectionResult.ShouldRetry)
            {
                return result;
            }

            attemptCount++;
            if (attemptCount > maxReflectionAttempts)
            {
                _ui.PrintWarning("已达到最大重试次数");
                return result;
            }

            _ui.PrintInfo("正在重试...");
        }
    }

    /// <summary>
    /// Determines the stop reason when loop controller stops the loop.
    /// </summary>
    private string DetermineLoopStopReason()
    {
        if (_loopController == null)
        {
            return "end_turn";
        }

        var state = _loopController.State;

        if (state.IterationLimitReached)
        {
            _ui.PrintWarning("已达到最大迭代次数限制");
            return "iteration_limit";
        }

        if (state.TokenLimitReached)
        {
            _ui.PrintWarning("已达到 token 使用限制");
            return "token_limit";
        }

        if (state.DetectedCycle)
        {
            _ui.PrintWarning("检测到循环行为");
            return "cycle_detected";
        }

        return "loop_control_stop";
    }

    private async Task<StreamResponse> CollectStreamingResponseAsync(CancellationToken cancellationToken)
    {
        _options.Tools = _tools.GetAllTools()
            .Select(t => new ToolDefinition(t.Name, t.Description, t.InputSchema))
            .ToList();

        var textBuilder = new StringBuilder();
        var thinkingBuilder = new StringBuilder();
        var toolCallBuilders = new List<ToolCallBuilder>();
        string? stopReason = null;
        bool toolCallDetected = false;
        UsageInfo? usage = null;

        _ui.BeginStream();

        await foreach (var chunk in _provider.CompleteStreamingAsync(History, _options, cancellationToken))
        {
            if (!string.IsNullOrEmpty(chunk.TextDelta))
            {
                _ui.StreamText(chunk.TextDelta);
                textBuilder.Append(chunk.TextDelta);
            }

            if (!string.IsNullOrEmpty(chunk.ThinkingDelta))
            {
                _ui.StreamThinking(chunk.ThinkingDelta);
                thinkingBuilder.Append(chunk.ThinkingDelta);
            }

            if (chunk.ToolCallDelta != null)
            {
                if (!toolCallDetected)
                {
                    _ui.PrintToolCallDetected();
                    toolCallDetected = true;
                }
                AccumulateToolCall(toolCallBuilders, chunk.ToolCallDelta);
            }

            if (!string.IsNullOrEmpty(chunk.StopReason))
            {
                stopReason = chunk.StopReason;
            }

            if (chunk.Usage != null)
            {
                usage = chunk.Usage;
            }
        }

        _ui.EndStream();

        var contentBlocks = BuildContentBlocks(textBuilder, thinkingBuilder);
        var toolCalls = BuildToolCalls(toolCallBuilders);

        if (stopReason == null && toolCalls.Count > 0)
        {
            stopReason = "tool_use";
        }

        return new StreamResponse(contentBlocks, toolCalls, stopReason ?? "end_turn", usage);
    }

    private void AccumulateToolCall(List<ToolCallBuilder> builders, ToolCallDelta delta)
    {
        if (!string.IsNullOrEmpty(delta.Id))
        {
            builders.Add(new ToolCallBuilder { Id = delta.Id, Name = delta.Name });
        }
        else if (builders.Count > 0 && !string.IsNullOrEmpty(delta.Name))
        {
            builders.Last().Name = delta.Name;
        }

        if (builders.Count > 0 && !string.IsNullOrEmpty(delta.ArgumentsDelta))
        {
            builders.Last().ArgumentsBuilder.Append(delta.ArgumentsDelta);
        }
    }

    private IEnumerable<ContentBlock> BuildContentBlocks(StringBuilder text, StringBuilder thinking)
    {
        var blocks = new List<ContentBlock>();
        if (thinking.Length > 0) blocks.Add(new ThinkingBlock(thinking.ToString()));
        if (text.Length > 0) blocks.Add(new TextBlock(text.ToString()));
        return blocks;
    }

    private IReadOnlyList<ToolCall> BuildToolCalls(List<ToolCallBuilder> builders)
    {
        return builders.Select(b => new ToolCall(
            b.Id ?? throw new InvalidOperationException("Tool call ID is null"),
            b.Name ?? throw new InvalidOperationException("Tool call name is null"),
            System.Text.Json.JsonDocument.Parse(b.ArgumentsBuilder.ToString()).RootElement
        )).ToList();
    }

    private async Task<ToolResult> ExecuteToolAsync(ToolCall toolCall, CancellationToken cancellationToken)
    {
        var tool = _tools.GetTool(toolCall.Name);
        if (tool == null)
        {
            _ui.PrintError($"工具 '{toolCall.Name}' 未找到");
            return new ToolResult(false, $"Tool '{toolCall.Name}' not found.");
        }

        _ui.PrintToolCallStart(toolCall.Name, toolCall.Arguments.ToString());

        bool confirmed;
        if (tool.RequiresConfirmation(toolCall.Arguments))
        {
            _ui.PrintToolConfirmation(toolCall.Name);
            _console.Write("    执行? [y/N]: ");

            var confirmation = _console.ReadLine();
            confirmed = confirmation?.ToLower() == "y";
        }
        else
        {
            confirmed = true;
        }

        if (!confirmed)
        {
            _ui.PrintWarning("工具执行已取消");
            return new ToolResult(false, "Tool execution cancelled by user");
        }

        try
        {
            _ui.PrintToolExecuting(toolCall.Name);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMinutes(5));
            var result = await tool.ExecuteAsync(toolCall.Arguments, cts.Token);
            _toolCallCount++;
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

    private class ToolCallBuilder
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public StringBuilder ArgumentsBuilder { get; } = new();
    }
}