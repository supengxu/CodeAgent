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
    private readonly ILayoutRenderer? _layoutRenderer;

    private readonly IMessageHandler _messageHandler;
    private readonly IToolExecutor _toolExecutor;
    private readonly IStreamProcessor _streamProcessor;
    private readonly ILoopManager? _loopManager;
    private readonly IPlanningEngine? _planningEngine;
    private readonly IContextManager? _contextManager;
    private readonly TodoManager? _todoManager;
    private int _roundsSinceLastTodo;

    public IReadOnlyList<ChatMessage> History => _sessionCli.CurrentSession.Messages;

    public AgentLoop(
        IChatProvider provider,
        IToolRegistry tools,
        ChatOptions options,
        IConsoleIO console,
        ISessionCli sessionCli,
        IConsoleUI ui,
        IMessageHandler messageHandler,
        IToolExecutor toolExecutor,
        IStreamProcessor streamProcessor,
        ILoopManager? loopManager = null,
        IPlanningEngine? planningEngine = null,
        IContextManager? contextManager = null,
        ILayoutRenderer? layoutRenderer = null,
        TodoManager? todoManager = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _tools = tools ?? throw new ArgumentNullException(nameof(tools));
        _options = options ?? new ChatOptions();
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _sessionCli = sessionCli ?? throw new ArgumentNullException(nameof(sessionCli));
        _ui = ui ?? throw new ArgumentNullException(nameof(ui));
        _messageHandler = messageHandler ?? throw new ArgumentNullException(nameof(messageHandler));
        _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
        _streamProcessor = streamProcessor ?? throw new ArgumentNullException(nameof(streamProcessor));
        _loopManager = loopManager;
        _planningEngine = planningEngine;
        _contextManager = contextManager;
        _layoutRenderer = layoutRenderer;
        _todoManager = todoManager;

        InitializeSession();
    }

    private void InitializeSession()
    {
        _sessionCli.InitializeAsync().GetAwaiter().GetResult();
        if (_sessionCli.CurrentSession.Count > 0)
        {
            _ui.DisplaySessionHistory(_sessionCli.CurrentSession.Messages);
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

        await _messageHandler.CreateUserMessageAsync(message);

        var startTime = DateTime.Now;
        var totalInputTokens = 0;
        var totalOutputTokens = 0;

        _loopManager?.Reset();

        var plan = await TryGeneratePlanAsync(message, cancellationToken);
        if (plan != null && plan.Steps.Count > 0)
        {
            await InjectPlanContextAsync(plan);
        }

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_loopManager != null)
            {
                var canContinue = await _loopManager.CanContinueAsync(cancellationToken);
                if (!canContinue)
                {
                    return new AgentResponse(Array.Empty<ContentBlock>(), _loopManager.DetermineStopReason());
                }
            }

            await TryCompressContextAsync(cancellationToken);

            var response = await CollectStreamingResponseAsync(cancellationToken);

            if (response.Usage != null)
            {
                totalInputTokens += response.Usage.InputTokens;
                totalOutputTokens += response.Usage.OutputTokens;
                _loopManager?.UpdateTokenUsage(totalInputTokens + totalOutputTokens);
            }

            if (response.Content.Any())
            {
                await _messageHandler.CreateAssistantMessageAsync(response.Content, cancellationToken);
            }

            if (response.StopReason == "tool_use" && response.ToolCalls.Count > 0)
            {
                if (_loopManager != null)
                {
                    var stateHash = _loopManager.GetStateHash(
                        string.Join(",", response.ToolCalls.Select(t => $"{t.Name}:{t.Arguments}")));
                    if (_loopManager.DetectCycle(stateHash))
                    {
                        _ui.PrintWarning("检测到循环行为，停止执行");
                        return new AgentResponse(Array.Empty<ContentBlock>(), "cycle_detected");
                    }
                }

                foreach (var toolCall in response.ToolCalls)
                {
                    var result = await _toolExecutor.ExecuteAsync(toolCall, cancellationToken);
                    await _messageHandler.CreateToolResultMessageAsync(toolCall.Id, result.Output, !result.Success);

                    if (toolCall.Name == "todo")
                    {
                        _roundsSinceLastTodo = 0;
                    }
                    else
                    {
                        _roundsSinceLastTodo++;
                    }
                }

                if (_todoManager != null && _todoManager.ShouldNag(_roundsSinceLastTodo, 3))
                {
                    var reminderText = "<reminder>请更新你的待办事项列表。使用 todo 工具查看或更新进度。</reminder>";
                    await _messageHandler.CreateSystemMessageAsync(reminderText);
                }

                if (plan != null)
                {
                    plan.AdvanceStep();
                    await UpdatePlanProgressAsync(plan);
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
            var input = await ReadMultiLineInputAsync(cancellationToken);

            if (input == null) break;
            if (string.IsNullOrWhiteSpace(input)) continue;

            if (input.Trim().ToLower() is "quit" or "exit")
            {
                await SaveAndExitAsync();
                break;
            }

            if (input.StartsWith("/"))
            {
                var result = await _sessionCli.TryExecuteCommandAsync(input);
                if (result.IsCommand)
                {
                    if (result.Success)
                    {
                        if (result.ClearScreen)
                        {
                            Console.Clear();
                        }
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
                _ui.PrintThinking();
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

    private async Task<string?> ReadMultiLineInputAsync(CancellationToken cancellationToken)
    {
        if (_layoutRenderer != null)
        {
            var multiLineInput = new MultiLineInput("> ");
            _layoutRenderer.RenderInput("输入 (Ctrl+Enter 提交):", multiLineInput);

            try
            {
                var result = await multiLineInput.ReadInputAsync(cancellationToken);

                return result.State switch
                {
                    InputState.Submitted => result.Text,
                    InputState.Cancelled => null,
                    InputState.Empty => string.Empty,
                    _ => null
                };
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        return _console.ReadLine();
    }

    private async Task SaveAndExitAsync()
    {
        await _sessionCli.SaveCurrentSessionAsync();
        _console.WriteLine("\nSession saved.");
        _ui.PrintStats(_sessionCli.CurrentSession.Count, _toolExecutor.ToolCallCount);
        _ui.PrintGoodbye();
    }

    private async Task<TaskPlan?> TryGeneratePlanAsync(string message, CancellationToken cancellationToken)
    {
        if (_planningEngine == null) return null;

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

    private async Task InjectPlanContextAsync(TaskPlan plan)
    {
        var planText = new StringBuilder();
        planText.AppendLine("【执行计划】请按以下步骤执行任务：");

        for (int i = 0; i < plan.Steps.Count; i++)
        {
            planText.AppendLine($"  步骤 {i + 1}: {plan.Steps[i]}");
        }

        planText.AppendLine("\n请严格按照上述步骤依次执行，完成当前步骤后再进行下一步。");

        await _messageHandler.CreateSystemMessageAsync(planText.ToString());
    }

    private async Task UpdatePlanProgressAsync(TaskPlan plan)
    {
        if (plan.IsComplete)
        {
            await _messageHandler.CreateSystemMessageAsync("【计划进度】所有步骤已完成！");
        }
        else
        {
            var progressText = $"【计划进度】当前应执行步骤 {plan.CurrentStep + 1}/{plan.Steps.Count}: {plan.GetCurrentStepDescription()}";
            await _messageHandler.CreateSystemMessageAsync(progressText);
        }
    }

    private async Task TryCompressContextAsync(CancellationToken cancellationToken)
    {
        if (_contextManager == null) return;

        try
        {
            if (_contextManager.ShouldCompress(History))
            {
                _ui.PrintInfo("上下文过长，正在压缩...");
                var result = await _contextManager.CompressAsync(History, cancellationToken);

                if (result.Success && result.RemovedTokens > 0)
                {
                    _ui.PrintInfo($"上下文压缩完成，移除 {result.RemovedTokens} tokens");
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

    private async Task<StreamResponse> CollectStreamingResponseAsync(CancellationToken cancellationToken)
    {
        _options.Tools = _tools.GetAllTools()
            .Select(t => new ToolDefinition(t.Name, t.Description, t.InputSchema))
            .ToList();

        var stream = _provider.CompleteStreamingAsync(History, _options, cancellationToken);
        return await _streamProcessor.ProcessAsync(stream, cancellationToken);
    }
}